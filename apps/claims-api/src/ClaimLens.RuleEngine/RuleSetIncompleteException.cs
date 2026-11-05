namespace ClaimLens.RuleEngine;

/// <summary>
/// A line's category has no coverage or exclusion rule. Publishing forbids this, so only a draft under test can reach it.
/// The API answers CATEGORY_NOT_CONFIGURED.
/// </summary>
public sealed class RuleSetIncompleteException(string categoryCode)
    : Exception($"Category {categoryCode} is neither covered nor excluded by this rule set.")
{
    public string CategoryCode { get; } = categoryCode;
}
