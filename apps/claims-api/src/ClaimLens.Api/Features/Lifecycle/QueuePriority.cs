using Microsoft.Extensions.Options;

namespace ClaimLens.Api.Features.Lifecycle;

/// <summary>Priority label for display and filtering (BR-09).</summary>
public enum Priority
{
    High,
    Normal,
}

/// <summary>The `Queue` section.</summary>
public sealed class QueueOptions
{
    /// <summary>BR-09: a claimed total at or above this is high value. The seed threshold is 5,000,000 VND.</summary>
    public long HighValueThreshold { get; set; } = 5_000_000;
}

public sealed class QueueOptionsValidator : IValidateOptions<QueueOptions>
{
    public ValidateOptionsResult Validate(string? name, QueueOptions options) =>
        options.HighValueThreshold > 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Queue:HighValueThreshold must be above 0.");
}

/// <summary>
/// BR-09 labels a claim High for a high value or an unresolved High flag. Risk flags arrive with US09/US14, so for now
/// the claimed total decides, which is also what the review queue measures before a payable exists.
/// </summary>
public sealed class QueuePriority(IOptions<QueueOptions> options)
{
    public long HighValueThreshold => options.Value.HighValueThreshold;

    public Priority Of(long claimedTotal) => claimedTotal >= HighValueThreshold ? Priority.High : Priority.Normal;
}
