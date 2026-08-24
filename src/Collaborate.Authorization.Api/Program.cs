using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Collaborate.Authorization;
using Collaborate.Authorization.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Token validation is the framework's job. The brief is explicit that hand-rolling token
// parsing, signature verification or key management is the wrong move unless there is a
// specific reason, and there is not one here. A symmetric key stands in for the identity
// provider, which the brief puts out of scope.
var signingKey = builder.Configuration["Auth:SigningKey"]
                 ?? "development-only-signing-key-not-for-production-use";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claims as the token wrote them. The default mapping renames `sub`, and this
        // service reasons about `sub` and `act` by their specification names.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://identity.caseware.test",
            ValidateAudience = true,
            ValidAudience = "collaborate.sync-api",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// Serialize enums by name. `"action": 2` in a decision payload is unreadable, and the
// deciding rule is the field a consuming service and an auditor both read.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<InMemoryPrivilegeStore>();
builder.Services.AddSingleton<InMemoryPrivilegeCache>();
builder.Services.AddSingleton<IPrivilegeStore>(sp => sp.GetRequiredService<InMemoryPrivilegeStore>());
builder.Services.AddSingleton<IPrivilegeCache>(sp => sp.GetRequiredService<InMemoryPrivilegeCache>());
builder.Services.AddSingleton<IPermissionResolver, PermissionResolver>();
builder.Services.AddSingleton<PrivilegeReader>();
builder.Services.AddSingleton<AuthorizationService>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Reports everything the caller may do in a workspace. This is the shape a downstream
// service calls when it does not want to compute authorization itself.
app.MapGet("/workspaces/{workspaceId}/permissions", async (
        string workspaceId,
        ClaimsPrincipal principal,
        AuthorizationService authorization,
        ILoggerFactory loggerFactory,
        CancellationToken ct) =>
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
    })
    .RequireAuthorization();

// Answers one question about one resource. This is the shape the enforcement point calls
// per request, and the one the decision-latency target measures.
app.MapGet("/workspaces/{workspaceId}/permissions/check", async (
        string workspaceId,
        string resourceId,
        PermissionAction action,
        ClaimsPrincipal principal,
        AuthorizationService authorization,
        ILoggerFactory loggerFactory,
        CancellationToken ct) =>
    {
        var caller = Caller.From(principal);
        var started = Stopwatch.GetTimestamp();

        var decision = await authorization.CheckAsync(caller.Subject, workspaceId, resourceId, action, ct);

        DecisionLog.Write(loggerFactory.CreateLogger("DecisionLog"), caller, decision, Stopwatch.GetElapsedTime(started));

        return Results.Ok(decision);
    })
    .RequireAuthorization();

app.Run();

/// <summary>
/// Who the decision is about, and who asked. The subject always comes from the token's
/// `sub`; there is no way for a caller to name a different one. Where the token carries
/// `act`, that actor is recorded for attribution and takes no part in the decision — which
/// is what keeps a delegated call from becoming a confused deputy.
/// </summary>
internal sealed record Caller(string Subject, string? Actor)
{
    public static Caller From(ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue("sub")
                      ?? throw new InvalidOperationException("token carries no subject");

        return new Caller(subject, ActorFrom(principal));
    }

    // RFC 8693 models `act` as a JSON object whose `sub` names the actor.
    private static string? ActorFrom(ClaimsPrincipal principal)
    {
        var act = principal.FindFirstValue("act");
        if (string.IsNullOrWhiteSpace(act)) return null;

        try
        {
            using var document = JsonDocument.Parse(act);
            return document.RootElement.TryGetProperty("sub", out var actorSubject)
                ? actorSubject.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// One structured line per decision. `deciding_rule` is the field that matters: without it
/// an auditor can see that access was denied but not on what basis, and a denial nobody can
/// explain is indistinguishable from a bug.
/// </summary>
internal static class DecisionLog
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

/// <summary>Exposed so the integration tests can host the application.</summary>
public partial class Program;
