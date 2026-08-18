using Backend.Features.Extraction;
namespace Backend.Tests;

public class ReconciliationTests
{
	private const decimal Tolerance = 0.01M;

	[Theory]
	// grandTotal, lineSum, pantTotal, expectedReconciled
	[InlineData(123.45, 120.45, 3.00, true)]   // exact
	[InlineData(123.46, 120.45, 3.00, true)]   // 1 øre over  - rounding, not an extraction error
	[InlineData(123.44, 120.45, 3.00, true)]   // 1 øre under
	[InlineData(123.47, 120.45, 3.00, false)]  // 2 øre over  - a real discrepancy
	[InlineData(133.45, 120.45, 3.00, false)]  // a missed product
	public void Reconciles_within_one_ore_of_tolerance(
		double grandTotal, double lineSum, double pantTotal, bool expected)
	{
		decimal delta = Reconciliation.Delta((decimal)grandTotal, (decimal)lineSum, (decimal)pantTotal);

		Assert.Equal(expected, Reconciliation.IsReconciled(delta, Tolerance));
	}

	[Fact]
	public void Delta_is_signed_so_the_best_attempt_can_be_compared()
	{
		Assert.True(Reconciliation.Delta(100M, 90M, 5M) > 0);
		Assert.True(Reconciliation.Delta(100M, 110M, 5M) < 0);
	}

	[Fact]
	public void Zero_tolerance_still_accepts_an_exact_match()
	{
		Assert.True(Reconciliation.IsReconciled(0M, 0M));
		Assert.False(Reconciliation.IsReconciled(0.01M, 0M));
	}
}
