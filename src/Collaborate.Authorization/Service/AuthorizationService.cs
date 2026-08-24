using Collaborate.Authorization.Model;
using Collaborate.Authorization.ReadPath;
using Collaborate.Authorization.Resolution;

namespace Collaborate.Authorization.Service;

/// <summary>
/// Both query shapes run through the same resolution, which is why they cannot disagree:
/// enumeration is the point query over every resource, not a second implementation.
/// </summary>
public sealed class AuthorizationService(PrivilegeReader reader, IPermissionResolver resolver)
{
    public async Task<AuthorizationDecision> CheckAsync(
        string subjectId, string workspaceId, string resourceId, PermissionAction action, CancellationToken ct)
    {
        var lookup = await reader.ReadAsync(subjectId, workspaceId, ct);

        if (lookup.SourceOfTruthUnavailable)
            return new AuthorizationDecision(
                resourceId, action, false, DecidingRule.SourceUnavailable, DecisionSource.Unavailable);

        var resource = lookup.Tree?.Resources.FirstOrDefault(r => r.Id == resourceId);
        if (lookup.Tree is null || resource is null)
            return new AuthorizationDecision(resourceId, action, false, DecidingRule.NoGrant, lookup.Source);

        var decision = resolver.Resolve(lookup.Tree, resource, action);
        return new AuthorizationDecision(resourceId, action, decision.Allowed, decision.DecidingRule, lookup.Source);
    }

    /// <summary>One cache read: the tree is the cache entry, so this returns what is there.</summary>
    public async Task<EnumerationResult> EnumerateAsync(
        string subjectId, string workspaceId, CancellationToken ct)
    {
        var lookup = await reader.ReadAsync(subjectId, workspaceId, ct);

        // An empty list would read as "this subject may do nothing". Say we could not answer.
        if (lookup.SourceOfTruthUnavailable)
            return EnumerationResult.Unavailable;

        if (lookup.Tree is null)
            return new EnumerationResult(true, []);

        return new EnumerationResult(true,
        [
            .. from resource in lookup.Tree.Resources
               from action in Enum.GetValues<PermissionAction>()
               let decision = resolver.Resolve(lookup.Tree, resource, action)
               where decision.Allowed
               select new AuthorizationDecision(
                   resource.Id, action, true, decision.DecidingRule, lookup.Source)
        ]);
    }
}
