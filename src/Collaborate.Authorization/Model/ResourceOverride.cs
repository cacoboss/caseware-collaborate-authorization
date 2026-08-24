namespace Collaborate.Authorization.Model;

/// <summary>Applies to one resource for one subject.</summary>
public sealed record ResourceOverride(string ResourceId, PermissionAction Action, bool Allow);
