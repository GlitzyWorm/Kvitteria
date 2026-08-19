namespace Backend.Abstractions;

/// <summary>An options class that knows which configuration section it binds to.</summary>
public interface IOptionsSection
{
	static abstract string SectionName { get; }
}
