namespace Collaborate.Authorization;

/// <summary>Where the privilege tree behind a decision came from.</summary>
public enum DecisionSource
{
    Cache,
    Database
}

/// <summary>The source of truth. Slow, authoritative, and allowed to be unreachable.</summary>
public interface IPrivilegeStore
{
    Task<PrivilegeTree?> LoadAsync(string subjectId, string workspaceId, CancellationToken ct);
}

/// <summary>
/// The read-side copy. Never authoritative. <see cref="EvictAsync"/> is the contract a bus
/// consumer would call when a permission changes; the bus itself is out of scope.
/// </summary>
public interface IPrivilegeCache
{
    Task<PrivilegeTree?> GetAsync(string subjectId, string workspaceId, CancellationToken ct);
    Task SetAsync(PrivilegeTree tree, CancellationToken ct);
    Task EvictAsync(string subjectId, string workspaceId, CancellationToken ct);
}

/// <param name="Tree">Null when the subject has no privileges, or when they could not be read.</param>
/// <param name="SourceOfTruthUnavailable">
/// True only when the database could not answer and the cache had nothing. This is the one
/// condition that fails closed; a cache outage on its own does not.
/// </param>
public sealed record PrivilegeLookup(
    PrivilegeTree? Tree,
    DecisionSource Source,
    bool SourceOfTruthUnavailable);

/// <summary>
/// Reads the privilege tree, cache first, and degrades along two independent axes.
///
///                     database reachable        database unreachable
///   cache, present    serve from cache          serve from cache
///   cache, absent     load, populate, serve     fail closed
///   cache unavailable load and serve            fail closed
///
/// The bottom-left cell is the one worth stating: a cache outage with a healthy database
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
