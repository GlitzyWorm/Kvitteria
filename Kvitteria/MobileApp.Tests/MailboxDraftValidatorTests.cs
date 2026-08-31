// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using MobileApp.Features.Shared;

namespace MobileApp.Tests;

/// <summary>
/// The client half of a rule the server also enforces. These pin the two halves together: the server's
/// MailboxValidation.TryValidate checks host, username, folder and a 1–65535 port, and a drift here
/// would show up as a round trip that always fails, or one that never gets made.
/// </summary>
public class MailboxDraftValidatorTests
{
	/// <summary>A complete draft for a new mailbox, which each test then breaks in one place.</summary>
	private static MailboxDraft Valid() => new()
	{
		OwnerId = Guid.NewGuid(),
		Host = "imap.gmail.com",
		Port = "993",
		Folder = "INBOX/Kvitteringer",
		Username = "nogen@eksempel.dk",
		Password = "hemmeligt"
	};

	[Fact]
	public void Accepts_a_complete_draft()
	{
		Assert.True(MailboxDraftValidator.TryValidate(Valid(), out string? error));
		Assert.Null(error);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public void Rejects_a_missing_host(string? host)
	{
		MailboxDraft draft = Valid() with { Host = host! };

		Assert.False(MailboxDraftValidator.TryValidate(draft, out string? error));
		Assert.Equal("MailboxErrorRequiredFields", error);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Rejects_a_missing_folder(string folder)
	{
		MailboxDraft draft = Valid() with { Folder = folder };

		Assert.False(MailboxDraftValidator.TryValidate(draft, out string? error));
		Assert.Equal("MailboxErrorRequiredFields", error);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Rejects_a_missing_username(string username)
	{
		MailboxDraft draft = Valid() with { Username = username };

		Assert.False(MailboxDraftValidator.TryValidate(draft, out string? error));
		Assert.Equal("MailboxErrorRequiredFields", error);
	}

	[Theory]
	[InlineData("1")]
	[InlineData("993")]
	[InlineData("65535")]
	public void Accepts_a_port_in_range(string port)
	{
		Assert.True(MailboxDraftValidator.TryValidate(Valid() with { Port = port }, out _));
	}

	[Theory]
	[InlineData("0")]
	[InlineData("-1")]
	[InlineData("65536")]
	[InlineData("")]
	[InlineData("abc")]
	[InlineData("99 3")]
	[InlineData("9.93")]
	public void Rejects_a_port_out_of_range_or_unparseable(string port)
	{
		MailboxDraft draft = Valid() with { Port = port };

		Assert.False(MailboxDraftValidator.TryValidate(draft, out string? error));
		Assert.Equal("MailboxErrorPort", error);
	}

	/// <summary>
	/// A new mailbox has no stored password to fall back on, so an empty one is a mistake. This is the
	/// one rule that differs between creating and editing.
	/// </summary>
	[Fact]
	public void Rejects_an_empty_password_when_creating()
	{
		MailboxDraft draft = Valid() with { Password = "" };

		Assert.False(MailboxDraftValidator.TryValidate(draft, out string? error));
		Assert.Equal("MailboxErrorPasswordRequired", error);
	}

	/// <summary>
	/// Editing, an empty password means "keep the stored one" — the server treats a null Password that
	/// way, and the plaintext was never sent to the app to prefill.
	/// </summary>
	[Fact]
	public void Accepts_an_empty_password_when_editing()
	{
		MailboxDraft draft = Valid() with { Id = Guid.NewGuid(), Password = "" };

		Assert.True(MailboxDraftValidator.TryValidate(draft, out string? error));
		Assert.Null(error);
	}

	/// <summary>Completeness is checked before the port, matching the server's own order.</summary>
	[Fact]
	public void Reports_missing_fields_before_a_bad_port()
	{
		MailboxDraft draft = Valid() with { Host = "", Port = "notanumber" };

		Assert.False(MailboxDraftValidator.TryValidate(draft, out string? error));
		Assert.Equal("MailboxErrorRequiredFields", error);
	}

	[Fact]
	public void Parses_the_port_invariantly()
	{
		Assert.Equal(993, MailboxDraftValidator.ParsePort(Valid()));
	}

	/// <summary>A draft with no id is a create; one with an id is an edit. Nothing else decides it.</summary>
	[Fact]
	public void IsEdit_follows_the_id()
	{
		Assert.False(Valid().IsEdit);
		Assert.True((Valid() with { Id = Guid.NewGuid() }).IsEdit);
	}
}
