using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaimLens.Api.AccessControl;
using ClaimLens.Api.AiJobs;
using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.DocumentPreparation;
using ClaimLens.Api.DocumentStore;
using ClaimLens.Api.Features.Evidence;
using ClaimLens.Api.Features.Intake;
using ClaimLens.Api.Features.Lifecycle;
using ClaimLens.Api.Features.RuleSets;
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
        services.AddOptions<AuthOptions>().Bind(builder.Configuration.GetSection("Auth")).ValidateOnStart();
        services.AddSingleton<IValidateOptions<AuthOptions>, AuthOptionsValidator>();
        services.Configure<ReleaseOptions>(builder.Configuration.GetSection("Release"));
        AddValidated<StorageOptions, StorageOptionsValidator>(builder, "Storage");
        AddValidated<ConverterOptions, ConverterOptionsValidator>(builder, "Converter");
        AddValidated<LimitsOptions, LimitsOptionsValidator>(builder, "Limits");
        AddValidated<WorkersOptions, WorkersOptionsValidator>(builder, "Workers");
        AddValidated<AiOptions, AiOptionsValidator>(builder, "Ai");
        AddValidated<QueueOptions, QueueOptionsValidator>(builder, "Queue");

        services.AddDbContext<ClaimLensDbContext>(options =>
            options.UseNpgsql(
                builder.Configuration.GetConnectionString("Database"),
                EnumMapping.MapAll));

        services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));

        services.AddSingleton<IPasswordHasher<Domain.Entities.StaffAccount>, PasswordHasher<Domain.Entities.StaffAccount>>();
        services.AddSingleton<TokenService>();
        services.AddSingleton<SignInThrottle>();
        services.AddScoped<RefreshTokenStore>();

        services.AddSingleton<QueuePriority>();
        services.AddSingleton<AiJobQueue>();

        // Document Store and Document Preparation (US03).
        services.AddSingleton<IDocumentStore, S3DocumentStore>();
        services.AddHostedService<BucketSetup>();
        services
            .AddHttpClient<IDocumentConverter, ConverterClient>()
            .ConfigureHttpClient((provider, http) =>
            {
                var converter = provider.GetRequiredService<IOptions<ConverterOptions>>().Value;
                http.BaseAddress = new Uri(converter.BaseUrl.TrimEnd('/') + "/");
                http.Timeout = TimeSpan.FromSeconds(converter.TimeoutSeconds);
            });
        services.AddSingleton<PageRenderer>();
        services.AddSingleton<PreparationSignal>();
        services.AddScoped<DocumentPreparer>();
        services.AddHostedService<DocumentPreparationWorker>();

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
                    ValidateIssuer = true,
                    ValidIssuer = auth.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = auth.Value.Audience,
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

        // Only the proxies named here may say who the client is (infra/compose.yaml sets them for the full stack).
        services
            .AddOptions<ForwardedHeadersOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
                foreach (var proxy in configuration.GetSection("Proxy:KnownProxies").Get<string[]>() ?? [])
                {
                    options.KnownProxies.Add(IPAddress.Parse(proxy));
                }

                foreach (var network in configuration.GetSection("Proxy:KnownNetworks").Get<string[]>() ?? [])
                {
                    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
                }
            });
    }

    public static void UseClaimLens(this WebApplication app)
    {
        // With both lists empty ASP.NET Core would believe X-Forwarded-For from any caller, who could then forge
        // audit_event.ip_address and dodge the per-address sign-in limit. So no trusted proxy means no forwarded headers.
        var forwarded = app.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        if (forwarded.KnownProxies.Count > 0 || forwarded.KnownIPNetworks.Count > 0)
        {
            app.UseForwardedHeaders();
        }

        app.UseMiddleware<RequestIdMiddleware>();
        app.UseMiddleware<ErrorMiddleware>();
        app.UseAuthentication();
        app.UseRouting();
        app.UseMiddleware<OperationPolicyMiddleware>();

        app.MapGet("/health", () => Results.Ok(new { status = "up" }));

        var api = app.MapGroup("/api/v1");
        api.MapAuth();
        api.MapIntake();
        api.MapLifecycle();
        api.MapEvidence();
        api.MapRuleSets();
        NotBuiltEndpoints.MapRemaining(api, app);
    }

    /// <summary>Each section is checked when the API starts, so a wrong value stops it instead of the first request.</summary>
    private static void AddValidated<TOptions, TValidator>(WebApplicationBuilder builder, string section)
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
    {
        builder.Services.AddOptions<TOptions>().Bind(builder.Configuration.GetSection(section)).ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<TOptions>, TValidator>();
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
