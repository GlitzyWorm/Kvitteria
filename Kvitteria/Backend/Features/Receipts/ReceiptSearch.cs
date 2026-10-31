using System.Globalization;
using System.Text.RegularExpressions;
namespace Backend.Features.Receipts;

/// <summary>
/// A parsed search term. <see cref="Pattern"/> is an <c>ILIKE</c> pattern with the user's own wildcard
/// characters escaped. When the term is also a number, receipts whose grand total is at least
/// <see cref="AmountFrom"/> and below <see cref="AmountTo"/> match too.
/// </summary>
public sealed record ReceiptSearchTerm(string Pattern, decimal? AmountFrom, decimal? AmountTo);

public static partial class ReceiptSearch
{
	public const int MaxLength = 100;

	/// <summary>
	/// Passed to every <c>ILike</c> call. Without an explicit escape character EF emits <c>ESCAPE ''</c>,
	/// which turns escaping off, so the escapes <see cref="TryParse"/> adds would be matched literally.
	/// </summary>
	public const string EscapeChar = @"\";

	/// <summary>The smallest step a grand total can take: Danish receipts round to øre.</summary>
	private const decimal Ore = 0.01m;

	[GeneratedRegex(@"^\d+([.,]\d+)?$")]
	private static partial Regex NumberPattern();

	/// <summary>
	/// A missing, empty or whitespace-only term succeeds with a null <paramref name="term"/>, meaning no
	/// filtering. Only a term over <see cref="MaxLength"/> characters is rejected.
	/// </summary>
	public static bool TryParse(string? search, out ReceiptSearchTerm? term, out IResult problem)
	{
		term = null;
		problem = Results.Empty;

		string trimmed = search?.Trim() ?? "";

		if (trimmed.Length == 0)
			return true;

		if (trimmed.Length > MaxLength)
		{
			problem = Results.Problem(
				statusCode: StatusCodes.Status400BadRequest,
				detail: $"search must be at most {MaxLength} characters.");
			return false;
		}

		string pattern = "%" + Escape(trimmed) + "%";
		decimal? from = null;
		decimal? to = null;

		if (NumberPattern().IsMatch(trimmed)
		    && decimal.TryParse(trimmed.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount))
		{
			bool whole = !trimmed.Contains(',') && !trimmed.Contains('.');

			from = amount;
			to = amount + (whole ? 1m : Ore);
		}

		term = new ReceiptSearchTerm(pattern, from, to);
		return true;
	}

	/// <summary>Makes <c>\</c>, <c>%</c> and <c>_</c> literal, <c>\</c> being Postgres's default escape.</summary>
	private static string Escape(string value) =>
		value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
