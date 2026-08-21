using Microsoft.EntityFrameworkCore;
namespace Backend.Features.Persistence;

public static class DatabaseMigrator
{
	/// <summary>How long to keep waiting for a database that is still accepting no connections.</summary>
	private static readonly TimeSpan ConnectAttemptDelay = TimeSpan.FromSeconds(2);

	private const int ConnectAttempts = 15;

	extension(WebApplication app)
	{
		/// <summary>
		/// Brings the schema up to date, and seeds it through the <c>UseAsyncSeeding</c> callback that
		/// <see cref="RelationalDatabaseFacadeExtensions.MigrateAsync"/> fires.
		///
		/// Called before the host starts rather than from an <see cref="IHostedService"/>, because
		/// <see cref="Mail.MailWorker"/> reads <see cref="Model.PipelineSettings"/> as its very first act
		/// and would throw against an unmigrated database. Hosted services do start in registration
		/// order, so a migrator registered above it would in fact win — but that ordering is invisible,
		/// and one reordered line would quietly restore the race.
		/// </summary>
		public async Task MigrateDatabaseAsync(CancellationToken ct = default)
		{
			await using AsyncServiceScope scope = app.Services.CreateAsyncScope();

			ILogger logger = scope.ServiceProvider
			                      .GetRequiredService<ILoggerFactory>()
			                      .CreateLogger(typeof(DatabaseMigrator));

			KvitteriaDbContext dbContext = scope.ServiceProvider.GetRequiredService<KvitteriaDbContext>();

			await WaitForDatabaseAsync(dbContext, logger, ct);

			logger.LogInformation("Applying database migrations.");
			await dbContext.Database.MigrateAsync(ct);
			logger.LogInformation("Database is up to date.");
		}
	}

	/// <summary>
	/// Absorbs the gap between Postgres accepting TCP and Postgres being ready to answer, which a
	/// container healthcheck narrows but does not close. Deliberately a loop around the connection
	/// attempt rather than <c>EnableRetryOnFailure</c>: an execution strategy throws on user-initiated
	/// transactions, and this codebase opens three of them.
	/// </summary>
	private static async Task WaitForDatabaseAsync(KvitteriaDbContext dbContext, ILogger logger, CancellationToken ct)
	{
		for (int attempt = 1; attempt <= ConnectAttempts; attempt++)
		{
			if (await dbContext.Database.CanConnectAsync(ct))
				return;

			logger.LogWarning(
				"Database is not accepting connections yet (attempt {Attempt} of {Attempts}).",
				attempt, ConnectAttempts);

			await Task.Delay(ConnectAttemptDelay, ct);
		}

		// Fall through rather than throwing here: MigrateAsync raises the provider's own exception,
		// which names the host and the reason far better than anything this method could invent.
	}
}
