using Backend.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Backend.Features.Shared;

public static class SaveChangesExtensions
{
	extension(KvitteriaDbContext dbContext)
	{
		/// <summary>
		/// Saves, reporting <c>false</c> instead of throwing when a unique index rejects the write.
		/// Every check-then-insert in this codebase races, so the database constraint — not the
		/// preceding existence query — is what actually guarantees uniqueness.
		/// </summary>
		public async Task<bool> TrySaveChangesAsync(CancellationToken ct)
		{
			try
			{
				await dbContext.SaveChangesAsync(ct);
				return true;
			}
			catch (DbUpdateException e)
				when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
			{
				return false;
			}
		}
	}
}
