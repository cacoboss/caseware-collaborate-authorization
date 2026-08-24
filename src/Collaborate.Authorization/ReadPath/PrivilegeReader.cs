using Collaborate.Authorization.Model;

namespace Collaborate.Authorization.ReadPath;

/// <summary>
/// Reads the privilege tree, cache first, and degrades along two independent axes:
/// whether the cache answered, and whether the source of truth is reachable.
///
/// With the database reachable, a present tree is served from the cache, an absent one is
/// loaded and cached, and an unavailable cache costs a load on every call. With the
/// database unreachable, a cached tree is still served and everything else fails closed.
///
/// That last distinction is the one worth stating: a cache outage with a healthy database
/// costs latency, not correctness. Fail-closed is scoped to the source of truth.
/// </summary>
public sealed class PrivilegeReader(IPrivilegeStore store, IPrivilegeCache cache)
{
    public async Task<PrivilegeLookup> ReadAsync(string subjectId, string workspaceId, CancellationToken ct)
    {
        PrivilegeTree? cached = null;
        try
        {
            cached = await cache.GetAsync(subjectId, workspaceId, ct);
        }
        catch (Exception)
        {
            // The cache is unavailable. Fall through to the source of truth: the answer is
            // still correct, only slower.
        }

        if (cached is not null)
            return new PrivilegeLookup(cached, DecisionSource.Cache, SourceOfTruthUnavailable: false);

        PrivilegeTree? loaded;
        try
        {
            loaded = await store.LoadAsync(subjectId, workspaceId, ct);
        }
        catch (Exception)
        {
            return new PrivilegeLookup(null, DecisionSource.Database, SourceOfTruthUnavailable: true);
        }

        if (loaded is not null)
        {
            try
            {
                await cache.SetAsync(loaded, ct);
            }
            catch (Exception)
            {
                // Populating the cache failed. The decision we are about to return is still
                // correct; the next request pays the same cost.
            }
        }

        return new PrivilegeLookup(loaded, DecisionSource.Database, SourceOfTruthUnavailable: false);
    }
}
