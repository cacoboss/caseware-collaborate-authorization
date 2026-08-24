using Collaborate.Authorization.Model;

namespace Collaborate.Authorization.Resolution;

/// <summary>
/// Resolves one authorization question. A pure function: no HTTP context, no container,
/// no framework. That is what lets the precedence matrix be exercised as a table.
/// </summary>
public interface IPermissionResolver
{
    Decision Resolve(PrivilegeTree tree, Resource resource, PermissionAction action);
}
