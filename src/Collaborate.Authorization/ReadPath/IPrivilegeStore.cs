using Collaborate.Authorization.Model;

namespace Collaborate.Authorization.ReadPath;

/// <summary>The source of truth. Allowed to be unreachable.</summary>
public interface IPrivilegeStore
{
    Task<PrivilegeTree?> LoadAsync(string subjectId, string workspaceId, CancellationToken ct);
}
