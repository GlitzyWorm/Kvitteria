using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
namespace Backend.Features.Mail;

/// <summary>
/// Encrypts and decrypts stored mailbox passwords. A named type rather than an injected
/// <see cref="IDataProtector"/> so the purpose string exists in exactly one place — change it and every
/// stored password becomes undecryptable.
/// </summary>
public sealed class MailboxSecrets
{
	private const string Purpose = "Kvitteria.Mailbox.Password";

	private readonly IDataProtector protector;

	public MailboxSecrets(IDataProtectionProvider provider) => protector = provider.CreateProtector(Purpose);

	public string Protect(string password) => protector.Protect(password);

	/// <summary>
	/// Throws <see cref="MailboxSecretException"/> when the ciphertext cannot be read back, which in
	/// practice means the key ring was lost or replaced. Callers turn that into "re-enter the password"
	/// rather than letting a raw <see cref="CryptographicException"/> surface as a 500.
	/// </summary>
	public string Unprotect(string encryptedPassword)
	{
		try
		{
			return protector.Unprotect(encryptedPassword);
		}
		catch (CryptographicException ex)
		{
			throw new MailboxSecretException(
				"The stored password could not be decrypted; the Data Protection key ring is missing or " +
				"has been replaced. Re-enter the mailbox password.", ex);
		}
	}
}

public sealed class MailboxSecretException(string message, Exception innerException)
	: Exception(message, innerException);
