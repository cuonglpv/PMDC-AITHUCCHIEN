namespace ClaimLens.RuleEngine.Input;

/// <summary>
/// Everything one evaluation needs: accepted values, one rule-set version and a balance.
/// A real claim and a synthetic sample claim produce the same shape, so a draft rule set is tested by the real engine (US20).
/// </summary>
/// <param name="ServiceDate">The date that selected the rule-set version (BR-04).</param>
/// <param name="FirstEnrolledOn">The start of waiting periods; earlier than the coverage start after a renewal.</param>
/// <param name="CoverageStart">Start of the policy year. Null for a sample claim, which has no policy.</param>
/// <param name="CoverageEnd">End of the policy year. Null for a sample claim, which has no policy.</param>
/// <param name="ConsumedBefore">Annual usage before this claim, in VND.</param>
/// <param name="Lines">Expense lines with their accepted category.</param>
/// <param name="Rules">Every rule of one rule-set version.</param>
public sealed record EvaluationInput(
    DateOnly ServiceDate,
    DateOnly FirstEnrolledOn,
    DateOnly? CoverageStart,
    DateOnly? CoverageEnd,
    long ConsumedBefore,
    IReadOnlyList<LineInput> Lines,
    IReadOnlyList<RuleInput> Rules);

/// <summary>One expense line. <paramref name="Override"/> is the reviewer's active override, if any (US16).</summary>
public sealed record LineInput(
    Guid LineId,
    int LineNo,
    string CategoryCode,
    long ClaimedAmount,
    LineOverride? Override = null);

/// <summary>A reviewer's override of one line. It changes the final result; the engine result stays visible.</summary>
public sealed record LineOverride(LineResult Result, long Amount);

/// <summary>One rule. Which optional values apply depends on <paramref name="Type"/>, as in the benefit_rule table.</summary>
public sealed record RuleInput(
    Guid RuleId,
    string RuleCode,
    RuleType Type,
    int ApplyOrder,
    string? CategoryCode = null,
    int? RateBp = null,
    long? LimitAmount = null,
    LimitScope? LimitScope = null,
    int? WaitingDays = null);
