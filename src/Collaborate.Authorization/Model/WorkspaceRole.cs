namespace Collaborate.Authorization.Model;

/// <summary>Roles only grant. An action missing from a role is no_grant, not a deny.</summary>
public enum WorkspaceRole
{
    Viewer,
    Contributor,
    Owner
}
