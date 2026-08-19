namespace Backend.Features.Extraction;

/// <summary>
/// The rule that decides whether an extraction is trustworthy: the printed grand total must equal
/// the sum of the line totals plus the pant total.
/// </summary>
public static class Reconciliation
{
	public static decimal Delta(decimal grandTotal, decimal lineSum, decimal pantTotal) =>
		grandTotal-lineSum-pantTotal;

	/// <summary>
	/// A delta within <paramref name="toleranceKr"/> of zero counts as reconciled. Danish receipts
	/// round to øre.
	/// </summary>
	public static bool IsReconciled(decimal delta, decimal toleranceKr) =>
		Math.Abs(delta) <= Math.Abs(toleranceKr);
}
