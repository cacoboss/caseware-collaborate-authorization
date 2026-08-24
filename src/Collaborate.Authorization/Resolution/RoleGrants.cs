using Collaborate.Authorization.Model;

namespace Collaborate.Authorization.Resolution;

/// <summary>Which actions each workspace role grants.</summary>
public static class RoleGrants
{
    public static bool Grants(WorkspaceRole role, PermissionAction action) => role switch
    {
        WorkspaceRole.Viewer => action is PermissionAction.View,
        WorkspaceRole.Contributor => action is PermissionAction.View
            or PermissionAction.Comment
            or PermissionAction.Edit,
        WorkspaceRole.Owner => true,
        _ => false
    };
}
