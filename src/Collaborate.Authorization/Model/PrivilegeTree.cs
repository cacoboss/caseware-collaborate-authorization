namespace Collaborate.Authorization.Model;

/// <summary>
/// Everything needed to answer for one subject in one workspace. This is the cache entry,
/// which is why enumeration is a single read.
/// </summary>
public sealed record PrivilegeTree(
    string SubjectId,
    string WorkspaceId,
    WorkspaceRole? Role,
    IReadOnlyList<FirmPolicyRule> FirmPolicy,
    IReadOnlyList<ResourceOverride> Overrides,
    IReadOnlyList<Resource> Resources);
