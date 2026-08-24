using System.Collections.Concurrent;
using Collaborate.Authorization.Model;
using Collaborate.Authorization.ReadPath;

namespace Collaborate.Authorization.Api.Infrastructure;

/// <summary>Stands in for Redis. <see cref="Fail"/> simulates it being down.</summary>
public sealed class InMemoryPrivilegeCache : IPrivilegeCache
{
    private readonly ConcurrentDictionary<string, PrivilegeTree> _entries = new();

    public bool Fail { get; set; }

    public Task<PrivilegeTree?> GetAsync(string subjectId, string workspaceId, CancellationToken ct)
    {
        if (Fail) throw new InvalidOperationException("cache unavailable");

        _entries.TryGetValue(Key(subjectId, workspaceId), out var tree);
        return Task.FromResult(tree);
    }

    public Task SetAsync(PrivilegeTree tree, CancellationToken ct)
    {
        if (Fail) throw new InvalidOperationException("cache unavailable");

        _entries[Key(tree.SubjectId, tree.WorkspaceId)] = tree;
        return Task.CompletedTask;
    }

    /// <summary>What a bus consumer calls on a permission change. The bus is out of scope.</summary>
    public Task EvictAsync(string subjectId, string workspaceId, CancellationToken ct)
    {
        _entries.TryRemove(Key(subjectId, workspaceId), out _);
        return Task.CompletedTask;
    }

    private static string Key(string subjectId, string workspaceId) => $"{workspaceId}/{subjectId}";
}
