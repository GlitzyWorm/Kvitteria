namespace Kvitteria.Contracts.DTO;

/// <summary>An accepted upload: the intake it was recorded as, which the pipeline status and failures name.</summary>
public record ReceiptUploadDto(Guid IntakeId);
