using Collaborate.Authorization.Model;

namespace Collaborate.Authorization.Resolution;

/// <summary>No HTTP context, no container. That is what lets the matrix run as a table.</summary>
public interface IPermissionResolver
{
    Decision Resolve(PrivilegeTree tree, Resource resource, PermissionAction action);
}
