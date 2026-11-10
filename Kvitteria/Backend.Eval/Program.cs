using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Features.Extraction;
using Backend.Features.Extraction.LLM;
using Backend.Features.Mail;
using Backend.Features.Uploads;
using MimeKit;

// Runs the real extraction code (prompt, schema, row mapper, date and chain matching, the reconciliation retry
// loop) over a folder of receipts and compares each result with an expected file. The samples carry personal
// data and live outside the repository; see docs/development.md.

EvalOptions? options = EvalOptions.Parse(args);

if (options is null)
{
	Console.Error.WriteLine(EvalOptions.Usage);
	return 2;
}

using var http = new HttpClient { BaseAddress = new Uri(options.Url), Timeout = TimeSpan.FromMinutes(15) };

string[] extensions = [".txt", ".eml", ".html", ".jpg", ".jpeg"];
List<string> samples = Directory.EnumerateFiles(options.Folder)
	.Where(f => extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
	.Where(f => options.Only is null || Path.GetFileNameWithoutExtension(f).Contains(options.Only, StringComparison.OrdinalIgnoreCase))
	.Order(StringComparer.OrdinalIgnoreCase)
	.ToList();

if (samples.Count == 0)
{
	Console.Error.WriteLine($"No samples (.txt, .eml, .html, .jpg) in {options.Folder}.");
	return 2;
}

int totalRuns = 0;
int rightRuns = 0;
bool missingExpected = false;
var perSample = new List<(string Name, int Right, int Runs)>();

foreach (string path in samples)
{
	string name = Path.GetFileNameWithoutExtension(path);
	string expectedPath = Path.Combine(options.Folder, name + ".expected.json");
	Expected? expected = File.Exists(expectedPath)
		? JsonSerializer.Deserialize<Expected>(File.ReadAllText(expectedPath), Expected.Json)
		: null;

	if (expected is null)
	{
		Console.WriteLine($"{name}: no {name}.expected.json, skipped");
		missingExpected = true;
		continue;
	}

	string? text;

	try
	{
		text = await ReadTextAsync(path, options, http);
	}
	catch (Exception ex) when (ex is IOException or HttpRequestException or TaskCanceledException or InvalidDataException)
	{
		Console.WriteLine($"{name}: {ex.Message}");
		continue;
	}

	if (text is null)
		continue;

	int right = 0;

	for (int run = 1; run <= options.Runs; run++)
	{
		var clock = Stopwatch.StartNew();
		Outcome outcome = await ExtractAsync(text, options, http);
		string verdict = Compare(outcome.Receipt, expected);
		bool ok = verdict.Length == 0;

		if (ok)
			right++;

		string state = outcome.Receipt is null ? "FAILED" : outcome.Reconciled ? "reconciled" : "unreconciled";
		string detail = outcome.Receipt is null ? outcome.Error ?? "" : ok ? "" : verdict;
		Console.WriteLine($"{name} run {run}/{options.Runs}: {(ok ? "RIGHT" : "WRONG")} ({state}, {outcome.Attempts} attempt(s), {clock.Elapsed.TotalSeconds:0}s) {detail}".TrimEnd());
	}

	totalRuns += options.Runs;
	rightRuns += right;
	perSample.Add((name, right, options.Runs));
}

Console.WriteLine();
Console.WriteLine($"{rightRuns}/{totalRuns} runs fully right");

foreach ((string name, int right, int runs) in perSample)
	Console.WriteLine($"  {name}: {right}/{runs}");

return missingExpected || rightRuns < totalRuns ? 1 : 0;

// A .txt as is, a .eml/.html through the mail body rules, a .jpg transcribed once and cached beside the image.
static async Task<string?> ReadTextAsync(string path, EvalOptions options, HttpClient http)
{
	switch (Path.GetExtension(path).ToLowerInvariant())
	{
		case ".txt":
			return File.ReadAllText(path);

		case ".html":
			return MailBody.NormalizeWhitespace(HtmlToPlainText.Convert(File.ReadAllText(path)));

		case ".eml":
			using (FileStream stream = File.OpenRead(path))
			{
				string body = MailBody.From(MimeMessage.Load(stream), out string source);
				return source == "none" ? throw new InvalidDataException($"{Path.GetFileName(path)} has no text or HTML body.") : body;
			}

		default:
			string cache = Path.ChangeExtension(path, ".transcript.txt");

			if (File.Exists(cache))
				return File.ReadAllText(cache);

			byte[] jpeg = File.ReadAllBytes(path);

			if (!ReceiptImageLimits.TryValidate(jpeg, out int width, out int height, out _))
				throw new InvalidDataException($"{Path.GetFileName(path)} is over the upload budget ({width}x{height}, {jpeg.Length} bytes); scale it to at most {ReceiptImageLimits.MaxPixels} pixels first.");

			List<OllamaChatMessage> messages = [
				new("system", ReceiptExtractionPrompt.TranscriptionPrompt),
				new("user", "Transcribe this image.", [jpeg])
			];
			string transcript = (await ChatAsync(http, options, messages, format: null, temperature: 0)).Trim();

			if (ReceiptTranscription.IsNoReceipt(transcript))
				throw new InvalidDataException($"{Path.GetFileName(path)}: the model sees no receipt.");

			File.WriteAllText(cache, transcript);
			return transcript;
	}
}

// The worker's retry loop with the production defaults: temperature 0, then the retry temperature, the same
// hints, and the attempt whose figures come closest to reconciling.
static async Task<Outcome> ExtractAsync(string text, EvalOptions options, HttpClient http)
{
	ExtractedReceipt? best = null;
	decimal bestDelta = decimal.MaxValue;
	bool reconciled = false;
	string? hint = null;
	string? error = null;
	int attempt = 0;

	while (attempt < options.MaxAttempts && !reconciled)
	{
		attempt++;
		double temperature = attempt == 1 ? 0 : options.RetryTemperature;
		List<OllamaChatMessage> messages = [
			new("system", ReceiptExtractionPrompt.ExtractionPrompt),
			new("user", text)
		];

		if (hint is not null)
			messages.Add(new("user", hint));

		ExtractedReceipt result;

		try
		{
			string content = await ChatAsync(http, options, messages, ReceiptExtractionPrompt.Schema, temperature);
			ReceiptRowsAnswer answer = JsonSerializer.Deserialize<ReceiptRowsAnswer>(content, OllamaWire.JsonOptions)
				?? throw new OllamaResponseException("The content was the JSON literal null.");
			result = ReceiptRows.ToExtracted(answer, text);
		}
		catch (Exception ex) when (ex is OllamaResponseException or JsonException)
		{
			error = ex.Message;
			hint = ReceiptExtractionPrompt.MalformedJsonHint;
			continue;
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
		{
			error = ex.Message;
			continue;
		}

		decimal lineSum = result.LineItems.Sum(l => l.LineTotal);
		decimal delta = Reconciliation.Delta(result.GrandTotal, lineSum, result.PantTotal);
		reconciled = Reconciliation.IsReconciled(delta, options.Tolerance);

		if (best is null || reconciled || Math.Abs(delta) < Math.Abs(bestDelta))
		{
			best = result;
			bestDelta = delta;
		}

		hint = ReceiptExtractionPrompt.ReconciliationHint(lineSum + result.PantTotal, result.GrandTotal);
	}

	return new Outcome(best, reconciled, attempt, error);
}

// One /api/chat call, built from the records the backend itself sends.
static async Task<string> ChatAsync(HttpClient http, EvalOptions options, List<OllamaChatMessage> messages, JsonElement? format, double temperature)
{
	var request = new OllamaChatRequest(
		options.Model,
		messages,
		Think: false,
		Stream: false,
		Format: format,
		Options: new OllamaChatOptions(temperature, options.NumCtx, options.Threads),
		KeepAlive: "30m");

	using HttpResponseMessage response = await http.PostAsJsonAsync("api/chat", request, OllamaWire.JsonOptions);

	if (!response.IsSuccessStatusCode)
		throw new HttpRequestException($"Ollama answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

	OllamaChatResponse? chat = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(OllamaWire.JsonOptions);

	return string.IsNullOrWhiteSpace(chat?.Message?.Content)
		? throw new OllamaResponseException("Ollama returned an empty message.")
		: chat.Message.Content;
}

// What differs from the expected file, or an empty string when everything it states matches.
static string Compare(ExtractedReceipt? receipt, Expected expected)
{
	if (receipt is null)
		return "no extraction";

	List<string> differences = [];

	if (expected.Merchant is not null && receipt.Merchant != expected.Merchant)
		differences.Add($"merchant '{receipt.Merchant}' != '{expected.Merchant}'");

	if (expected.PurchaseDate is not null && receipt.PurchaseDate != expected.PurchaseDate)
		differences.Add($"date '{receipt.PurchaseDate}' != '{expected.PurchaseDate}'");

	if (expected.Total is { } total && receipt.GrandTotal != total)
		differences.Add($"total {receipt.GrandTotal.ToString(CultureInfo.InvariantCulture)} != {total.ToString(CultureInfo.InvariantCulture)}");

	if (expected.Pant is { } pant && receipt.PantTotal != pant)
		differences.Add($"pant {receipt.PantTotal.ToString(CultureInfo.InvariantCulture)} != {pant.ToString(CultureInfo.InvariantCulture)}");

	if (expected.LineCount is { } count && receipt.LineItems.Count != count)
		differences.Add($"{receipt.LineItems.Count} lines != {count}");

	return string.Join("; ", differences);
}

internal sealed record Outcome(ExtractedReceipt? Receipt, bool Reconciled, int Attempts, string? Error);

/// <summary>
/// The figures a sample must come out with. Every field is optional; one that is absent is not compared.
/// <c>PurchaseDate</c> is the printed local date as <c>yyyy-MM-dd HH:mm:ss</c>, or <c>""</c> for a receipt that
/// prints none (the capture time then applies later, in ingestion).
/// </summary>
internal sealed record Expected(string? Merchant, string? PurchaseDate, decimal? Total, decimal? Pant, int? LineCount)
{
	public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
}

internal sealed record EvalOptions(
	string Folder, string Url, string Model, int Runs, int? Threads, string? Only,
	int NumCtx, int MaxAttempts, double RetryTemperature, decimal Tolerance)
{
	public const string Usage = """
		Usage: dotnet run --project Backend.Eval -- <folder> [options]

		  --url <address>   Ollama base address (default http://localhost:11434)
		  --model <tag>     model to ask for (default qwen3.5:4b)
		  --runs <n>        runs per sample (default 3)
		  --threads <n>     num_thread to send, for a container limited to n cores
		  --only <text>     only samples whose name contains the text
		""";

	public static EvalOptions? Parse(string[] args)
	{
		if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
			return null;

		string url = "http://localhost:11434", model = "qwen3.5:4b";
		int runs = 3;
		int? threads = null;
		string? only = null;

		for (int i = 1; i < args.Length; i += 2)
		{
			if (i + 1 >= args.Length)
				return null;

			string value = args[i + 1];

			switch (args[i])
			{
				case "--url": url = value; break;
				case "--model": model = value; break;
				case "--only": only = value; break;
				case "--runs" when int.TryParse(value, out int n) && n > 0: runs = n; break;
				case "--threads" when int.TryParse(value, out int t) && t > 0: threads = t; break;
				default: return null;
			}
		}

		if (!Directory.Exists(args[0]))
		{
			Console.Error.WriteLine($"{args[0]} is not a folder.");
			return null;
		}

		// The production defaults of PipelineSettings, which the tool cannot read from a database.
		return new EvalOptions(args[0], url.TrimEnd('/') + "/", model, runs, threads, only,
			NumCtx: 8192, MaxAttempts: 3, RetryTemperature: 0.3, Tolerance: 0.01m);
	}
}
