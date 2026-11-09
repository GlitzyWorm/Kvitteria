using System.Linq.Expressions;
using Backend.Features.Extraction;
using Backend.Features.Persistence;
using Backend.Features.Persistence.Model.Receipts;
using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Receipts;

/// <summary>Whether a stored receipt adds up, and whether a member has accepted that it doesn't.</summary>
public enum ReconciliationState
{
	/// <summary>The difference is within the tolerance.</summary>
	Reconciled,

	/// <summary>The difference is outside the tolerance, and equals the one a member accepted.</summary>
	Accepted,

	/// <summary>The difference is outside the tolerance and nobody has accepted it. Needs attention.</summary>
	Unreconciled
}

/// <summary>
/// The state is derived from the stored figures on every read. <see cref="Classify"/> and
/// <see cref="NeedsAttention"/> are the same rule in two forms, so they stay in this file: the first is what
/// the tests cover, the second is what the list filter and the count run in the database.
/// </summary>
public static class ReceiptReconciliation
{
	public static ReconciliationState Classify(decimal delta, decimal tolerance, decimal? acceptedDifference)
	{
		if (Reconciliation.IsReconciled(delta, tolerance))
			return ReconciliationState.Reconciled;

		return acceptedDifference == delta ? ReconciliationState.Accepted : ReconciliationState.Unreconciled;
	}

	/// <summary>The tolerance in pipeline settings, read once per request.</summary>
	public static async Task<decimal> ReadToleranceAsync(KvitteriaDbContext dbContext, CancellationToken ct) =>
		await dbContext.PipelineSettings
		               .AsNoTracking()
		               .Select(s => (decimal?)s.OllamaReconciliationToleranceKr)
		               .FirstOrDefaultAsync(ct) ?? 0.01M;

	public static decimal Delta(Receipt receipt) =>
		Reconciliation.Delta(receipt.GrandTotal, receipt.ReceiptLines.Sum(l => l.LineTotal), receipt.PantTotal);

	/// <summary>
	/// True where <see cref="Classify"/> says Unreconciled: <c>|delta| &gt; |tolerance|</c> and no acceptance
	/// equal to the delta. Keep the two in step.
	/// </summary>
	public static Expression<Func<Receipt, bool>> NeedsAttention(decimal tolerance)
	{
		decimal limit = Math.Abs(tolerance);

		return r => (r.GrandTotal - r.ReceiptLines.Sum(l => l.LineTotal) - r.PantTotal > limit ||
		             r.GrandTotal - r.ReceiptLines.Sum(l => l.LineTotal) - r.PantTotal < -limit) &&
		            (r.AcceptedDifference == null ||
		             r.AcceptedDifference != r.GrandTotal - r.ReceiptLines.Sum(l => l.LineTotal) - r.PantTotal);
	}
}
