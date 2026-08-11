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
	public static ReceiptStatus FromIntToReceiptStatus(int status)
	{
		return status switch {
			0 => ReceiptStatus.Queued,
			1 => ReceiptStatus.Extracting,
			2 => ReceiptStatus.Extracted,
			3 => ReceiptStatus.Completed,
			4 => ReceiptStatus.Failed,
			_ => throw new ArgumentOutOfRangeException(nameof(status), $"Invalid status value: {status}")
		};
	}
}