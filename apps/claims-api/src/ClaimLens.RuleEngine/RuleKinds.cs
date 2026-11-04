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

/// <summary>Same values as the limit_scope enum in the database.</summary>
public enum LimitScope
{
    PerVisit,
    Annual,
}
