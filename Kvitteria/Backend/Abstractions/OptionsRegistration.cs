namespace Backend.Abstractions;

public static class OptionsRegistration
{
	extension(IServiceCollection services)
	{
		/// <summary>
		/// Binds <typeparamref name="T"/> to the section it names, validating its data annotations at
		/// startup rather than on first use.
		/// </summary>
		public IServiceCollection AddOptionsSection<T>(IConfiguration configuration)
			where T : class, IOptionsSection
			=> services.AddOptions<T>()
			           .Bind(configuration.GetSection(T.SectionName))
			           .ValidateDataAnnotations()
			           .ValidateOnStart()
			           .Services;
	}
}
