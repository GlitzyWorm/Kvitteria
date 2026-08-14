namespace Backend.Features.Shared;

public enum ReceiptStatus
{
	Queued,
	Extracting,
	Extracted,
	Completed,
	Failed
}
public static class ReceiptStatusExtension
{
	public static ReceiptStatus FromIntToReceiptStatus(int? status)
	{
		if (status != null && Enum.IsDefined(typeof(ReceiptStatus), status.Value)) return (ReceiptStatus)status.Value;

		if (status != null) throw new ArgumentOutOfRangeException(nameof(status), $"Invalid status value: {status.Value}");
		else throw new ArgumentNullException(nameof(status), "Status value cannot be null.");
	}
}