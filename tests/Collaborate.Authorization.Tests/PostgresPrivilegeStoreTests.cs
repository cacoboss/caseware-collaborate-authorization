using Collaborate.Authorization.Api.Infrastructure;
using Collaborate.Authorization.Model;
using Collaborate.Authorization.ReadPath;
using Collaborate.Authorization.Resolution;
using Collaborate.Authorization.Service;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Collaborate.Authorization.Tests;

/// <summary>
/// The source of truth against a real PostgreSQL. What these prove is that the privilege
/// tree is a projection of the schema and not a structure invented in memory: the three
/// planes are three tables, and assembling them is where a query can quietly go wrong.
/// </summary>
public class PostgresPrivilegeStoreTests
{
    private const string Firm = "firm-a";
    private const string Workspace = "ws-1";
    private const string Subject = "user-1";

    private static async Task<(PostgreSqlContainer Container, PostgresPrivilegeStore Store)> StartDatabase()
    {
        var container = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await container.StartAsync();

        await Execute(container, PrivilegeSchema.Sql);
        await Execute(container, $"""
            insert into workspaces (workspace_id, firm_id) values ('{Workspace}', '{Firm}');
            insert into resources (resource_id, workspace_id, resource_type) values
                ('doc-1', '{Workspace}', 'document'),
                ('fin-1', '{Workspace}', 'financial');
            """);

        return (container, new PostgresPrivilegeStore(container.GetConnectionString()));
    }

    private static async Task Execute(PostgreSqlContainer container, string sql)
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    [Test]
    public async Task A_tree_is_assembled_from_all_three_planes()
    {
        var (container, store) = await StartDatabase();
        await using var _ = container;

        await Execute(container, $"""
            insert into workspace_members values ('{Workspace}', '{Subject}', 'Contributor');
            insert into resource_overrides values ('doc-1', '{Subject}', 'Manage', true);
            insert into firm_policies values ('{Firm}', 'financial', 'View', false);
            """);

        var tree = await store.LoadAsync(Subject, Workspace, CancellationToken.None);

        await Assert.That(tree).IsNotNull();
        await Assert.That(tree!.Role).IsEqualTo(WorkspaceRole.Contributor);
        await Assert.That(tree.Resources).Count().IsEqualTo(2);
        await Assert.That(tree.Overrides).HasSingleItem();
        await Assert.That(tree.FirmPolicy).HasSingleItem();
        await Assert.That(tree.FirmPolicy[0].Allow)
            .IsFalse()
            .Because("firm policy denies viewing financial data, and a deny read as an allow is the bug that matters");
    }

    [Test]
    public async Task Another_subjects_override_never_reaches_this_tree()
    {
        var (container, store) = await StartDatabase();
        await using var _ = container;

        await Execute(container, $"""
            insert into workspace_members values ('{Workspace}', '{Subject}', 'Viewer');
            insert into resource_overrides values ('doc-1', 'someone-else', 'Manage', true);
            """);

        var tree = await store.LoadAsync(Subject, Workspace, CancellationToken.None);

        await Assert.That(tree!.Overrides)
            .IsEmpty()
            .Because("a missing subject filter on the override query would hand one user another user's access");
    }

    [Test]
    public async Task A_resource_in_another_workspace_is_not_included()
    {
        var (container, store) = await StartDatabase();
        await using var _ = container;

        await Execute(container, $"""
            insert into workspaces values ('ws-other', '{Firm}');
            insert into resources values ('doc-other', 'ws-other', 'document');
            insert into workspace_members values ('{Workspace}', '{Subject}', 'Viewer');
            """);

        var tree = await store.LoadAsync(Subject, Workspace, CancellationToken.None);

        await Assert.That(tree!.Resources.Select(r => r.Id))
            .DoesNotContain("doc-other");
    }

    [Test]
    public async Task A_subject_who_is_not_a_member_has_no_tree_at_all()
    {
        var (container, store) = await StartDatabase();
        await using var _ = container;

        var tree = await store.LoadAsync("stranger", Workspace, CancellationToken.None);

        await Assert.That(tree)
            .IsNull()
            .Because("an empty tree would claim we know this subject and they may do nothing");
    }

    [Test]
    public async Task Firm_policy_from_another_firm_does_not_apply()
    {
        var (container, store) = await StartDatabase();
        await using var _ = container;

        await Execute(container, $"""
            insert into workspace_members values ('{Workspace}', '{Subject}', 'Owner');
            insert into firm_policies values ('firm-b', 'document', 'Edit', false);
            """);

        var tree = await store.LoadAsync(Subject, Workspace, CancellationToken.None);

        await Assert.That(tree!.FirmPolicy)
            .IsEmpty()
            .Because("one firm's policy reaching another firm's workspace is a cross-tenant leak");
    }

    /// <summary>
    /// The whole read path over both containers: PostgreSQL as the source of truth, Redis as
    /// the cache, and a firm-level deny that has to survive the trip through both.
    /// </summary>
    [Test]
    public async Task The_read_path_resolves_over_a_real_database_and_a_real_cache()
    {
        var (database, store) = await StartDatabase();
        await using var _ = database;
        var (redis, cache) = await RedisPrivilegeCacheTests.StartRedis();
        await using var __ = redis;

        await Execute(database, $"""
            insert into workspace_members values ('{Workspace}', '{Subject}', 'Owner');
            insert into firm_policies values ('{Firm}', 'financial', 'View', false);
            """);

        var service = new AuthorizationService(new PrivilegeReader(store, cache), new PermissionResolver());

        var cold = await service.CheckAsync(Subject, Workspace, "fin-1", PermissionAction.View, CancellationToken.None);
        var warm = await service.CheckAsync(Subject, Workspace, "fin-1", PermissionAction.View, CancellationToken.None);

        await Assert.That(cold.Source).IsEqualTo(DecisionSource.Database);
        await Assert.That(warm.Source).IsEqualTo(DecisionSource.Cache);

        foreach (var decision in new[] { cold, warm })
        {
            await Assert.That(decision.Allowed)
                .IsFalse()
                .Because("an owner may do anything the workspace allows, and firm policy still says no");
            await Assert.That(decision.DecidingRule).IsEqualTo(DecidingRule.FirmPolicy);
        }
    }
}
