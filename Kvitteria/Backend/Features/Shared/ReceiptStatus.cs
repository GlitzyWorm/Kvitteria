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
		if (status == null || !Enum.IsDefined(typeof(ReceiptStatus), status.Value))
		{
			throw new ArgumentOutOfRangeException(nameof(status), $"Invalid status value: {status}");
		}
		
		return (ReceiptStatus)status.Value;
	}
}