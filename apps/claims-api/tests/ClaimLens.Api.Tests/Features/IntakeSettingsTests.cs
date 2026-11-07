using ClaimLens.Api.AiJobs;
using ClaimLens.Api.DocumentPreparation;
using ClaimLens.Api.DocumentStore;
using ClaimLens.Api.Features.Lifecycle;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.Tests.Features;

/// <summary>The sections Slice 1 adds are checked when the API starts, like Auth (docs/10-code-structure.md, section 4).</summary>
public class IntakeSettingsTests(ApiFactory api)
{
    [Theory]
    [InlineData("Storage:ServiceUrl", "storage:8333", typeof(StorageOptions))]
    [InlineData("Storage:Bucket", "Claim_Lens", typeof(StorageOptions))]
    [InlineData("Storage:SecretKey", "", typeof(StorageOptions))]
    [InlineData("Converter:BaseUrl", "", typeof(ConverterOptions))]
    [InlineData("Converter:TimeoutSeconds", "0", typeof(ConverterOptions))]
    [InlineData("Limits:MaxFileBytes", "20971521", typeof(LimitsOptions))]
    [InlineData("Limits:MaxPagesPerClaim", "31", typeof(LimitsOptions))]
    [InlineData("Workers:DocumentPreparation:PollIntervalSeconds", "0", typeof(WorkersOptions))]
    [InlineData("Workers:DocumentPreparation:ConcurrentRuns", "0", typeof(WorkersOptions))]
    [InlineData("Workers:DocumentPreparation:MaxAttempts", "0", typeof(WorkersOptions))]
    [InlineData("Workers:DocumentPreparation:RetryDelaySeconds", "-1", typeof(WorkersOptions))]
    [InlineData("Ai:PageCheck:Name", "", typeof(AiOptions))]
    [InlineData("Ai:FieldExtraction:Version", "", typeof(AiOptions))]
    [InlineData("Queue:HighValueThreshold", "0", typeof(QueueOptions))]
    public void The_api_does_not_start_with_a_wrong_setting(string key, string value, Type options)
    {
        using var factory = api.WithWebHostBuilder(builder => builder.UseSetting(key, value));

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Equal(options, error.OptionsType);
    }

    [Fact]
    public void The_shipped_limits_are_the_ones_the_database_allows()
    {
        var limits = new LimitsOptions();

        Assert.True(new LimitsOptionsValidator().Validate(null, limits).Succeeded);
        Assert.Equal(20 * 1024 * 1024, limits.MaxFileBytes);
        Assert.Equal(30, limits.MaxPagesPerClaim);
    }
}
