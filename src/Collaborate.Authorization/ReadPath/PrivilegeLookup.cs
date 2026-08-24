using Collaborate.Authorization.Model;

namespace Collaborate.Authorization.ReadPath;

/// <param name="Tree">Null if the subject has no privileges, or if nothing could be read.</param>
public sealed record PrivilegeLookup(PrivilegeTree? Tree, DecisionSource Source)
{
    public bool SourceOfTruthUnavailable => Source is DecisionSource.Unavailable;
}
