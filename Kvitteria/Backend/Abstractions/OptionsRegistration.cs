namespace Backend.Abstractions;

public static class OptionsRegistration
{
	extension(IServiceCollection services)
	{
		/// <summary>
		/// Binds <typeparamref name="T"/> to the section it names, validating its data annotations at
		/// startup rather than on first use.
		/// </summary>
		/// <remarks>
		/// Replaces three hand-written copies of the same bind call. The <c>ValidateOnStart</c> is the
		/// real gain: misconfiguration used to surface as an exception thrown deep inside the first
		/// mail poll, long after the app reported itself healthy.
		/// </remarks>
		public IServiceCollection AddOptionsSection<T>(IConfiguration configuration)
			where T : class, IOptionsSection
			=> services.AddOptions<T>()
			           .Bind(configuration.GetSection(T.SectionName))
			           .ValidateDataAnnotations()
			           .ValidateOnStart()
			           .Services;
	}
}
