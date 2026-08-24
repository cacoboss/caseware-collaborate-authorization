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
    private static readonly RuleOutcome[] AllRuleStates = Enum.GetValues<RuleOutcome>();

    private static PrivilegeTree Tree(WorkspaceRole? role, RuleOutcome firmPolicy, RuleOutcome resourceOverride, PermissionAction action) =>
        new(
            SubjectId: "user-1",
            WorkspaceId: "ws-1",
            Role: role,
            FirmPolicy: firmPolicy is RuleOutcome.Absent ? [] : [new FirmPolicyRule(Document.Type, action, firmPolicy is RuleOutcome.Allow)],
            Overrides: resourceOverride is RuleOutcome.Absent ? [] : [new ResourceOverride(Document.Id, action, resourceOverride is RuleOutcome.Allow)],
            Resources: [Document]);

    /// <summary>Every combination of the three planes, for every action. 144 cases.</summary>
    private static IEnumerable<(WorkspaceRole? Role, RuleOutcome FirmPolicy, RuleOutcome Override, PermissionAction Action)> Matrix()
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
            if (firmPolicy is not RuleOutcome.Deny && over is not RuleOutcome.Deny) continue;

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
            var decision = Resolver.Resolve(Tree(role, RuleOutcome.Deny, over, action), Document, action);

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
                DecidingRule.ResourceOverride => over is RuleOutcome.Allow,
                DecidingRule.WorkspaceRole => role is { } r && RoleGrants.Grants(r, action),
                DecidingRule.FirmPolicy => firmPolicy is RuleOutcome.Allow,
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
        var tree = Tree(WorkspaceRole.Owner, RuleOutcome.Absent, RuleOutcome.Deny, PermissionAction.Edit);

        var decision = Resolver.Resolve(tree, Document, PermissionAction.Edit);

        await Assert.That(decision.Allowed).IsFalse();
        await Assert.That(decision.DecidingRule).IsEqualTo(DecidingRule.ResourceOverride);
    }

    [Test]
    public async Task A_resource_allow_grants_an_action_the_role_does_not()
    {
        // The brief's example: a single document shared with one external user only.
        var tree = Tree(WorkspaceRole.Viewer, RuleOutcome.Absent, RuleOutcome.Allow, PermissionAction.Edit);

        var decision = Resolver.Resolve(tree, Document, PermissionAction.Edit);

        await Assert.That(decision.Allowed).IsTrue();
        await Assert.That(decision.DecidingRule).IsEqualTo(DecidingRule.ResourceOverride);
    }

    [Test]
    public async Task A_role_grant_is_credited_to_the_workspace_role()
    {
        var tree = Tree(WorkspaceRole.Contributor, RuleOutcome.Absent, RuleOutcome.Absent, PermissionAction.Edit);

        var decision = Resolver.Resolve(tree, Document, PermissionAction.Edit);

        await Assert.That(decision.Allowed).IsTrue();
        await Assert.That(decision.DecidingRule).IsEqualTo(DecidingRule.WorkspaceRole);
    }

    [Test]
    public async Task A_subject_with_no_role_and_no_rules_is_denied_as_no_grant()
    {
        var tree = Tree(role: null, RuleOutcome.Absent, RuleOutcome.Absent, PermissionAction.View);

        var decision = Resolver.Resolve(tree, Document, PermissionAction.View);

        await Assert.That(decision.Allowed).IsFalse();
        await Assert.That(decision.DecidingRule).IsEqualTo(DecidingRule.NoGrant);
    }

    [Test]
    public async Task A_contributor_cannot_manage()
    {
        var tree = Tree(WorkspaceRole.Contributor, RuleOutcome.Absent, RuleOutcome.Absent, PermissionAction.Manage);

        var decision = Resolver.Resolve(tree, Document, PermissionAction.Manage);

        await Assert.That(decision.Allowed).IsFalse();
        await Assert.That(decision.DecidingRule)
            .IsEqualTo(DecidingRule.NoGrant)
            .Because("a role that does not grant an action has not denied it either");
    }
}
