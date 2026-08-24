namespace Collaborate.Authorization.Service;

/// <summary>
/// <paramref name="SourceAvailable"/> false means we could not read the tree, which is a
/// different answer from an empty set.
/// </summary>
public sealed record EnumerationResult(bool SourceAvailable, IReadOnlyList<AuthorizationDecision> Permissions)
{
    public static readonly EnumerationResult Unavailable = new(false, []);
}
