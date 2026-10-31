using System.Linq.Expressions;
using Backend.Features.Persistence.Model;
using Kvitteria.Contracts.DTO;

namespace Backend.Features.Endpoints.Extensions;

public static class ReceiptFailureDtoExtension
{
	/// <summary>
	/// A projection rather than a <c>ToDto()</c> method, so EF translates it and never reads
	/// <c>BodyText</c> off the row: the mail text is large and must not reach the client anyway.
	/// </summary>
	public static readonly Expression<Func<ReceiptIntake, ReceiptFailureDto>> Projection = i =>
		new ReceiptFailureDto(
			i.Id,
			i.Subject,
			i.From,
			i.MailDate,
			i.FailedAt ?? i.ReceivedAt,
			i.Error ?? "",
			i.Attempts,
			i.AppUserId,
			i.AppUser!.DisplayName,
			i.MailboxId);
}
