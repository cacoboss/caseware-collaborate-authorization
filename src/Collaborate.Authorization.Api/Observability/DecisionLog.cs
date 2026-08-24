using Collaborate.Authorization.Api.Authentication;
using Collaborate.Authorization.Service;

namespace Collaborate.Authorization.Api.Observability;

/// <summary>
/// One structured line per decision. `deciding_rule` is the field that matters: without it
/// an auditor can see that access was denied but not on what basis, and a denial nobody can
/// explain is indistinguishable from a bug.
/// </summary>
public static class DecisionLog
{
    public static void Write(ILogger logger, Caller caller, AuthorizationDecision decision, TimeSpan elapsed) =>
        logger.LogInformation(
            "decision {DecisionId} sub={Subject} act={Actor} resource={Resource} action={Action} " +
            "decision={Decision} deciding_rule={DecidingRule} source={Source} latency_ms={LatencyMs}",
            Guid.NewGuid(),
            caller.Subject,
            caller.Actor ?? "-",
            decision.ResourceId,
            decision.Action,
            decision.Allowed ? "allow" : "deny",
            decision.DecidingRule,
            decision.Source,
            elapsed.TotalMilliseconds);
}
