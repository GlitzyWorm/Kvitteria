namespace Backend.Features.Extraction;

public enum ExtractionStatus
{
	Queued,
	Extracting,
	Extracted,
	Completed,
	Failed
}