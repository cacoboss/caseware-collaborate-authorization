namespace Collaborate.Authorization;

/// <summary>What a caller wants to do with a resource.</summary>
public enum PermissionAction
{
    View,
    Comment,
    Edit,
    Manage
}

/// <summary>
/// Workspace-level role. Roles only ever grant. The absence of an action from a role is
/// not a denial — it is the absence of a grant, which is a different decision.
/// </summary>
public enum WorkspaceRole
{
    Viewer,
    Contributor,
    Owner
}

/// <summary>
/// Which permission plane produced a decision.
/// <see cref="NoGrant"/> is a decision, not a missing value: nothing granted the action,
/// so it was denied by default. A denial an auditor cannot explain is indistinguishable
/// from a bug, so every decision names its rule.
/// </summary>
public enum DecidingRule
{
    FirmPolicy,
    WorkspaceRole,
    ResourceOverride,
    NoGrant,

    /// <summary>
    /// The source of truth could not be reached and nothing was cached, so the request
    /// failed closed. Produced by the read path, never by the resolver: it is a statement
    /// about availability, not about policy.
    /// </summary>
    SourceUnavailable
}

/// <summary>The outcome of one authorization question, and why it came out that way.</summary>
public sealed record Decision(bool Allowed, DecidingRule DecidingRule)
{
    public static Decision Allow(DecidingRule rule) => new(true, rule);
    public static Decision Deny(DecidingRule rule) => new(false, rule);
}

/// <summary>A resource inside a workspace.</summary>
public sealed record Resource(string Id, string Type);

/// <summary>
/// A firm-level rule. Applies to every resource of a type across the firm and cannot be
/// lifted inside a workspace.
/// </summary>
public sealed record FirmPolicyRule(string ResourceType, PermissionAction Action, bool Allow);

/// <summary>A rule attached to a single resource for a single subject.</summary>
public sealed record ResourceOverride(string ResourceId, PermissionAction Action, bool Allow);

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
