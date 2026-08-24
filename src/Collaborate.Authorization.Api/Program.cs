using System.Text;
using System.Text.Json.Serialization;
using Collaborate.Authorization.Api.Endpoints;
using Collaborate.Authorization.Api.Infrastructure;
using Collaborate.Authorization.ReadPath;
using Collaborate.Authorization.Resolution;
using Collaborate.Authorization.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Token validation is the framework's job. A symmetric key stands in for the identity
// provider, which the brief puts out of scope.
var signingKey = builder.Configuration["Auth:SigningKey"]
                 ?? "development-only-signing-key-not-for-production-use";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // The default mapping renames `sub`. This service reasons about `sub` and `act`
        // by their specification names, so keep the claims as the token wrote them.
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

// Enums by name. `"action": 2` is unreadable, and the deciding rule is the field an
// auditor reads.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// PostgreSQL when configured, in-memory otherwise. The in-memory one is also registered
// by its concrete type so tests can make it fail.
var databaseConnection = builder.Configuration["Database:ConnectionString"];
if (string.IsNullOrWhiteSpace(databaseConnection))
{
    builder.Services.AddSingleton<InMemoryPrivilegeStore>();
    builder.Services.AddSingleton<IPrivilegeStore>(sp => sp.GetRequiredService<InMemoryPrivilegeStore>());
}
else
{
    builder.Services.AddSingleton<IPrivilegeStore>(_ => new PostgresPrivilegeStore(databaseConnection));
}

// Redis when configured, in-memory otherwise. Nothing above the interface changes.
var redisConnection = builder.Configuration["Redis:ConnectionString"];
if (string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddSingleton<InMemoryPrivilegeCache>();
    builder.Services.AddSingleton<IPrivilegeCache>(sp => sp.GetRequiredService<InMemoryPrivilegeCache>());
}
else
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
    builder.Services.AddSingleton<IPrivilegeCache, RedisPrivilegeCache>();
}

builder.Services.AddSingleton<IPermissionResolver, PermissionResolver>();
builder.Services.AddSingleton<PrivilegeReader>();
builder.Services.AddSingleton<AuthorizationService>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapPermissionEndpoints();

app.Run();

/// <summary>Exposed so the integration tests can host the application.</summary>
public partial class Program;
