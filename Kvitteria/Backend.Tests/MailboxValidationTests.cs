using Backend.Features.Shared;
using Kvitteria.Contracts.DTO;

namespace Backend.Tests;

public class MailboxValidationTests
{
	private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");

	private static CreateMailboxDto ValidCreate() => new(
		AppUserId: Owner,
		Host: "imap.example.com",
		Port: 993,
		UseSsl: true,
		Username: "member@example.com",
		Password: "correct horse battery staple",
		Folder: "INBOX.Netto",
		IsEnabled: true);

	private static PutMailboxDto ValidPut() => new(
		AppUserId: Owner,
		Host: "imap.example.com",
		Port: 993,
		UseSsl: true,
		Username: "member@example.com",
		Password: null,
		Folder: "INBOX.Netto",
		IsEnabled: true);

	[Fact]
	public void Accepts_a_complete_create() =>
		Assert.True(MailboxValidation.TryValidate(ValidCreate(), out _));

	[Fact]
	public void Accepts_an_update_that_omits_the_password() =>
		Assert.True(MailboxValidation.TryValidate(ValidPut(), out _));

	[Fact]
	public void Accepts_an_update_that_replaces_the_password() =>
		Assert.True(MailboxValidation.TryValidate(ValidPut() with { Password = "a new one" }, out _));

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Rejects_blank_host_username_or_folder(string blank)
	{
		Assert.False(MailboxValidation.TryValidate(ValidCreate() with { Host = blank }, out _));
		Assert.False(MailboxValidation.TryValidate(ValidCreate() with { Username = blank }, out _));
		Assert.False(MailboxValidation.TryValidate(ValidCreate() with { Folder = blank }, out _));

		Assert.False(MailboxValidation.TryValidate(ValidPut() with { Host = blank }, out _));
		Assert.False(MailboxValidation.TryValidate(ValidPut() with { Username = blank }, out _));
		Assert.False(MailboxValidation.TryValidate(ValidPut() with { Folder = blank }, out _));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(65_536)]
	public void Rejects_port_out_of_range(int port)
	{
		Assert.False(MailboxValidation.TryValidate(ValidCreate() with { Port = port }, out _));
		Assert.False(MailboxValidation.TryValidate(ValidPut() with { Port = port }, out _));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(143)]
	[InlineData(65_535)]
	public void Accepts_port_at_and_inside_the_bounds(int port)
	{
		Assert.True(MailboxValidation.TryValidate(ValidCreate() with { Port = port }, out _));
		Assert.True(MailboxValidation.TryValidate(ValidPut() with { Port = port }, out _));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public void Rejects_a_create_without_a_password(string? password)
	{
		// Nothing is stored yet, so there is no password to fall back to.
		Assert.False(MailboxValidation.TryValidate(ValidCreate() with { Password = password! }, out _));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Rejects_an_update_whose_password_is_present_but_blank(string blank)
	{
		// Distinct from null: a blank field would otherwise wipe a working password.
		Assert.False(MailboxValidation.TryValidate(ValidPut() with { Password = blank }, out _));
	}

	[Fact]
	public void Rejects_a_mailbox_with_no_owner()
	{
		Assert.False(MailboxValidation.TryValidate(ValidCreate() with { AppUserId = Guid.Empty }, out _));
		Assert.False(MailboxValidation.TryValidate(ValidPut() with { AppUserId = Guid.Empty }, out _));
	}
}
