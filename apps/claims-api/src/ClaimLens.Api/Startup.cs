using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaimLens.Api.AccessControl;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Http;
using ClaimLens.Api.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClaimLens.Api;

public static class Startup
{
    public static void AddClaimLens(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddSingleton(TimeProvider.System);
        services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
        services.Configure<ReleaseOptions>(builder.Configuration.GetSection("Release"));

        services.AddDbContext<ClaimLensDbContext>(options =>
            options.UseNpgsql(
                builder.Configuration.GetConnectionString("Database"),
                EnumMapping.MapAll));

        services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));

        services.AddSingleton<IPasswordHasher<Domain.Entities.StaffAccount>, PasswordHasher<Domain.Entities.StaffAccount>>();
        services.AddSingleton<TokenService>();
        services.AddSingleton<SignInThrottle>();
        services.AddScoped<RefreshTokenStore>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>, TimeProvider>((jwt, auth, clock) =>
            {
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.Zero,
                    LifetimeValidator = (notBefore, expires, _, _) =>
                    {
                        var now = clock.GetUtcNow().UtcDateTime;
                        return (notBefore is null || notBefore <= now) && expires > now;
                    },
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.Value.SigningKey)),
                };
            });

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var proxy in builder.Configuration.GetSection("Proxy:KnownProxies").Get<string[]>() ?? [])
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });
    }

    public static void UseClaimLens(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseMiddleware<RequestIdMiddleware>();
        app.UseMiddleware<ErrorMiddleware>();
        app.UseAuthentication();
        app.UseRouting();
        app.UseMiddleware<OperationPolicyMiddleware>();

        app.MapGet("/health", () => Results.Ok(new { status = "up" }));

        var api = app.MapGroup("/api/v1");
        api.MapAuth();
        NotBuiltEndpoints.MapRemaining(api, app);
    }

    public static void ConfigureJson(JsonSerializerOptions json)
    {
        json.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        json.DefaultIgnoreCondition = JsonIgnoreCondition.Never;

        // Enum values read the same in the database and the API (docs/07-api-contract.md).
        json.Converters.Add(new JsonStringEnumConverter<FieldKey>(JsonNamingPolicy.SnakeCaseLower));
        json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
    }
}
