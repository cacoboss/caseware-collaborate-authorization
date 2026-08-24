using Collaborate.Authorization.Model;

namespace Collaborate.Authorization.ReadPath;

/// <param name="Tree">Null when the subject has no privileges, or when they could not be read.</param>
/// <param name="SourceOfTruthUnavailable">
/// True only when the database could not answer and the cache had nothing. This is the one
/// condition that fails closed; a cache outage on its own does not.
/// </param>
public sealed record PrivilegeLookup(
    PrivilegeTree? Tree,
    DecisionSource Source,
    bool SourceOfTruthUnavailable);
