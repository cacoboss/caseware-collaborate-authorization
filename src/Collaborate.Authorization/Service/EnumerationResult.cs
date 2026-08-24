namespace Collaborate.Authorization.Service;

/// <summary>
/// The result of an enumeration. <paramref name="SourceAvailable"/> is false when the tree
/// could not be read at all, which is a different answer from an empty set of permissions.
/// </summary>
public sealed record EnumerationResult(bool SourceAvailable, IReadOnlyList<AuthorizationDecision> Permissions)
{
    public static readonly EnumerationResult Unavailable = new(false, []);
}
