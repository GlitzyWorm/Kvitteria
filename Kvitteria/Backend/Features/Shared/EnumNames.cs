namespace Backend.Features.Shared;

public static class EnumNames
{
	extension(string? candidate)
	{
		/// <summary>
		/// Parses an enum by name only. <see cref="Enum.TryParse{T}(string, bool, out T)"/> also accepts
		/// ordinals, which would both make the wire contract depend on the enum's declaration order and let
		/// an out-of-range number through as an undefined value.
		/// </summary>
		public bool TryParseName<T>(out T value) where T : struct, Enum
		{
			string trimmed = candidate?.Trim() ?? "";

			foreach (T known in Enum.GetValues<T>())
			{
				if (string.Equals(known.ToString(), trimmed, StringComparison.OrdinalIgnoreCase))
				{
					value = known;
					return true;
				}
			}

			value = default;
			return false;
		}
	}
}
