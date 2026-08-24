namespace Collaborate.Authorization.Resolution;

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
