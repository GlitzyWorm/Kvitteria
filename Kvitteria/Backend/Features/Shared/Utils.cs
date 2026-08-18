namespace Backend.Features.Shared;

public static class Utils
{
	extension(string rawName)
	{
		public string NormalizeString() => rawName.Trim().ToLowerInvariant();
	}
}