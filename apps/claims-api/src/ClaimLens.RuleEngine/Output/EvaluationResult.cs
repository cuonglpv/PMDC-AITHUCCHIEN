namespace ClaimLens.RuleEngine.Output;

/// <summary>The result of one evaluation. Its fields match calculation_revision and calculation_line.</summary>
public sealed record EvaluationResult(
    string EngineVersion,
    IReadOnlyList<LineOutcome> Lines,
    long ClaimedTotal,
    long EligibleTotal,
    long AfterRateAmount,
    long? PerVisitLimit,
    long? AnnualLimit,
    long ConsumedBefore,
    long RemainingLimit,
    long PayableAmount,
    long ProjectedRemainingAfter,
    IReadOnlyList<FormulaRow> Formula);

/// <summary>The engine result of one line next to its final result, so an override never hides what the rules said (US16).</summary>
public sealed record LineOutcome(
    Guid LineId,
    int LineNo,
    string CategoryCode,
    long ClaimedAmount,
    LineResult EngineResult,
    long EngineCoveredAmount,
    Guid EngineRuleId,
    string EngineRuleCode,
    bool IsOverridden,
    LineResult FinalResult,
    long FinalCoveredAmount);

/// <summary>One step of the payable formula shown on screen.</summary>
public sealed record FormulaRow(string Label, string? RuleCode, long? Delta, long Total);
