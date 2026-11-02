namespace Backend.Features.Extraction.LLM;

/// <summary>
/// Ollama starts one thread per core listed in <c>/proc/cpuinfo</c>, which inside a container lists the
/// whole machine even when a cpuset allows only some of it, as in a Proxmox LXC. llama.cpp's threads spin
/// while they wait for each other, so six of them on four CPUs generated at a twentieth of the speed of
/// four. Ollama has no setting for this and the backend shares its machine, so the limit is sent per
/// request, and only when there is one: anywhere else the request stays as it was and Ollama's own
/// choice stands.
/// </summary>
public static class OllamaThreads
{
	private const string CpuInfoPath = "/proc/cpuinfo";

	private static readonly Lazy<int?> CurrentLimit = new Lazy<int?>(() =>
	{
		try
		{
			return Limit(Environment.ProcessorCount, File.ReadAllText(CpuInfoPath));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// Not Linux, which means a host-side run with nothing to correct.
			return null;
		}
	});

	/// <summary>The limit for this machine, or null when Ollama should choose for itself.</summary>
	public static int? Current => CurrentLimit.Value;

	/// <param name="availableCpus">CPUs this process may run on, which unlike cpuinfo honours the cpuset.</param>
	/// <param name="cpuInfo">The text of <c>/proc/cpuinfo</c>.</param>
	public static int? Limit(int availableCpus, string cpuInfo) =>
		availableCpus < CountCores(cpuInfo) ? availableCpus : null;

	/// <summary>Physical cores, so a host with hyper-threading isn't taken for one with a limit.</summary>
	private static int CountCores(string cpuInfo)
	{
		int processors = 0;
		string? package = null;
		HashSet<(string? Package, string Core)> cores = [];

		foreach (string line in cpuInfo.Split('\n'))
		{
			int colon = line.IndexOf(':');
			if (colon < 0)
			{
				continue;
			}

			string value = line[(colon+1)..].Trim();
			switch (line[..colon].Trim())
			{
				case "processor":
					processors++;
					break;
				case "physical id":
					package = value;
					break;
				case "core id":
					cores.Add((package, value));
					break;
			}
		}

		// Arm kernels print neither id.
		return cores.Count > 0 ? cores.Count : processors;
	}
}
