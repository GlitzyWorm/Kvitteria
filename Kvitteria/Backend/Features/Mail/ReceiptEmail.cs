namespace Backend.Features.Mail;

public sealed record ReceiptEmail(
	string? MessageId,
	string From,
	string Subject,
	DateTimeOffset Date,
	string BodyText,
	string BodySource, // "text" | "html" | "none"
	string MemberEmail);