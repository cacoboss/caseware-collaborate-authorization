using Collaborate.Authorization.Model;

namespace Collaborate.Authorization.ReadPath;

/// <summary>
/// Never authoritative. <see cref="EvictAsync"/> is what a bus consumer would call on a
/// permission change; the bus is out of scope.
/// </summary>
public interface IPrivilegeCache
{
    Task<PrivilegeTree?> GetAsync(string subjectId, string workspaceId, CancellationToken ct);
    Task SetAsync(PrivilegeTree tree, CancellationToken ct);
    Task EvictAsync(string subjectId, string workspaceId, CancellationToken ct);
}
