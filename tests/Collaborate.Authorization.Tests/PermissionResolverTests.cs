using Collaborate.Authorization.Model;
using Collaborate.Authorization.Resolution;

namespace Collaborate.Authorization.Tests;

/// <summary>
/// The precedence matrix. These tests retire the risk named in the design document's
/// implementation plan: "an explicit resource-level deny is masked by an inherited
/// workspace allow".
///
/// The exhaustive tests assert invariants rather than comparing against a second
/// expectation table. A hand-written table of expected outcomes is just the resolver
/// written twice, and it is wrong in the same places.
/// </summary>
public class PermissionResolverTests
{
    private static readonly PermissionResolver Resolver = new();
    private static readonly Resource Document = new("doc-1", "document");

    private static readonly PermissionAction[] AllActions = Enum.GetValues<PermissionAction>();
    private static readonly WorkspaceRole?[] AllRoles = [null, .. Enum.GetValues<WorkspaceRole>().Cast<WorkspaceRole?>()];
    private static readonly bool?[] AllRuleStates = [null, true, false];   // absent, allow, deny

    private static PrivilegeTree Tree(WorkspaceRole? role, bool? firmPolicy, bool? resourceOverride, PermissionAction action) =>
        new(
            SubjectId: "user-1",
            WorkspaceId: "ws-1",
            Role: role,
            FirmPolicy: firmPolicy is { } fp ? [new FirmPolicyRule(Document.Type, action, fp)] : [],
            Overrides: resourceOverride is { } ro ? [new ResourceOverride(Document.Id, action, ro)] : [],
            Resources: [Document]);

    /// <summary>Every combination of the three planes, for every action. 144 cases.</summary>
    private static IEnumerable<(WorkspaceRole? Role, bool? FirmPolicy, bool? Override, PermissionAction Action)> Matrix()
    {
        foreach (var action in AllActions)
        foreach (var role in AllRoles)
        foreach (var firmPolicy in AllRuleStates)
        foreach (var over in AllRuleStates)
            yield return (role, firmPolicy, over, action);
    }

    // ---------------------------------------------------------------- invariants

    [Test]
    public async Task An_explicit_deny_is_never_masked_by_any_allow()
    {
        foreach (var (role, firmPolicy, over, action) in Matrix())
        {
            if (firmPolicy is not false && over is not false) continue;

            var decision = Resolver.Resolve(Tree(role, firmPolicy, over, action), Document, action);

            await Assert.That(decision.Allowed)
                .IsFalse()
                .Because($"role={role}, firmPolicy={firmPolicy}, override={over}, action={action} contains an explicit deny");
        }
    }

    [Test]
    public async Task A_firm_policy_deny_is_reported_over_a_resource_deny()
    {
        foreach (var (role, _, over, action) in Matrix())
        {
            var decision = Resolver.Resolve(Tree(role, firmPolicy: false, over, action), Document, action);

            await Assert.That(decision.DecidingRule).IsEqualTo(DecidingRule.FirmPolicy);
        }
    }

    [Test]
    public async Task Every_decision_names_a_rule_and_no_grant_only_appears_on_a_denial()
    {
        foreach (var (role, firmPolicy, over, action) in Matrix())
        {
            var decision = Resolver.Resolve(Tree(role, firmPolicy, over, action), Document, action);

            if (decision.DecidingRule is DecidingRule.NoGrant)
                await Assert.That(decision.Allowed)
                    .IsFalse()
                    .Because("no_grant means nothing granted the action");
        }
    }

    [Test]
    public async Task An_allow_is_only_ever_credited_to_a_plane_that_actually_granted_it()
    {
        foreach (var (role, firmPolicy, over, action) in Matrix())
        {
            var decision = Resolver.Resolve(Tree(role, firmPolicy, over, action), Document, action);
            if (!decision.Allowed) continue;

            var granted = decision.DecidingRule switch
            {
                DecidingRule.ResourceOverride => over is true,
                DecidingRule.WorkspaceRole => role is { } r && RoleGrants.Grants(r, action),
                DecidingRule.FirmPolicy => firmPolicy is true,
                _ => false
            };

            await Assert.That(granted)
                .IsTrue()
                .Because($"{decision.DecidingRule} was credited for allowing {action} but did not grant it");
        }
    }

    // ------------------------------------------------------- named cases from the design

    [Test]
    public async Task A_resource_deny_beats_an_inherited_workspace_allow()
    {
        var tree = Tree(WorkspaceRole.Owner, firmPolicy: null, resourceOverride: false, PermissionAction.Edit);

        var decision = Resolver.Resolve(tree, Document, PermissionAction.Edit);

        await Assert.That(decision.Allowed).IsFalse();
        await Assert.That(decision.DecidingRule).IsEqualTo(DecidingRule.ResourceOverride);
    }

    [Test]
    public async Task A_resource_allow_grants_an_action_the_role_does_not()
    {
        // The brief's example: a single document shared with one external user only.
        var tree = Tree(WorkspaceRole.Viewer, firmPolicy: null, resourceOverride: true, PermissionAction.Edit);

        var decision = Resolver.Resolve(tree, Document, PermissionAction.Edit);

        await Assert.That(decision.Allowed).IsTrue();
        await Assert.That(decision.DecidingRule).IsEqualTo(DecidingRule.ResourceOverride);
    }

    [Test]
    public async Task A_role_grant_is_credited_to_the_workspace_role()
    {
        var tree = Tree(WorkspaceRole.Contributor, firmPolicy: null, resourceOverride: null, PermissionAction.Edit);

        var decision = Resolver.Resolve(tree, Document, PermissionAction.Edit);

        await Assert.That(decision.Allowed).IsTrue();
        await Assert.That(decision.DecidingRule).IsEqualTo(DecidingRule.WorkspaceRole);
    }

    [Test]
    public async Task A_subject_with_no_role_and_no_rules_is_denied_as_no_grant()
    {
        var tree = Tree(role: null, firmPolicy: null, resourceOverride: null, PermissionAction.View);

        var decision = Resolver.Resolve(tree, Document, PermissionAction.View);

        await Assert.That(decision.Allowed).IsFalse();
        await Assert.That(decision.DecidingRule).IsEqualTo(DecidingRule.NoGrant);
    }

    [Test]
    public async Task A_contributor_cannot_manage()
    {
        var tree = Tree(WorkspaceRole.Contributor, firmPolicy: null, resourceOverride: null, PermissionAction.Manage);

        var decision = Resolver.Resolve(tree, Document, PermissionAction.Manage);

        await Assert.That(decision.Allowed).IsFalse();
        await Assert.That(decision.DecidingRule)
            .IsEqualTo(DecidingRule.NoGrant)
            .Because("a role that does not grant an action has not denied it either");
    }
}
