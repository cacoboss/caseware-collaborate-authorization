using System.Text.Json;
using System.Text.Json.Serialization;
using Collaborate.Authorization.Model;
using Collaborate.Authorization.ReadPath;
using StackExchange.Redis;

namespace Collaborate.Authorization.Api.Infrastructure;

/// <summary>
/// The read side backed by Redis. One JSON value per subject and workspace, which is what
/// makes enumeration a single read.
///
/// No expiry is set. Invalidation is pushed, not driven by TTL — a short TTL would move the
/// load back onto the database, which is the thing the cache exists to avoid.
/// </summary>
public sealed class RedisPrivilegeCache(IConnectionMultiplexer redis) : IPrivilegeCache
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<PrivilegeTree?> GetAsync(string subjectId, string workspaceId, CancellationToken ct)
    {
        var value = await redis.GetDatabase().StringGetAsync(Key(subjectId, workspaceId));

        return value.IsNullOrEmpty
            ? null
            : JsonSerializer.Deserialize<PrivilegeTree>((string)value!, Json);
    }

    public async Task SetAsync(PrivilegeTree tree, CancellationToken ct) =>
        await redis.GetDatabase().StringSetAsync(
            Key(tree.SubjectId, tree.WorkspaceId),
            JsonSerializer.Serialize(tree, Json));

    /// <summary>What a bus consumer calls on a permission change. The bus is out of scope.</summary>
    public async Task EvictAsync(string subjectId, string workspaceId, CancellationToken ct) =>
        await redis.GetDatabase().KeyDeleteAsync(Key(subjectId, workspaceId));

    private static RedisKey Key(string subjectId, string workspaceId) =>
        $"privileges:{workspaceId}:{subjectId}";
}
