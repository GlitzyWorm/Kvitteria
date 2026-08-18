namespace Backend.Features.Persistence.Model.Receipts;

public sealed class Meal
{
	public Guid Id { get; set; }
	public required string DisplayName { get; set; }
	public int SortOrder { get; set; }
	public bool TracksDayCoverage { get; set; }
}
