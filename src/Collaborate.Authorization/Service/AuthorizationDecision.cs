using Collaborate.Authorization.Model;
using Collaborate.Authorization.ReadPath;
using Collaborate.Authorization.Resolution;

namespace Collaborate.Authorization.Service;

public sealed record AuthorizationDecision(
    string ResourceId,
    PermissionAction Action,
    bool Allowed,
    DecidingRule DecidingRule,
    DecisionSource Source);
