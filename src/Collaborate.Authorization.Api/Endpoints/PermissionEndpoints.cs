using System.Diagnostics;
using System.Security.Claims;
using Collaborate.Authorization.Api.Authentication;
using Collaborate.Authorization.Api.Observability;
using Collaborate.Authorization.Model;
using Collaborate.Authorization.Service;

namespace Collaborate.Authorization.Api.Endpoints;

public static class PermissionEndpoints
{
    public static void MapPermissionEndpoints(this WebApplication app)
    {
        app.MapGet("/workspaces/{workspaceId}/permissions", Enumerate).RequireAuthorization();
        app.MapGet("/workspaces/{workspaceId}/permissions/check", Check).RequireAuthorization();
    }

    /// <summary>What a service calls when it does not want to compute authorization itself.</summary>
    private static async Task<IResult> Enumerate(
        string workspaceId,
        ClaimsPrincipal principal,
        AuthorizationService authorization,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var caller = Caller.From(principal);
        var started = Stopwatch.GetTimestamp();

        var result = await authorization.EnumerateAsync(caller.Subject, workspaceId, ct);

        // Fail closed, and say so. An empty list here would read as "you may do nothing".
        if (!result.SourceAvailable)
            return Results.Problem(
                title: "Authorization data unavailable",
                detail: "The source of truth could not be reached and nothing was cached.",
                statusCode: StatusCodes.Status503ServiceUnavailable);

        var log = loggerFactory.CreateLogger("DecisionLog");
        foreach (var decision in result.Permissions)
            DecisionLog.Write(log, caller, decision, Stopwatch.GetElapsedTime(started));

        return Results.Ok(new { workspaceId, subject = caller.Subject, permissions = result.Permissions });
    }

    /// <summary>What the enforcement point calls per request. This is the 10 ms path.</summary>
    private static async Task<IResult> Check(
        string workspaceId,
        string resourceId,
        PermissionAction action,
        ClaimsPrincipal principal,
        AuthorizationService authorization,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var caller = Caller.From(principal);
        var started = Stopwatch.GetTimestamp();

        var decision = await authorization.CheckAsync(caller.Subject, workspaceId, resourceId, action, ct);

        DecisionLog.Write(loggerFactory.CreateLogger("DecisionLog"), caller, decision, Stopwatch.GetElapsedTime(started));

        return Results.Ok(decision);
    }
}
