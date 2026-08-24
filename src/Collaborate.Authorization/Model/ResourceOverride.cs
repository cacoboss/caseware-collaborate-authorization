namespace Collaborate.Authorization.Model;

/// <summary>A rule attached to a single resource for a single subject.</summary>
public sealed record ResourceOverride(string ResourceId, PermissionAction Action, bool Allow);
