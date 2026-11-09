using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Receipts;
namespace Backend.Tests;

public class ReceiptReconciliationTests
{
	private const decimal Tolerance = 0.01M;

	[Theory]
	// delta, tolerance, accepted, expected
	[InlineData(0.00, 0.01, null, ReconciliationState.Reconciled)]
	[InlineData(0.01, 0.01, null, ReconciliationState.Reconciled)]    // on the boundary
	[InlineData(0.02, 0.01, null, ReconciliationState.Unreconciled)]  // just outside
	[InlineData(-0.02, 0.01, null, ReconciliationState.Unreconciled)] // negative delta
	[InlineData(-0.01, 0.01, null, ReconciliationState.Reconciled)]
	[InlineData(5.00, 0.01, 5.00, ReconciliationState.Accepted)]      // accepted equal
	[InlineData(5.00, 0.01, 4.00, ReconciliationState.Unreconciled)]  // accepted lapsed
	[InlineData(0.01, 0.01, 0.01, ReconciliationState.Reconciled)]    // within tolerance: reconciled wins
	[InlineData(-5.00, 0.01, 5.00, ReconciliationState.Unreconciled)] // sign matters
	[InlineData(0.02, -0.01, null, ReconciliationState.Unreconciled)] // negative tolerance setting
	[InlineData(0.01, -0.01, null, ReconciliationState.Reconciled)]
	public void Classifies(double delta, double tolerance, double? accepted, ReconciliationState expected)
	{
		Assert.Equal(expected, ReceiptReconciliation.Classify((decimal)delta, (decimal)tolerance, (decimal?)accepted));
	}

	[Theory]
	[InlineData(100.00, 90.00, 5.00, null)]
	[InlineData(100.00, 90.00, 10.00, null)]
	[InlineData(100.00, 90.00, 4.99, null)]
	[InlineData(100.00, 90.00, 4.99, 5.01)]
	[InlineData(100.00, 90.00, 5.00, 5.00)]
	[InlineData(100.00, 110.00, 5.00, -15.00)]
	[InlineData(100.00, 110.00, 5.00, 15.00)]
	public void The_database_predicate_agrees_with_the_classifier(double grand, double lines, double pant, double? accepted)
	{
		var receipt = new Receipt {
			GrandTotal = (decimal)grand,
			PantTotal = (decimal)pant,
			AcceptedDifference = (decimal?)accepted,
			ReceiptLines = [
				new ReceiptLine { RawName = "a", LineTotal = (decimal)lines / 2 },
				new ReceiptLine { RawName = "b", LineTotal = (decimal)lines / 2 }
			]
		};

		bool needsAttention = ReceiptReconciliation.NeedsAttention(Tolerance).Compile()(receipt);
		ReconciliationState state = ReceiptReconciliation.Classify(ReceiptReconciliation.Delta(receipt), Tolerance, receipt.AcceptedDifference);

		Assert.Equal(state == ReconciliationState.Unreconciled, needsAttention);
	}
}
