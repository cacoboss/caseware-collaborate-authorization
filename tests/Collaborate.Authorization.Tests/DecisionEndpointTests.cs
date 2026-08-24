using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Collaborate.Authorization;
using Collaborate.Authorization.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Collaborate.Authorization.Tests;

/// <summary>
/// The endpoint over real HTTP with real, signed tokens. Token validation is the
/// framework's; these tests are about what the service does with a valid one.
/// </summary>
public class DecisionEndpointTests
{
    private const string SigningKey = "development-only-signing-key-not-for-production-use";

    /// <summary>Matches the API: enums travel by name, not by ordinal.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private const string Workspace = "ws-1";
    private static readonly Resource Document = new("doc-1", "document");

    private static PrivilegeTree Tree(string subject, WorkspaceRole role) =>
        new(subject, Workspace, role, FirmPolicy: [], Overrides: [], Resources: [Document]);

    private static string Token(string subject, string? actor = null)
    {
        var claims = new Dictionary<string, object> { ["sub"] = subject };
        if (actor is not null)
            claims["act"] = new Dictionary<string, object> { ["sub"] = actor };

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "https://identity.caseware.test",
            Audience = "collaborate.sync-api",
            Claims = claims,
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256)
        });
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory, string subject, string? actor = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(subject, actor));
        return client;
    }

    private static string CheckUrl(PermissionAction action) =>
        $"/workspaces/{Workspace}/permissions/check?resourceId={Document.Id}&action={action}";

    [Test]
    public async Task A_request_without_a_token_is_rejected()
    {
        await using var factory = new WebApplicationFactory<Program>();

        var response = await factory.CreateClient().GetAsync(CheckUrl(PermissionAction.View));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task The_point_query_answers_with_a_decision_and_the_rule_behind_it()
    {
        await using var factory = new WebApplicationFactory<Program>();
        factory.Services.GetRequiredService<InMemoryPrivilegeStore>().Seed(Tree("user-1", WorkspaceRole.Contributor));

        var response = await Client(factory, "user-1").GetAsync(CheckUrl(PermissionAction.Edit));
        var decision = await response.Content.ReadFromJsonAsync<AuthorizationDecision>(Json);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(decision!.Allowed).IsTrue();
        await Assert.That(decision.DecidingRule).IsEqualTo(DecidingRule.WorkspaceRole);
    }

    /// <summary>
    /// The headline of the design: a permission change lands while the token that was
    /// issued before it is still perfectly valid. Nobody re-authenticates.
    /// </summary>
    [Test]
    public async Task A_revocation_takes_effect_on_a_token_that_is_still_valid()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var store = factory.Services.GetRequiredService<InMemoryPrivilegeStore>();
        var cache = factory.Services.GetRequiredService<InMemoryPrivilegeCache>();
        store.Seed(Tree("user-1", WorkspaceRole.Contributor));

        var client = Client(factory, "user-1");   // one token, used for both calls

        var before = await (await client.GetAsync(CheckUrl(PermissionAction.Edit)))
            .Content.ReadFromJsonAsync<AuthorizationDecision>(Json);
        await Assert.That(before!.Allowed).IsTrue();

        store.Replace(Tree("user-1", WorkspaceRole.Viewer));
        await cache.EvictAsync("user-1", Workspace, CancellationToken.None);

        var after = await (await client.GetAsync(CheckUrl(PermissionAction.Edit)))
            .Content.ReadFromJsonAsync<AuthorizationDecision>(Json);

        await Assert.That(after!.Allowed)
            .IsFalse()
            .Because("the token never carried the permission, so revoking it does not need a new token");
    }

    /// <summary>
    /// The confused deputy. The actor is a service with wide rights; the subject is a user
    /// with none. Authorizing the actor would grant access the delegating user never had.
    /// </summary>
    [Test]
    public async Task A_delegated_call_is_authorized_against_the_subject_not_the_actor()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var store = factory.Services.GetRequiredService<InMemoryPrivilegeStore>();
        store.Seed(Tree("restricted-user", WorkspaceRole.Viewer));
        store.Seed(Tree("notification-service", WorkspaceRole.Owner));

        var client = Client(factory, subject: "restricted-user", actor: "notification-service");

        var decision = await (await client.GetAsync(CheckUrl(PermissionAction.Manage)))
            .Content.ReadFromJsonAsync<AuthorizationDecision>(Json);

        await Assert.That(decision!.Allowed)
            .IsFalse()
            .Because("the actor may manage; the delegating user may not, and the decision follows the subject");
    }

    [Test]
    public async Task Enumeration_reports_the_set_the_caller_may_act_on()
    {
        await using var factory = new WebApplicationFactory<Program>();
        factory.Services.GetRequiredService<InMemoryPrivilegeStore>().Seed(Tree("user-1", WorkspaceRole.Viewer));

        var response = await Client(factory, "user-1").GetAsync($"/workspaces/{Workspace}/permissions");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var permissions = payload.GetProperty("permissions").EnumerateArray().ToList();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(permissions).HasCount().EqualTo(1);
        await Assert.That(permissions[0].GetProperty("action").GetString()).IsEqualTo(nameof(PermissionAction.View));
        await Assert.That(permissions[0].GetProperty("decidingRule").GetString()).IsEqualTo(nameof(DecidingRule.WorkspaceRole));
    }

    [Test]
    public async Task Enumeration_returns_503_when_the_source_of_truth_cannot_be_reached()
    {
        await using var factory = new WebApplicationFactory<Program>();
        factory.Services.GetRequiredService<InMemoryPrivilegeStore>().Fail = true;

        var response = await Client(factory, "user-1").GetAsync($"/workspaces/{Workspace}/permissions");

        await Assert.That(response.StatusCode)
            .IsEqualTo(HttpStatusCode.ServiceUnavailable)
            .Because("an empty 200 would be indistinguishable from having no permissions");
    }
}
