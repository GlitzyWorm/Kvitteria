using Backend.Features.Persistence.Model.Receipts;
using Backend.Features.Shared;
namespace Backend.Features.Receipts;

/// <summary>How a remembered alias reaches lines that were not part of the save that taught it.</summary>
public static class AliasLearning
{
	/// <summary>
	/// The lines whose printed name normalizes to <paramref name="normalizedRawName"/>. Normalizes in C#
	/// rather than SQL so it cannot disagree with the normalization that wrote the alias.
	/// </summary>
	public static List<ReceiptLine> Matching(IEnumerable<ReceiptLine> lines, string normalizedRawName) =>
		lines.Where(l => l.RawName.NormalizeString() == normalizedRawName).ToList();

	/// <summary>
	/// Sets the category and meal only where the line has none, so an automatic write never replaces
	/// a person's choice. A null argument leaves the field alone.
	/// </summary>
	public static void FillGaps(ReceiptLine line, Guid? categoryId, Guid? mealId)
	{
		if (line.CategoryId == null && categoryId != null)
			line.CategoryId = categoryId;

		if (line.MealId == null && mealId != null)
			line.MealId = mealId;
	}
}
