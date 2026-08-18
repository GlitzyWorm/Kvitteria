namespace Backend.Abstractions;

/// <summary>
/// An options class that knows which configuration section it binds to.
/// </summary>
/// <remarks>
/// Renamed from <c>IOptions</c>, which collided conceptually with
/// <c>Microsoft.Extensions.Options.IOptions&lt;T&gt;</c> and forced files to import both. The
/// <c>static abstract</c> member is now load-bearing: it is the constraint
/// <c>AddOptionsSection&lt;T&gt;</c> uses, rather than a marker nothing reads.
/// </remarks>
public interface IOptionsSection
{
	static abstract string SectionName { get; }
}
