using Microsoft.Extensions.Caching.Memory;
namespace Backend.Features.Auth;

/// <summary>
/// Caches resolved members so the common case — a read by a known user — costs no queries. Entries are
/// dropped wholesale by bumping a version that is part of every key, which is cheaper than tracking
/// which users a settings change affected (the answer is: all of them). The short expiry is only a
/// backstop for changes made outside the API, such as directly in the database.
/// </summary>
public sealed class PermissionCache(IMemoryCache cache)
{
	private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

	private int version;

	public bool TryGet(Guid authUserId, out CurrentUser? user) =>
		cache.TryGetValue(Key(authUserId), out user);

	public void Set(CurrentUser user) =>
		cache.Set(Key(user.AuthUserId), user, Lifetime);

	/// <summary>Called after any change to a member or to the household's member grants.</summary>
	public void Invalidate() => Interlocked.Increment(ref version);

	private string Key(Guid authUserId) => $"user:{Volatile.Read(ref version)}:{authUserId}";
}
