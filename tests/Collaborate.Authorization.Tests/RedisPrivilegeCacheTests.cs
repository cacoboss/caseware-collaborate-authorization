using Collaborate.Authorization.Api.Infrastructure;
using Collaborate.Authorization.Model;
using Collaborate.Authorization.ReadPath;
using Collaborate.Authorization.Resolution;
using Collaborate.Authorization.Service;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Collaborate.Authorization.Tests;

/// <summary>
/// A wiring test against a real Redis. The behaviour of the read path is covered by
/// <see cref="ReadPathTests"/> with fakes, which is where failure cases belong — turning a
/// real Redis off mid-test is fiddly and a flag is not. What these tests prove is the part
/// a fake cannot: that the tree survives a round trip through a real client and server.
/// </summary>
public class RedisPrivilegeCacheTests
{
    private const string Subject = "user-1";
    private const string Workspace = "ws-1";
    private static readonly Resource Document = new("doc-1", "document");

    private static PrivilegeTree Tree(WorkspaceRole role) =>
        new(Subject, Workspace, role,
            FirmPolicy: [new FirmPolicyRule(Document.Type, PermissionAction.Manage, Allow: false)],
            Overrides: [new ResourceOverride(Document.Id, PermissionAction.Edit, Allow: true)],
            Resources: [Document]);

    internal static async Task<(RedisContainer Container, RedisPrivilegeCache Cache)> StartRedis()
    {
        var container = new RedisBuilder("redis:7-alpine").Build();
        await container.StartAsync();

        // The server is reachable but has just come up. Retry rather than abort on the
        // first refused connection.
        var options = ConfigurationOptions.Parse(container.GetConnectionString());
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 5;
        options.ConnectTimeout = 15_000;

        var connection = await ConnectionMultiplexer.ConnectAsync(options);
        return (container, new RedisPrivilegeCache(connection));
    }

    [Test]
    public async Task A_tree_survives_a_round_trip_through_redis()
    {
        var (container, cache) = await StartRedis();
        await using var _ = container;

        await cache.SetAsync(Tree(WorkspaceRole.Viewer), CancellationToken.None);
        var read = await cache.GetAsync(Subject, Workspace, CancellationToken.None);

        await Assert.That(read).IsNotNull();
        await Assert.That(read!.Role).IsEqualTo(WorkspaceRole.Viewer);
        await Assert.That(read.Resources).HasSingleItem();
        await Assert.That(read.FirmPolicy[0].Allow)
            .IsFalse()
            .Because("a deny rule that came back as an allow would be the worst kind of serialization bug");
        await Assert.That(read.Overrides[0].Action).IsEqualTo(PermissionAction.Edit);
    }

    [Test]
    public async Task An_absent_key_reads_as_no_tree_rather_than_throwing()
    {
        var (container, cache) = await StartRedis();
        await using var _ = container;

        var read = await cache.GetAsync("nobody", Workspace, CancellationToken.None);

        await Assert.That(read).IsNull();
    }

    [Test]
    public async Task Eviction_removes_the_entry()
    {
        var (container, cache) = await StartRedis();
        await using var _ = container;

        await cache.SetAsync(Tree(WorkspaceRole.Owner), CancellationToken.None);
        await cache.EvictAsync(Subject, Workspace, CancellationToken.None);

        var read = await cache.GetAsync(Subject, Workspace, CancellationToken.None);

        await Assert.That(read).IsNull();
    }

    /// <summary>
    /// The whole read path over a real cache: a miss loads and populates, the next call is
    /// served from Redis, and eviction sends the following one back to the database.
    /// </summary>
    [Test]
    public async Task The_read_path_populates_and_then_serves_from_redis()
    {
        var (container, cache) = await StartRedis();
        await using var _ = container;

        var store = new InMemoryPrivilegeStore();
        store.Seed(Tree(WorkspaceRole.Contributor));
        var service = new AuthorizationService(new PrivilegeReader(store, cache), new PermissionResolver());

        var first = await service.CheckAsync(Subject, Workspace, Document.Id, PermissionAction.Edit, CancellationToken.None);
        var second = await service.CheckAsync(Subject, Workspace, Document.Id, PermissionAction.Edit, CancellationToken.None);

        await Assert.That(first.Source).IsEqualTo(DecisionSource.Database);
        await Assert.That(second.Source).IsEqualTo(DecisionSource.Cache);
        await Assert.That(second.Allowed).IsTrue();

        await cache.EvictAsync(Subject, Workspace, CancellationToken.None);
        var third = await service.CheckAsync(Subject, Workspace, Document.Id, PermissionAction.Edit, CancellationToken.None);

        await Assert.That(third.Source).IsEqualTo(DecisionSource.Database);
    }
}
