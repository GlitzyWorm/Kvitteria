using Microsoft.Extensions.Options;
namespace Backend.Features.Update;

public sealed class GitHubReleasesClient(HttpClient httpClient, IOptionsMonitor<UpdateOptions> options)
{
	public async Task<List<GitHubRelease>> GetReleasesAsync(CancellationToken ct) =>
		await httpClient.GetFromJsonAsync<List<GitHubRelease>>($"repos/{options.CurrentValue.Repository}/releases?per_page=30", ct) ?? [];
}
