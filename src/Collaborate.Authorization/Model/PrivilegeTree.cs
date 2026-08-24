namespace Collaborate.Authorization.Model;

/// <summary>
/// Everything needed to answer authorization questions for one subject in one workspace,
/// resolved from the source of truth. This is the unit that is cached: one entry per
/// subject and workspace, which is why enumeration is a single read.
/// </summary>
public sealed record PrivilegeTree(
    string SubjectId,
    string WorkspaceId,
    WorkspaceRole? Role,
    IReadOnlyList<FirmPolicyRule> FirmPolicy,
    IReadOnlyList<ResourceOverride> Overrides,
    IReadOnlyList<Resource> Resources);
