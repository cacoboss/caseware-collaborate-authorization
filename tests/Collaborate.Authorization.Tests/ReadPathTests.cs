using Collaborate.Authorization.Api.Infrastructure;
using Collaborate.Authorization.Model;
using Collaborate.Authorization.Resolution;
using Collaborate.Authorization.ReadPath;
using Collaborate.Authorization.Service;

namespace Collaborate.Authorization.Tests;

/// <summary>
/// The read path degrades along two independent axes. All six cells of the matrix in the
/// scope document are asserted here, plus invalidation and the consistency of the two
/// query shapes.
/// </summary>
public class ReadPathTests
{
    private const string Subject = "user-1";
    private const string Workspace = "ws-1";
    private static readonly Resource Document = new("doc-1", "document");

    private static PrivilegeTree Tree(WorkspaceRole role) =>
        new(Subject, Workspace, role, FirmPolicy: [], Overrides: [], Resources: [Document]);

    private static (AuthorizationService Service, InMemoryPrivilegeStore Store, InMemoryPrivilegeCache Cache) Build(
        WorkspaceRole role = WorkspaceRole.Contributor)
    {
        var store = new InMemoryPrivilegeStore();
        var cache = new InMemoryPrivilegeCache();
        store.Seed(Tree(role));
        var service = new AuthorizationService(new PrivilegeReader(store, cache), new PermissionResolver());
        return (service, store, cache);
    }

    private static Task<AuthorizationDecision> Check(AuthorizationService service, PermissionAction action = PermissionAction.Edit) =>
        service.CheckAsync(Subject, Workspace, Document.Id, action, CancellationToken.None);

    // ------------------------------------------------- database reachable

    [Test]
    public async Task Cache_absent_loads_from_the_source_of_truth_and_reports_it()
    {
        var (service, _, _) = Build();

        var decision = await Check(service);

        await Assert.That(decision.Allowed).IsTrue();
        await Assert.That(decision.Source).IsEqualTo(DecisionSource.Database);
    }

    [Test]
    public async Task A_load_populates_the_cache_so_the_next_call_is_served_from_it()
    {
        var (service, _, _) = Build();

        await Check(service);
        var second = await Check(service);

        await Assert.That(second.Source).IsEqualTo(DecisionSource.Cache);
    }

    [Test]
    public async Task A_cache_outage_costs_latency_not_correctness()
    {
        var (service, _, cache) = Build();
        cache.Fail = true;

        var decision = await Check(service);

        await Assert.That(decision.Allowed)
            .IsTrue()
            .Because("fail-closed is scoped to the source of truth, not to the cache");
        await Assert.That(decision.Source).IsEqualTo(DecisionSource.Database);
    }

    // ----------------------------------------------- database unreachable

    [Test]
    public async Task A_cached_subject_keeps_working_while_the_database_is_down()
    {
        var (service, store, _) = Build();
        await Check(service);           // warm the cache

        store.Fail = true;
        var decision = await Check(service);

        await Assert.That(decision.Allowed).IsTrue();
        await Assert.That(decision.Source).IsEqualTo(DecisionSource.Cache);
    }

    [Test]
    public async Task An_uncached_subject_is_denied_while_the_database_is_down()
    {
        var (service, store, _) = Build();
        store.Fail = true;

        var decision = await Check(service);

        await Assert.That(decision.Allowed).IsFalse();
        await Assert.That(decision.DecidingRule)
            .IsEqualTo(DecidingRule.SourceUnavailable)
            .Because("the response has to say we could not check, not that policy denied");
        await Assert.That(decision.Source)
            .IsEqualTo(DecisionSource.Unavailable)
            .Because("nothing was read, so the decision must not claim a source");
    }

    [Test]
    public async Task Both_dependencies_down_denies()
    {
        var (service, store, cache) = Build();
        store.Fail = true;
        cache.Fail = true;

        var decision = await Check(service);

        await Assert.That(decision.Allowed).IsFalse();
        await Assert.That(decision.DecidingRule).IsEqualTo(DecidingRule.SourceUnavailable);
    }

    [Test]
    public async Task Enumeration_says_it_could_not_answer_rather_than_returning_nothing()
    {
        var (service, store, _) = Build();
        store.Fail = true;

        var result = await service.EnumerateAsync(Subject, Workspace, CancellationToken.None);

        await Assert.That(result.SourceAvailable)
            .IsFalse()
            .Because("an empty list would read as 'this subject may do nothing'");
        await Assert.That(result.Permissions).IsEmpty();
    }

    // ------------------------------------------------------- invalidation

    [Test]
    public async Task A_revoked_permission_takes_effect_on_the_next_call()
    {
        var (service, store, cache) = Build(WorkspaceRole.Contributor);
        var before = await Check(service);
        await Assert.That(before.Allowed).IsTrue();

        // What the Auth-API would write, followed by what the bus consumer would call.
        store.Replace(Tree(WorkspaceRole.Viewer));
        await cache.EvictAsync(Subject, Workspace, CancellationToken.None);

        var after = await Check(service);

        await Assert.That(after.Allowed)
            .IsFalse()
            .Because("revocation must land without the caller re-authenticating");
        await Assert.That(after.Source).IsEqualTo(DecisionSource.Database);
    }

    [Test]
    public async Task Without_eviction_the_cached_tree_is_still_served()
    {
        var (service, store, _) = Build(WorkspaceRole.Contributor);
        await Check(service);

        store.Replace(Tree(WorkspaceRole.Viewer));   // written, but never invalidated

        var after = await Check(service);

        await Assert.That(after.Allowed)
            .IsTrue()
            .Because("this is the stale window the design accepts, and it is bounded by invalidation");
        await Assert.That(after.Source).IsEqualTo(DecisionSource.Cache);
    }

    // ------------------------------------------------ what the cache is for

    [Test]
    public async Task A_hundred_checks_make_one_trip_to_the_source_of_truth()
    {
        var (service, store, _) = Build();

        for (var i = 0; i < 100; i++)
            await Check(service);

        await Assert.That(store.Loads)
            .IsEqualTo(1)
            .Because("the whole read path exists so that repeated checks do not reach the database");
    }

    [Test]
    public async Task Eviction_costs_exactly_one_more_trip()
    {
        var (service, store, cache) = Build();

        await Check(service);
        await Check(service);
        await cache.EvictAsync(Subject, Workspace, CancellationToken.None);
        await Check(service);
        await Check(service);

        await Assert.That(store.Loads)
            .IsEqualTo(2)
            .Because("invalidation is what sends a request back to the source of truth, and nothing else does");
    }

    // ---------------------------------------------------- shape agreement

    [Test]
    public async Task The_point_query_agrees_with_the_enumeration_for_every_entry()
    {
        var (service, _, _) = Build(WorkspaceRole.Contributor);

        var enumerated = await service.EnumerateAsync(Subject, Workspace, CancellationToken.None);
        await Assert.That(enumerated.SourceAvailable).IsTrue();

        foreach (var entry in enumerated.Permissions)
        {
            var point = await service.CheckAsync(Subject, Workspace, entry.ResourceId, entry.Action, CancellationToken.None);

            await Assert.That(point.Allowed).IsEqualTo(entry.Allowed);
            await Assert.That(point.DecidingRule).IsEqualTo(entry.DecidingRule);
        }
    }

    [Test]
    public async Task An_action_absent_from_the_enumeration_is_denied_by_the_point_query()
    {
        var (service, _, _) = Build(WorkspaceRole.Contributor);

        var enumerated = await service.EnumerateAsync(Subject, Workspace, CancellationToken.None);
        var granted = enumerated.Permissions.Select(p => p.Action).ToHashSet();

        foreach (var action in Enum.GetValues<PermissionAction>().Where(a => !granted.Contains(a)))
        {
            var point = await service.CheckAsync(Subject, Workspace, Document.Id, action, CancellationToken.None);

            await Assert.That(point.Allowed)
                .IsFalse()
                .Because($"{action} was not enumerated, so the point query must not grant it");
        }
    }

    [Test]
    public async Task An_unknown_resource_is_denied_as_no_grant()
    {
        var (service, _, _) = Build();

        var decision = await service.CheckAsync(Subject, Workspace, "doc-unknown", PermissionAction.View, CancellationToken.None);

        await Assert.That(decision.Allowed).IsFalse();
        await Assert.That(decision.DecidingRule).IsEqualTo(DecidingRule.NoGrant);
    }
}
