using ClaimLens.Api.AiJobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.Tests.AiJobs;

/// <summary>The Ai, Thresholds and Workers:AiJobs sections are checked when the API starts.</summary>
public class AiSettingsTests(ApiFactory api)
{
    [Theory]
    [InlineData("Ai:BaseUrl", "document-ai:8001", typeof(AiOptions))]
    [InlineData("Ai:ServiceKey", "", typeof(AiOptions))]
    [InlineData("Ai:PageCheck:TimeoutSeconds", "0", typeof(AiOptions))]
    [InlineData("Ai:FieldExtraction:TimeoutSeconds", "601", typeof(AiOptions))]
    [InlineData("Ai:PageReread:TimeoutSeconds", "0", typeof(AiOptions))]
    [InlineData("Thresholds:PageType", "1.5", typeof(ThresholdsOptions))]
    [InlineData("Thresholds:GeneralField", "0", typeof(ThresholdsOptions))]
    [InlineData("Thresholds:NeedsEntryBelow", "0.95", typeof(ThresholdsOptions))]
    [InlineData("Thresholds:BenefitMapping", "-0.1", typeof(ThresholdsOptions))]
    [InlineData("Workers:AiJobs:ConcurrentRuns", "0", typeof(WorkersOptions))]
    [InlineData("Workers:AiJobs:MaxAttempts", "0", typeof(WorkersOptions))]
    public void The_api_does_not_start_with_a_wrong_setting(string key, string value, Type options)
    {
        using var factory = api.WithWebHostBuilder(builder => builder.UseSetting(key, value));

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Equal(options, error.OptionsType);
    }

    [Fact]
    public void The_shipped_thresholds_are_the_ones_of_BR_03()
    {
        var thresholds = new ThresholdsOptions();

        Assert.True(new ThresholdsOptionsValidator().Validate(null, thresholds).Succeeded);
        Assert.Equal(
            (0.80m, 0.90m, 0.95m, 0.70m, 0.50m, 0.85m),
            (thresholds.PageType, thresholds.GeneralField, thresholds.MoneyAndIdentityField, thresholds.NeedsEntryBelow, thresholds.PageQuality, thresholds.BenefitMapping));
    }
}
