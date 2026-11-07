using ClaimLens.Api.Domain.Enums;
using ClaimLens.Api.Features.Evidence;
using Microsoft.Extensions.Options;

namespace ClaimLens.Api.AiJobs;

/// <summary>
/// The `Thresholds` section: BR-03 and the quality threshold of US04. Seed configuration with no UI in the MVP
/// (docs/03-use-cases.md). Checked when the API starts.
/// </summary>
public sealed class ThresholdsOptions
{
    /// <summary>A page type at or above this is used; below it the page is Unclassified.</summary>
    public decimal PageType { get; set; } = 0.80m;

    /// <summary>A field at or above this is an auto-accepted suggestion.</summary>
    public decimal GeneralField { get; set; } = 0.90m;

    /// <summary>The same for insured name, ID number, policy number, invoice total and line amount.</summary>
    public decimal MoneyAndIdentityField { get; set; } = 0.95m;

    /// <summary>Below this a field is "Needs entry": left empty, its AI value never shown.</summary>
    public decimal NeedsEntryBelow { get; set; } = 0.70m;

    /// <summary>A page whose quality score is below this is Low quality. Provisional until the golden set tunes it.</summary>
    public decimal PageQuality { get; set; } = 0.50m;

    /// <summary>Expense line to benefit category (US10). Declared now; Slice 4b applies it.</summary>
    public decimal BenefitMapping { get; set; } = 0.85m;
}

public sealed class ThresholdsOptionsValidator : IValidateOptions<ThresholdsOptions>
{
    public ValidateOptionsResult Validate(string? name, ThresholdsOptions options)
    {
        var failures = new List<string>();
        Check(nameof(options.PageType), options.PageType, failures);
        Check(nameof(options.GeneralField), options.GeneralField, failures);
        Check(nameof(options.MoneyAndIdentityField), options.MoneyAndIdentityField, failures);
        Check(nameof(options.NeedsEntryBelow), options.NeedsEntryBelow, failures);
        Check(nameof(options.PageQuality), options.PageQuality, failures);
        Check(nameof(options.BenefitMapping), options.BenefitMapping, failures);

        if (options.NeedsEntryBelow >= options.GeneralField || options.NeedsEntryBelow >= options.MoneyAndIdentityField)
        {
            failures.Add("Thresholds:NeedsEntryBelow must be below GeneralField and MoneyAndIdentityField, or nothing could need verification.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void Check(string key, decimal value, List<string> failures)
    {
        if (value is <= 0 or > 1)
        {
            failures.Add($"Thresholds:{key} must be above 0 and at most 1.");
        }
    }
}

/// <summary>
/// Turns a raw confidence into a level (BR-03). The only place thresholds are applied (C3 rule 4, held by an
/// architecture test): everything else reads the level the AI Job Runner stored.
/// </summary>
public sealed class Thresholds(IOptions<ThresholdsOptions> options)
{
    private ThresholdsOptions Values => options.Value;

    /// <summary>
    /// Confidence as the database keeps it, numeric(4,3), which rounds half away from zero. The level is decided on
    /// this value, so a stored 0.900 is never a CHECK.
    /// </summary>
    public static decimal Stored(decimal confidence) => Math.Round(confidence, 3, MidpointRounding.AwayFromZero);

    /// <summary>HIGH: the AI's type is used. CHECK: the page is Unclassified and a person chooses its type.</summary>
    public ConfidenceLevel PageType(decimal confidence) =>
        Stored(confidence) >= Values.PageType ? ConfidenceLevel.High : ConfidenceLevel.Check;

    public bool IsLowQuality(decimal qualityScore) => Stored(qualityScore) < Values.PageQuality;

    /// <summary>
    /// HIGH: an auto-accepted suggestion. CHECK: "Needs verification". MISSING: "Needs entry", for a value below the
    /// floor or no value at all.
    /// </summary>
    public ConfidenceLevel Field(FieldKey key, decimal confidence, bool hasValue)
    {
        if (!hasValue)
        {
            return ConfidenceLevel.Missing;
        }

        var stored = Stored(confidence);
        var accept = FieldCatalogue.GroupOf(key) == ThresholdGroup.MoneyAndIdentity ? Values.MoneyAndIdentityField : Values.GeneralField;
        return stored >= accept
            ? ConfidenceLevel.High
            : stored >= Values.NeedsEntryBelow ? ConfidenceLevel.Check : ConfidenceLevel.Missing;
    }
}
