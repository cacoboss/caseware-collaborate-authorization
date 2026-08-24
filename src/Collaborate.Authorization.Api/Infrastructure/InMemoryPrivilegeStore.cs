using System.Collections.Concurrent;
using Collaborate.Authorization.Model;
using Collaborate.Authorization.ReadPath;

namespace Collaborate.Authorization.Api.Infrastructure;

/// <summary>
/// Stands in for the permissions database. <see cref="Fail"/> simulates it being
/// unreachable, which is how the fail-closed path is exercised.
/// </summary>
public sealed class InMemoryPrivilegeStore : IPrivilegeStore
{
    private readonly ConcurrentDictionary<string, PrivilegeTree> _trees = new();

    public bool Fail { get; set; }

    public void Seed(PrivilegeTree tree) => _trees[Key(tree.SubjectId, tree.WorkspaceId)] = tree;

    /// <summary>Applies a permission change. In production this is the Auth-API's job.</summary>
    public void Replace(PrivilegeTree tree) => Seed(tree);

    public Task<PrivilegeTree?> LoadAsync(string subjectId, string workspaceId, CancellationToken ct)
    {
        if (Fail) throw new InvalidOperationException("source of truth unreachable");

        _trees.TryGetValue(Key(subjectId, workspaceId), out var tree);
        return Task.FromResult(tree);
    }

    private static string Key(string subjectId, string workspaceId) => $"{workspaceId}/{subjectId}";
}
