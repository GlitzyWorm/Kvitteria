using System.Text;
using Backend.Features.Extraction.LLM;
namespace Backend.Tests;

public class OllamaThreadsTests
{
	/// <summary>One cpuinfo block per logical CPU, as an x86 kernel prints them.</summary>
	private static string CpuInfo(int packages, int coresPerPackage, int threadsPerCore)
	{
		var text = new StringBuilder();
		int processor = 0;

		for (int package = 0; package < packages; package++)
		{
			for (int core = 0; core < coresPerPackage; core++)
			{
				for (int thread = 0; thread < threadsPerCore; thread++)
				{
					text.Append($"processor\t: {processor++}\nmodel name\t: Test CPU @ 3.00GHz\n");
					text.Append($"physical id\t: {package}\nsiblings\t: {coresPerPackage*threadsPerCore}\n");
					text.Append($"core id\t\t: {core}\ncpu cores\t: {coresPerPackage}\n\n");
				}
			}
		}

		return text.ToString();
	}

	[Fact]
	public void Limits_to_the_cpuset_when_cpuinfo_lists_more_of_the_machine()
	{
		// The home server: a 6-core i5-9500 with a Proxmox LXC given 4 of them.
		Assert.Equal(4, OllamaThreads.Limit(4, CpuInfo(1, 6, 1)));
	}

	[Theory]
	[InlineData(6, 1, 6, 1)]   // every core available
	[InlineData(8, 1, 4, 2)]   // hyper-threading: 8 logical on 4 physical is not a limit
	[InlineData(4, 1, 4, 2)]   // exactly the physical cores
	public void Leaves_the_choice_to_Ollama_when_nothing_is_limited(
		int availableCpus, int packages, int coresPerPackage, int threadsPerCore)
	{
		Assert.Null(OllamaThreads.Limit(availableCpus, CpuInfo(packages, coresPerPackage, threadsPerCore)));
	}

	[Fact]
	public void Core_ids_repeat_across_packages_without_being_the_same_core()
	{
		Assert.Equal(6, OllamaThreads.Limit(6, CpuInfo(2, 4, 1)));
	}

	[Fact]
	public void Counts_processors_when_the_kernel_prints_no_core_ids()
	{
		const string arm = "processor\t: 0\nBogoMIPS\t: 108.00\n\nprocessor\t: 1\nBogoMIPS\t: 108.00\n\nprocessor\t: 2\nBogoMIPS\t: 108.00\n\n";

		Assert.Equal(2, OllamaThreads.Limit(2, arm));
		Assert.Null(OllamaThreads.Limit(3, arm));
	}

	[Fact]
	public void Unreadable_cpuinfo_limits_nothing()
	{
		Assert.Null(OllamaThreads.Limit(4, string.Empty));
	}
}
