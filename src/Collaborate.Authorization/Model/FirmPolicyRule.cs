namespace Collaborate.Authorization.Model;

/// <summary>
/// A firm-level rule. Applies to every resource of a type across the firm and cannot be
/// lifted inside a workspace.
/// </summary>
public sealed record FirmPolicyRule(string ResourceType, PermissionAction Action, bool Allow);
