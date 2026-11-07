namespace ClaimLens.RuleEngine;

/// <summary>Same values as the line_result enum in the database.</summary>
public enum LineResult
{
    Covered,
    PartiallyCovered,
    Excluded,
}

/// <summary>Same values as the rule_type enum in the database.</summary>
public enum RuleType
{
    Eligibility,
    Coverage,
    Exclusion,
    WaitingPeriod,
    Coinsurance,
    Limit,
}

/// <summary>
/// The order rule kinds apply in, whatever their apply_order (BR-05): a line is decided by eligibility, then waiting
/// periods, then exclusions, then coverage; the reimbursement rate and then the limits apply to the total.
/// apply_order only orders rules of the same kind.
/// </summary>
public static class RulePrecedence
{
    public static int Of(RuleType type) => type switch
    {
        RuleType.Eligibility => 0,
        RuleType.WaitingPeriod => 1,
        RuleType.Exclusion => 2,
        RuleType.Coverage => 3,
        RuleType.Coinsurance => 4,
        RuleType.Limit => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Not a rule type."),
    };
}

/// <summary>Same values as the limit_scope enum in the database.</summary>
public enum LimitScope
{
    PerVisit,
    Annual,
}
