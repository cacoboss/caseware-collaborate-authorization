using System.Text;
using System.Text.Json.Serialization;
using Collaborate.Authorization.ReadPath;
using Collaborate.Authorization.Resolution;
using Collaborate.Authorization.Service;
using Collaborate.Authorization.Api.Endpoints;
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

// The in-memory pair stands in for the database and Redis. Both are registered by their
// concrete type as well, so tests can make either one fail.
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
app.MapPermissionEndpoints();

app.Run();

/// <summary>Exposed so the integration tests can host the application.</summary>
public partial class Program;
