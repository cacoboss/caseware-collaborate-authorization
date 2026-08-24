namespace Collaborate.Authorization.Model;

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
