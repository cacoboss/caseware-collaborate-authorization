using Collaborate.Authorization.Model;

namespace Collaborate.Authorization.ReadPath;

/// <summary>
/// Cache first, source of truth second. The cache and the database fail independently;
/// the six resulting cases are in Scope.md. Fail-closed applies to the source of truth
/// only — a cache outage costs latency, not correctness.
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
            // Cache is down. Fall through and load; the answer is still correct.
        }

        if (cached is not null)
            return new PrivilegeLookup(cached, DecisionSource.Cache);

        PrivilegeTree? loaded;
        try
        {
            loaded = await store.LoadAsync(subjectId, workspaceId, ct);
        }
        catch (Exception)
        {
            return new PrivilegeLookup(null, DecisionSource.Unavailable);
        }

        if (loaded is not null)
        {
            try
            {
                await cache.SetAsync(loaded, ct);
            }
            catch (Exception)
            {
                // Could not populate. This answer is fine; the next call pays the same cost.
            }
        }

        return new PrivilegeLookup(loaded, DecisionSource.Database);
    }
}
