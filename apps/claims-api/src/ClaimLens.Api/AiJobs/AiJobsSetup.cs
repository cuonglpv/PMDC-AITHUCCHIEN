using ClaimLens.Api.AiJobs.Appliers;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.AiJobs;

/// <summary>
/// Registers the AI Job Runner. Kept here so the Document AI client and the thresholds stay inside AiJobs/ (C3 rule 4
/// and the architecture tests); the rest of the API only queues runs with <see cref="AiJobQueue"/>.
/// </summary>
public static class AiJobsSetup
{
    public static void AddAiJobs(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        Startup.AddValidated<AiOptions, AiOptionsValidator>(builder, "Ai");
        Startup.AddValidated<ThresholdsOptions, ThresholdsOptionsValidator>(builder, "Thresholds");

        services.AddSingleton<AiJobQueue>();
        services.AddSingleton<Thresholds>();
        services.AddSingleton<AiRunAttempts>();
        services
            .AddHttpClient<IDocumentAiClient, DocumentAiClient>()
            .ConfigureHttpClient((provider, http) =>
            {
                var ai = provider.GetRequiredService<IOptions<AiOptions>>().Value;
                http.BaseAddress = new Uri(ai.BaseUrl.TrimEnd('/') + "/v1/");

                // Each task has its own limit, which DocumentAiClient applies per call.
                http.Timeout = Timeout.InfiniteTimeSpan;
            });
        services.AddScoped<PageCheckApplier>();
        services.AddScoped<FieldExtractionApplier>();
        services.AddScoped<AiRunProcessor>();
        services.AddHostedService<AiJobRunner>();
    }
}
