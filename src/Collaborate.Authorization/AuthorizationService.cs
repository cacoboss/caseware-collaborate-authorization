namespace Collaborate.Authorization;

/// <summary>
/// The result of an enumeration. <paramref name="SourceAvailable"/> is false when the tree
/// could not be read at all, which is a different answer from an empty set of permissions.
/// </summary>
public sealed record EnumerationResult(bool SourceAvailable, IReadOnlyList<AuthorizationDecision> Permissions)
{
    public static readonly EnumerationResult Unavailable = new(false, []);
}

/// <summary>One answer, and everything an audit needs to explain it.</summary>
public sealed record AuthorizationDecision(
    string ResourceId,
    PermissionAction Action,
    bool Allowed,
    DecidingRule DecidingRule,
    DecisionSource Source);

/// <summary>
/// The decision point. Both query shapes run through the same resolution, which is what
/// keeps them from disagreeing: enumeration is the point query applied to every resource
/// in the tree, not a second implementation of the same rules.
/// </summary>
public sealed class AuthorizationService(PrivilegeReader reader, IPermissionResolver resolver)
{
    /// <summary>Answers one question about one resource.</summary>
    public async Task<AuthorizationDecision> CheckAsync(
        string subjectId, string workspaceId, string resourceId, PermissionAction action, CancellationToken ct)
    {
        var lookup = await reader.ReadAsync(subjectId, workspaceId, ct);

        if (lookup.SourceOfTruthUnavailable)
            return Unavailable(resourceId, action);

        var resource = lookup.Tree?.Resources.FirstOrDefault(r => r.Id == resourceId);
        if (lookup.Tree is null || resource is null)
            return new AuthorizationDecision(resourceId, action, false, DecidingRule.NoGrant, lookup.Source);

        var decision = resolver.Resolve(lookup.Tree, resource, action);
        return new AuthorizationDecision(resourceId, action, decision.Allowed, decision.DecidingRule, lookup.Source);
    }

    /// <summary>
    /// Reports everything the subject may do in the workspace. One cache read: the tree is
    /// already the cache entry, so enumeration returns what is already materialized.
    /// </summary>
    public async Task<EnumerationResult> EnumerateAsync(
        string subjectId, string workspaceId, CancellationToken ct)
    {
        var lookup = await reader.ReadAsync(subjectId, workspaceId, ct);

        // An empty list would be indistinguishable from "this subject may do nothing", which
        // is the silent failure this service exists to avoid. Say we could not answer.
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

    private static AuthorizationDecision Unavailable(string resourceId, PermissionAction action) =>
        new(resourceId, action, false, DecidingRule.SourceUnavailable, DecisionSource.Database);
}
