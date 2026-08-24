using Collaborate.Authorization.Model;

namespace Collaborate.Authorization.Resolution;

public sealed class PermissionResolver : IPermissionResolver
{
    public Decision Resolve(PrivilegeTree tree, Resource resource, PermissionAction action)
    {
        var firmPolicy = FirmPolicyFor(tree, resource, action);
        var resourceOverride = OverrideFor(tree, resource, action);

        // Denies before grants, so an explicit deny is never masked by an inherited allow.
        // Firm policy first: a workspace admin cannot lift it, so it explains the denial better.
        if (firmPolicy is RuleOutcome.Deny) return Decision.Deny(DecidingRule.FirmPolicy);
        if (resourceOverride is RuleOutcome.Deny) return Decision.Deny(DecidingRule.ResourceOverride);

        // Grants, most specific first.
        if (resourceOverride is RuleOutcome.Allow) return Decision.Allow(DecidingRule.ResourceOverride);
        if (tree.Role is { } role && RoleGrants.Grants(role, action))
            return Decision.Allow(DecidingRule.WorkspaceRole);
        if (firmPolicy is RuleOutcome.Allow) return Decision.Allow(DecidingRule.FirmPolicy);

        return Decision.Deny(DecidingRule.NoGrant);
    }

    private static RuleOutcome FirmPolicyFor(PrivilegeTree tree, Resource resource, PermissionAction action)
    {
        var outcome = RuleOutcome.Absent;
        foreach (var rule in tree.FirmPolicy)
        {
            if (rule.ResourceType != resource.Type || rule.Action != action) continue;
            if (!rule.Allow) return RuleOutcome.Deny;   // a deny on this plane is final for it
            outcome = RuleOutcome.Allow;
        }
        return outcome;
    }

    private static RuleOutcome OverrideFor(PrivilegeTree tree, Resource resource, PermissionAction action)
    {
        var outcome = RuleOutcome.Absent;
        foreach (var rule in tree.Overrides)
        {
            if (rule.ResourceId != resource.Id || rule.Action != action) continue;
            if (!rule.Allow) return RuleOutcome.Deny;
            outcome = RuleOutcome.Allow;
        }
        return outcome;
    }
}
