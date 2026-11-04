using System.Globalization;
using ClaimLens.RuleEngine.Input;
using ClaimLens.RuleEngine.Output;

namespace ClaimLens.RuleEngine;

/// <summary>
/// Evaluates expense lines against one rule-set version and calculates the payable amount (BR-05).
/// A pure function: no clock, database or network, so the same input always gives the same result (US11).
/// </summary>
public static class RuleEvaluator
{
    /// <summary>Stored on each calculation revision. Raise it whenever a change could alter a result.</summary>
    public const string EngineVersion = "1.0.0";

    private const int FullRateBp = 10_000;

    public static EvaluationResult Evaluate(EvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var rules = input.Rules
            .OrderBy(rule => rule.ApplyOrder)
            .ThenBy(rule => rule.RuleCode, StringComparer.Ordinal)
            .ToList();

        var lines = input.Lines
            .OrderBy(line => line.LineNo)
            .Select(line => EvaluateLine(line, input, rules))
            .ToList();

        var claimedTotal = lines.Sum(line => line.ClaimedAmount);
        var eligibleTotal = lines.Sum(line => line.FinalCoveredAmount);

        var formula = new List<FormulaRow> { new("Claimed", null, null, claimedTotal) };
        AddExclusionRows(formula, lines);
        AddOverrideRows(formula, lines);

        var afterRate = ApplyCoinsurance(eligibleTotal, rules, formula);
        var limits = ReadLimits(rules, input.ConsumedBefore);
        var remainingLimit = limits.Remaining ?? afterRate;
        var payable = Math.Min(afterRate, Math.Max(remainingLimit, 0));

        if (payable < afterRate)
        {
            var label = limits.BindingRule!.LimitScope == LimitScope.PerVisit
                ? "Capped by the per-visit limit"
                : "Capped by remaining balance";
            formula.Add(new FormulaRow(label, limits.BindingRule.RuleCode, payable - afterRate, payable));
        }

        var balanceBefore = limits.Annual is { } annual ? annual - input.ConsumedBefore : remainingLimit;

        return new EvaluationResult(
            EngineVersion,
            lines,
            claimedTotal,
            eligibleTotal,
            afterRate,
            limits.PerVisit,
            limits.Annual,
            input.ConsumedBefore,
            remainingLimit,
            payable,
            Math.Max(balanceBefore - payable, 0),
            formula);
    }

    private static LineOutcome EvaluateLine(LineInput line, EvaluationInput input, List<RuleInput> rules)
    {
        if (line.ClaimedAmount < 0)
        {
            throw new ArgumentException($"Line {line.LineNo} has a negative amount.", nameof(input));
        }

        var (engineResult, engineCovered, decidingRule) = DecideLine(line, input, rules);

        var final = line.Override;
        if (final is not null)
        {
            CheckOverride(line, final);
        }

        return new LineOutcome(
            line.LineId,
            line.LineNo,
            line.CategoryCode,
            line.ClaimedAmount,
            engineResult,
            engineCovered,
            decidingRule.RuleId,
            decidingRule.RuleCode,
            final is not null,
            final?.Result ?? engineResult,
            final?.Amount ?? engineCovered);
    }

    /// <summary>
    /// The first rule in apply order that decides the line wins. No rule kind pays part of a line,
    /// so PartiallyCovered comes only from a reviewer's override.
    /// </summary>
    private static (LineResult Result, long Covered, RuleInput Rule) DecideLine(
        LineInput line,
        EvaluationInput input,
        List<RuleInput> rules)
    {
        foreach (var rule in rules)
        {
            var excludes = rule.Type switch
            {
                RuleType.Eligibility => !IsInForce(input),
                RuleType.WaitingPeriod => AppliesTo(rule, line) && IsWaiting(rule, input),
                RuleType.Exclusion => AppliesTo(rule, line),
                _ => false,
            };

            if (excludes)
            {
                return (LineResult.Excluded, 0, rule);
            }

            if (rule.Type == RuleType.Coverage && AppliesTo(rule, line))
            {
                return (LineResult.Covered, line.ClaimedAmount, rule);
            }
        }

        throw new RuleSetIncompleteException(line.CategoryCode);
    }

    private static bool IsInForce(EvaluationInput input) =>
        (input.CoverageStart is null || input.ServiceDate >= input.CoverageStart)
        && (input.CoverageEnd is null || input.ServiceDate <= input.CoverageEnd);

    private static bool IsWaiting(RuleInput rule, EvaluationInput input) =>
        input.ServiceDate < input.FirstEnrolledOn.AddDays(rule.WaitingDays ?? 0);

    private static bool AppliesTo(RuleInput rule, LineInput line) =>
        rule.CategoryCode is null || rule.CategoryCode == line.CategoryCode;

    /// <summary>The same limits as ck_override_cap and ck_override_result, so a bug fails here first.</summary>
    private static void CheckOverride(LineInput line, LineOverride change)
    {
        var fits = change.Result switch
        {
            LineResult.Covered => change.Amount == line.ClaimedAmount,
            LineResult.Excluded => change.Amount == 0,
            LineResult.PartiallyCovered => change.Amount >= 1 && change.Amount <= line.ClaimedAmount - 1,
            _ => false,
        };

        if (!fits)
        {
            throw new ArgumentException($"The override on line {line.LineNo} does not fit its result.", nameof(line));
        }
    }

    private static void AddExclusionRows(List<FormulaRow> formula, List<LineOutcome> lines)
    {
        var total = formula[^1].Total;
        var excluded = lines
            .Where(line => line.EngineResult == LineResult.Excluded && line.ClaimedAmount > 0)
            .GroupBy(line => line.EngineRuleCode, StringComparer.Ordinal)
            .OrderBy(group => group.Min(line => line.LineNo));

        foreach (var group in excluded)
        {
            var amount = group.Sum(line => line.ClaimedAmount);
            total -= amount;
            formula.Add(new FormulaRow("Excluded", group.Key, -amount, total));
        }
    }

    private static void AddOverrideRows(List<FormulaRow> formula, List<LineOutcome> lines)
    {
        var total = formula[^1].Total;

        foreach (var line in lines.Where(line => line.IsOverridden && line.FinalCoveredAmount != line.EngineCoveredAmount))
        {
            var delta = line.FinalCoveredAmount - line.EngineCoveredAmount;
            total += delta;
            formula.Add(new FormulaRow(
                string.Create(CultureInfo.InvariantCulture, $"Override on line {line.LineNo}"),
                null,
                delta,
                total));
        }
    }

    /// <summary>The amount is rounded down to a whole dong, so the insurer never pays above the stated rate.</summary>
    private static long ApplyCoinsurance(long eligibleTotal, List<RuleInput> rules, List<FormulaRow> formula)
    {
        var rule = rules.FirstOrDefault(candidate => candidate.Type == RuleType.Coinsurance);
        if (rule?.RateBp is not { } rateBp || rateBp == FullRateBp)
        {
            return eligibleTotal;
        }

        var afterRate = checked(eligibleTotal * rateBp) / FullRateBp;
        var percent = (rateBp / 100m).ToString("0.##", CultureInfo.InvariantCulture);
        formula.Add(new FormulaRow($"Coinsurance {percent}%", rule.RuleCode, afterRate - eligibleTotal, afterRate));
        return afterRate;
    }

    private static Limits ReadLimits(List<RuleInput> rules, long consumedBefore)
    {
        var perVisit = LowestLimit(rules, LimitScope.PerVisit);
        var annual = LowestLimit(rules, LimitScope.Annual);
        var annualLeft = annual?.LimitAmount - consumedBefore;

        return (perVisit, annual) switch
        {
            (null, null) => new Limits(null, null, null, null),
            (not null, null) => new Limits(perVisit.LimitAmount, null, perVisit.LimitAmount, perVisit),
            (null, not null) => new Limits(null, annual.LimitAmount, annualLeft, annual),
            _ when perVisit!.LimitAmount < annualLeft => new Limits(perVisit.LimitAmount, annual!.LimitAmount, perVisit.LimitAmount, perVisit),
            _ => new Limits(perVisit!.LimitAmount, annual!.LimitAmount, annualLeft, annual),
        };
    }

    private static RuleInput? LowestLimit(List<RuleInput> rules, LimitScope scope) =>
        rules
            .Where(rule => rule.Type == RuleType.Limit && rule.LimitScope == scope && rule.LimitAmount is not null)
            .OrderBy(rule => rule.LimitAmount)
            .ThenBy(rule => rule.RuleCode, StringComparer.Ordinal)
            .FirstOrDefault();

    private sealed record Limits(long? PerVisit, long? Annual, long? Remaining, RuleInput? BindingRule);
}
