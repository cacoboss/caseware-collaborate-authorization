using Collaborate.Authorization.Model;
using Collaborate.Authorization.ReadPath;
using Npgsql;

namespace Collaborate.Authorization.Api.Infrastructure;

/// <summary>
/// The source of truth over PostgreSQL. Assembles the privilege tree from the three planes
/// in one round trip: four statements in a single command, read as four result sets.
///
/// This is the cold path. It runs on a cache miss, and the cost of running it is the
/// argument for caching the assembled tree rather than the rows behind it.
/// </summary>
public sealed class PostgresPrivilegeStore(string connectionString) : IPrivilegeStore
{
    private const string LoadSql = """
        select role from workspace_members where workspace_id = @ws and subject_id = @sub;

        select resource_id, resource_type from resources where workspace_id = @ws order by resource_id;

        select o.resource_id, o.action, o.allow
        from resource_overrides o
        join resources r on r.resource_id = o.resource_id
        where r.workspace_id = @ws and o.subject_id = @sub;

        select p.resource_type, p.action, p.allow
        from firm_policies p
        join workspaces w on w.firm_id = p.firm_id
        where w.workspace_id = @ws;
        """;

    public async Task<PrivilegeTree?> LoadAsync(string subjectId, string workspaceId, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(LoadSql, connection);
        command.Parameters.AddWithValue("ws", workspaceId);
        command.Parameters.AddWithValue("sub", subjectId);

        await using var reader = await command.ExecuteReaderAsync(ct);

        WorkspaceRole? role = null;
        if (await reader.ReadAsync(ct))
            role = Enum.Parse<WorkspaceRole>(reader.GetString(0), ignoreCase: true);

        await reader.NextResultAsync(ct);
        var resources = new List<Resource>();
        while (await reader.ReadAsync(ct))
            resources.Add(new Resource(reader.GetString(0), reader.GetString(1)));

        await reader.NextResultAsync(ct);
        var overrides = new List<ResourceOverride>();
        while (await reader.ReadAsync(ct))
            overrides.Add(new ResourceOverride(
                reader.GetString(0),
                Enum.Parse<PermissionAction>(reader.GetString(1), ignoreCase: true),
                reader.GetBoolean(2)));

        await reader.NextResultAsync(ct);
        var firmPolicy = new List<FirmPolicyRule>();
        while (await reader.ReadAsync(ct))
            firmPolicy.Add(new FirmPolicyRule(
                reader.GetString(0),
                Enum.Parse<PermissionAction>(reader.GetString(1), ignoreCase: true),
                reader.GetBoolean(2)));

        // No membership and no override anywhere means this subject is not in the workspace
        // at all. Null, not an empty tree: the caller turns that into no_grant, and an empty
        // tree would claim we know the subject and they may do nothing.
        if (role is null && overrides.Count == 0)
            return null;

        return new PrivilegeTree(subjectId, workspaceId, role, firmPolicy, overrides, resources);
    }
}
