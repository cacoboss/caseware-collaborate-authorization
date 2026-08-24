namespace Collaborate.Authorization.Api.Infrastructure;

/// <summary>
/// The shape of the source of truth. The three permission planes are three tables, which is
/// what the privilege tree is a projection of: a workspace role, resource-level overrides
/// scoped to one subject, and firm policy that applies to a resource type across the firm.
///
/// Applied by the tests. A real deployment would own this through migrations.
/// </summary>
public static class PrivilegeSchema
{
    public const string Sql = """
        create table if not exists workspaces (
            workspace_id text primary key,
            firm_id      text not null
        );

        create table if not exists workspace_members (
            workspace_id text not null,
            subject_id   text not null,
            role         text not null,
            primary key (workspace_id, subject_id)
        );

        create table if not exists resources (
            resource_id   text primary key,
            workspace_id  text not null,
            resource_type text not null
        );

        create table if not exists resource_overrides (
            resource_id text not null,
            subject_id  text not null,
            action      text not null,
            allow       boolean not null,
            primary key (resource_id, subject_id, action)
        );

        create table if not exists firm_policies (
            firm_id       text not null,
            resource_type text not null,
            action        text not null,
            allow         boolean not null,
            primary key (firm_id, resource_type, action)
        );
        """;
}
