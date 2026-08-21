using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Backend.Features.Persistence;

/// <summary>
/// Builds a context for the <c>dotnet ef</c> tooling without starting the application. EF prefers this
/// over the web host whenever it is present, which matters for two reasons: the host now migrates the
/// database before it finishes starting, so scaffolding a migration would apply one as a side effect;
/// and <see cref="DatabaseOptions.ConnectionString"/> has no default, so building the host to read it
/// would fail validation before the tooling got that far.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<KvitteriaDbContext>
{
	/// <summary>
	/// Parseable, and deliberately unreachable. <c>migrations add</c> only needs the provider to
	/// resolve; anything that talks to a server — <c>database update</c>, <c>has-pending-model-changes</c>
	/// against a real database — needs <c>Database__ConnectionString</c> in the environment.
	/// </summary>
	private const string Placeholder = "Host=localhost;Database=kvitteria;Username=postgres";

	public KvitteriaDbContext CreateDbContext(string[] args)
	{
		string connectionString =
			Environment.GetEnvironmentVariable("Database__ConnectionString") ?? Placeholder;

		DbContextOptionsBuilder<KvitteriaDbContext> options = new();
		options.UseNpgsql(connectionString);

		return new KvitteriaDbContext(options.Options);
	}
}
