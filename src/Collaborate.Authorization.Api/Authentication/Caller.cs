using System.Security.Claims;
using System.Text.Json;

namespace Collaborate.Authorization.Api.Authentication;

/// <summary>
/// Who the decision is about, and who asked. The subject always comes from the token's
/// `sub`; there is no way for a caller to name a different one. Where the token carries
/// `act`, that actor is recorded for attribution and takes no part in the decision — which
/// is what keeps a delegated call from becoming a confused deputy.
/// </summary>
public sealed record Caller(string Subject, string? Actor)
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
