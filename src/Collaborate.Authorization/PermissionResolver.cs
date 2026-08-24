namespace Collaborate.Authorization;

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

public interface IPermissionResolver
{
    Decision Resolve(PrivilegeTree tree, Resource resource, PermissionAction action);
}

/// <summary>
/// Resolves one authorization question across the three permission planes.
///
/// Denials are evaluated before grants, so an explicit deny can never be masked by an
/// inherited allow. Grants are then evaluated most specific first, so the rule reported
/// is the one closest to the resource.
/// </summary>
public sealed class PermissionResolver : IPermissionResolver
{
    public Decision Resolve(PrivilegeTree tree, Resource resource, PermissionAction action)
    {
        var firmPolicy = FirmPolicyFor(tree, resource, action);
        var resourceOverride = OverrideFor(tree, resource, action);

        // Denials first, firm policy ahead of the resource override. A firm-level
        // prohibition cannot be lifted by a workspace administrator, so when both deny it
        // is the more authoritative explanation for an audit trail.
        if (firmPolicy is false) return Decision.Deny(DecidingRule.FirmPolicy);
        if (resourceOverride is false) return Decision.Deny(DecidingRule.ResourceOverride);

        // Grants, most specific first.
        if (resourceOverride is true) return Decision.Allow(DecidingRule.ResourceOverride);
        if (tree.Role is { } role && RoleGrants.Grants(role, action))
            return Decision.Allow(DecidingRule.WorkspaceRole);
        if (firmPolicy is true) return Decision.Allow(DecidingRule.FirmPolicy);

        // Nothing granted it. That is still a decision, and it says so.
        return Decision.Deny(DecidingRule.NoGrant);
    }

    /// <summary>Returns null when no rule on this plane speaks to the question.</summary>
    private static bool? FirmPolicyFor(PrivilegeTree tree, Resource resource, PermissionAction action)
    {
        bool? result = null;
        foreach (var rule in tree.FirmPolicy)
        {
            if (rule.ResourceType != resource.Type || rule.Action != action) continue;
            if (!rule.Allow) return false;   // a deny on this plane is final for it
            result = true;
        }
        return result;
    }

    /// <summary>Returns null when no override on this resource speaks to the question.</summary>
    private static bool? OverrideFor(PrivilegeTree tree, Resource resource, PermissionAction action)
    {
        bool? result = null;
        foreach (var rule in tree.Overrides)
        {
            if (rule.ResourceId != resource.Id || rule.Action != action) continue;
            if (!rule.Allow) return false;
            result = true;
        }
        return result;
    }
}
