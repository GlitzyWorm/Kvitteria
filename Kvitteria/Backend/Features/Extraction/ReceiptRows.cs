using System.Globalization;
using System.Text.RegularExpressions;
using Backend.Features.Extraction.LLM;
namespace Backend.Features.Extraction;

/// <summary>What the model writes: every printed line that carries an amount, labelled, plus the amount paid.</summary>
public sealed record ReceiptRowsAnswer(string Merchant, string Currency, IReadOnlyList<ReceiptRow> Rows, string GrandTotal);

/// <summary>One printed line. <c>Amount</c> is the amount as printed (<c>14,95-</c>), <c>Kind</c> one of <see cref="ReceiptRows.Kinds"/>.</summary>
public sealed record ReceiptRow(string Text, string Amount, decimal Quantity, string Kind);

/// <summary>
/// Turns the model's labelled rows into the <see cref="ExtractedReceipt"/> the rest of the pipeline knows.
/// The model labels; what counts as a line, the signs, the sums, the date and the chain are decided here,
/// where they are tested. Anything unusable throws <see cref="OllamaResponseException"/>, which the retry
/// loop already treats as an attempt spent.
/// </summary>
public static partial class ReceiptRows
{
	public const string Product = "product";
	public const string Discount = "discount";
	public const string Deposit = "deposit";
	public const string DepositTotal = "deposit_total";

	public static readonly IReadOnlyList<string> Kinds =
		[Product, Discount, Deposit, DepositTotal, "total", "payment", "vat", "loyalty", "other"];

	private const string Unit = "stk";

	public static ExtractedReceipt ToExtracted(ReceiptRowsAnswer answer, string text)
	{
		// The schema marks these required, but an Ollama that ignores it can still leave them out.
		if (answer.Rows is null || answer.GrandTotal is null || answer.Merchant is null)
			throw new OllamaResponseException("The answer is missing its rows, grand total or merchant.");

		List<ExtractedLineItem> lines = [];
		decimal depositSum = 0;
		decimal? depositTotal = null;
		bool anyDeposit = false;

		foreach (ReceiptRow row in answer.Rows)
		{
			if (row?.Text is null || row.Amount is null || row.Kind is null)
				throw new OllamaResponseException("A row is missing its text, amount or kind.");

			if (!Kinds.Contains(row.Kind, StringComparer.Ordinal))
				throw new OllamaResponseException($"Row '{row.Text}' has the unknown kind '{row.Kind}'.");

			if (!DanishAmount.TryParse(row.Amount, out decimal amount))
				throw new OllamaResponseException($"Row '{row.Text}' has an amount that is not a printed amount: '{row.Amount}'.");

			switch (row.Kind)
			{
				case Product or Discount:
					if (QuantityOnly().IsMatch(row.Text))
						throw new OllamaResponseException($"Row '{row.Text}' is a quantity line with its own amount; it belongs to the product above it.");

					decimal total = row.Kind == Discount ? -Math.Abs(amount) : amount;
					decimal quantity = row.Quantity > 0 ? row.Quantity : 1;
					lines.Add(new ExtractedLineItem(row.Text.Trim(), quantity, Unit, Math.Round(total / quantity, 2), total));
					break;

				case Deposit:
					anyDeposit = true;
					depositSum += amount;
					break;

				case DepositTotal:
					depositTotal ??= amount;
					break;
			}
		}

		if (!DanishAmount.TryParse(answer.GrandTotal, out decimal grandTotal))
			throw new OllamaResponseException($"The grand total is not a printed amount: '{answer.GrandTotal}'.");

		// PANT lines and a HERAF PANT IALT summary can both be printed; the lines win so it is counted once.
		decimal pant = anyDeposit ? depositSum : depositTotal ?? 0;

		DateTime? printed = PrintedDate.Find(text);
		string purchaseDate = printed is { } date ? date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "";
		string merchant = MerchantChains.Match(text) ?? answer.Merchant.Trim();
		string currency = string.IsNullOrWhiteSpace(answer.Currency) ? "DKK" : answer.Currency.Trim();

		return new ExtractedReceipt(merchant, purchaseDate, currency, Math.Abs(grandTotal), pant, lines);
	}

	/// <summary>A row that is only <c>2 x 12,00</c> or <c>3 stk</c>: a quantity, not a product.</summary>
	[GeneratedRegex(@"^\s*\d+(?:[.,]\d+)?\s*(?:x\s*\d+(?:[.,]\d+)?\s*(?:kr\.?)?|stk\.?|kg|g|l|ml)\s*$",
		RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
	private static partial Regex QuantityOnly();
}
