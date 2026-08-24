using System.Collections.Concurrent;

namespace Collaborate.Authorization.Api.Infrastructure;

/// <summary>
/// Stands in for Redis. Swapping this for a real Redis client changes nothing above it,
/// which is the point of the interface.
///
/// <see cref="Fail"/> makes the cache unavailable. The behaviour that matters when it is
/// set is that decisions stay correct — a cache outage costs latency, not correctness.
/// </summary>
public sealed class InMemoryPrivilegeCache : IPrivilegeCache
{
    private readonly ConcurrentDictionary<string, PrivilegeTree> _entries = new();

    public bool Fail { get; set; }

    /// <summary>Counts loads served from here, so tests can prove a recompute happened.</summary>
    public int Hits { get; private set; }

    public Task<PrivilegeTree?> GetAsync(string subjectId, string workspaceId, CancellationToken ct)
    {
        if (Fail) throw new InvalidOperationException("cache unavailable");

        if (_entries.TryGetValue(Key(subjectId, workspaceId), out var tree))
        {
            Hits++;
            return Task.FromResult<PrivilegeTree?>(tree);
        }
        return Task.FromResult<PrivilegeTree?>(null);
    }

    public Task SetAsync(PrivilegeTree tree, CancellationToken ct)
    {
        if (Fail) throw new InvalidOperationException("cache unavailable");

        _entries[Key(tree.SubjectId, tree.WorkspaceId)] = tree;
        return Task.CompletedTask;
    }

    /// <summary>What a bus consumer calls when a permission changes. The bus is out of scope.</summary>
    public Task EvictAsync(string subjectId, string workspaceId, CancellationToken ct)
    {
        _entries.TryRemove(Key(subjectId, workspaceId), out _);
        return Task.CompletedTask;
    }

    private static string Key(string subjectId, string workspaceId) => $"{workspaceId}/{subjectId}";
}
