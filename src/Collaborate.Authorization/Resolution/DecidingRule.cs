namespace Collaborate.Authorization.Resolution;

/// <summary>Which plane produced a decision. Every decision names one.</summary>
public enum DecidingRule
{
    FirmPolicy,
    WorkspaceRole,
    ResourceOverride,

    /// <summary>Nothing granted the action. A denial, not a missing value.</summary>
    NoGrant,

    /// <summary>Could not check. Set by the read path, never by the resolver.</summary>
    SourceUnavailable
}
