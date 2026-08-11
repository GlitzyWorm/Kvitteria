namespace Backend.Features.Persistence.Model.Receipts;

public class Merchant
{
	public Guid Id { get; set; }
	public required string Name { get; set; }
}