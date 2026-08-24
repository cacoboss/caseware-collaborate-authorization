using Collaborate.Authorization.Model;

namespace Collaborate.Authorization.ReadPath;

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
