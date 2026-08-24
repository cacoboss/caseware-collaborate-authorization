namespace Collaborate.Authorization.Model;

/// <summary>Applies to every resource of a type in the firm. A workspace cannot lift it.</summary>
public sealed record FirmPolicyRule(string ResourceType, PermissionAction Action, bool Allow);
