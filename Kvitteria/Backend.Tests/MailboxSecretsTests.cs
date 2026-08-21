using Backend.Features.Mail;
using Microsoft.AspNetCore.DataProtection;
namespace Backend.Tests;

public class MailboxSecretsTests
{
	private static MailboxSecrets Secrets() => new(new EphemeralDataProtectionProvider());

	[Fact]
	public void Round_trips_a_password()
	{
		MailboxSecrets secrets = Secrets();
		string cipher = secrets.Protect("correct horse battery staple");

		Assert.NotEqual("correct horse battery staple", cipher);
		Assert.Equal("correct horse battery staple", secrets.Unprotect(cipher));
	}

	[Fact]
	public void Reports_a_lost_key_ring_as_its_own_exception()
	{
		// The endpoint and the poller both turn this into "re-enter the password"; a raw
		// CryptographicException would surface as a 500 that explains nothing.
		Assert.Throws<MailboxSecretException>(() => Secrets().Unprotect("not-ciphertext"));
	}

	[Fact]
	public void A_different_key_ring_cannot_read_the_ciphertext()
	{
		// Ephemeral providers each generate their own key, which is exactly the "the key ring was
		// replaced" case.
		string cipher = Secrets().Protect("hunter2");

		Assert.Throws<MailboxSecretException>(() => Secrets().Unprotect(cipher));
	}
}
