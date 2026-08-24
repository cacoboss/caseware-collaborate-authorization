using Collaborate.Authorization.Model;
using Collaborate.Authorization.Resolution;
using Collaborate.Authorization.ReadPath;

namespace Collaborate.Authorization.Service;

/// <summary>One answer, and everything an audit needs to explain it.</summary>
public sealed record AuthorizationDecision(
    string ResourceId,
    PermissionAction Action,
    bool Allowed,
    DecidingRule DecidingRule,
    DecisionSource Source);
