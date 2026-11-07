using System.Globalization;
using ClaimLens.Api.Domain.Entities;
using ClaimLens.Api.Http;
using ClaimLens.RuleEngine;

namespace ClaimLens.Api.Features.RuleSets;

public sealed record RuleInput(
    string? RuleCode,
    string? RuleType,
    string? CategoryCode,
    string? ClauseText,
    string? ClauseRef,
    int? ApplyOrder,
    int? RateBp,
    long? LimitAmount,
    string? LimitScope,
    int? WaitingDays);

/// <summary>The result of checking a list of rules: one error code, with one entry per problem.</summary>
public sealed record RuleProblems(string Code, string Title, IReadOnlyList<FieldError> Errors);

/// <summary>What the order check needs to know of a rule.</summary>
public sealed record OrderedRule(string Code, RuleType Type, int Order);

/// <summary>A rule placed before a rule of a kind that applies first: its index in the list, and why.</summary>
public sealed record OrderProblem(int Index, string RuleCode, string Message);

public static class RuleSetRules
{
    public const string EligibilityCode = "ELIG-01";

    /// <summary>
    /// The order of the system rule, the same as in the seeded rule sets (db/seed/dev/030_plans_and_rules.sql). The seeded
    /// GOLD-OP v3 is published, so its rules are frozen; the API follows the seed rather than the other way round (F-01b).
    /// </summary>
    public const short EligibilityOrder = 10;

    public const string OrderTitle = "Each kind of rule applies in a fixed order";

    private static readonly Dictionary<string, RuleType> EditableTypes = new(StringComparer.Ordinal)
    {
        ["COVERAGE"] = RuleType.Coverage,
        ["EXCLUSION"] = RuleType.Exclusion,
        ["WAITING_PERIOD"] = RuleType.WaitingPeriod,
        ["COINSURANCE"] = RuleType.Coinsurance,
        ["LIMIT"] = RuleType.Limit,
    };

    private static readonly Dictionary<string, LimitScope> Scopes = new(StringComparer.Ordinal)
    {
        ["PER_VISIT"] = LimitScope.PerVisit,
        ["ANNUAL"] = LimitScope.Annual,
    };

    /// <summary>The rule the system adds to every version: the editor has no tab for it, and the API refuses it as input.</summary>
    public static BenefitRule Eligibility(Guid versionId) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            RuleSetVersionId = versionId,
            RuleCode = EligibilityCode,
            RuleType = RuleType.Eligibility,
            ClauseText = "The policy must be in force on the service date.",
            ApplyOrder = EligibilityOrder,
        };

    /// <summary>
    /// Checks in the order a person would fix them: the shape of each rule, the values its type needs, its category,
    /// repeats, then the order of application. Each stage reports all of its problems, so a list is not fixed one error at a time.
    /// <paramref name="system"/> is the version's eligibility rule, which the list does not contain.
    /// </summary>
    public static RuleProblems? Check(IReadOnlyList<RuleInput> rules, IReadOnlySet<string> activeCategories, OrderedRule system)
    {
        var shape = CheckShape(rules, system.Code);
        if (shape.Count > 0)
        {
            return new RuleProblems(ErrorCodes.ValidationFailed, "Check the highlighted fields", shape);
        }

        var values = CheckValues(rules);
        if (values.Count > 0)
        {
            return new RuleProblems(ErrorCodes.RuleInvalid, "A rule is missing the values of its type", values);
        }

        var categories = CheckCategories(rules, activeCategories);
        if (categories.Count > 0)
        {
            return new RuleProblems(ErrorCodes.UnknownCategory, "That is not a benefit category", categories);
        }

        var repeats = CheckRepeats(rules);
        if (repeats.Count > 0)
        {
            return new RuleProblems(ErrorCodes.ValidationFailed, "Check the highlighted fields", repeats);
        }

        var ordered = rules
            .Select(rule => new OrderedRule(rule.RuleCode!.Trim(), EditableTypes[rule.RuleType!], rule.ApplyOrder!.Value))
            .Append(system)
            .ToList();
        var order = OutOfOrder(ordered)
            .Select(problem => new FieldError($"rules[{problem.Index}].applyOrder", "OUT_OF_ORDER", problem.Message))
            .ToList();
        return order.Count > 0 ? new RuleProblems(ErrorCodes.RuleInvalid, OrderTitle, order) : null;
    }

    /// <summary>
    /// The engine applies rules by kind (<see cref="RulePrecedence"/>), and apply order only orders rules of one kind (F-01).
    /// So each rule must be ordered above every rule of a kind that applies before it; otherwise the editor would show an
    /// order the rules do not apply in. A rule that is not names the highest of those, which its order must pass.
    /// </summary>
    public static List<OrderProblem> OutOfOrder(IReadOnlyList<OrderedRule> rules)
    {
        var problems = new List<OrderProblem>();

        for (var index = 0; index < rules.Count; index++)
        {
            var rule = rules[index];
            var bound = rules
                .Where(other => RulePrecedence.Of(other.Type) < RulePrecedence.Of(rule.Type))
                .OrderByDescending(other => other.Order)
                .ThenBy(other => other.Code, StringComparer.Ordinal)
                .FirstOrDefault();

            if (bound is not null && rule.Order <= bound.Order)
            {
                problems.Add(new OrderProblem(
                    index,
                    rule.Code,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{Subject(rule.Type)} applies after {Kinds(bound.Type)}, so its order must be above {bound.Order} ({bound.Code}).")));
            }
        }

        return problems;
    }

    private static string Subject(RuleType type) => type switch
    {
        RuleType.WaitingPeriod => "A waiting period",
        RuleType.Exclusion => "An exclusion",
        RuleType.Coverage => "A coverage rule",
        RuleType.Coinsurance => "The reimbursement rate",
        _ => "A limit",
    };

    private static string Kinds(RuleType type) => type switch
    {
        RuleType.Eligibility => "the eligibility rule",
        RuleType.WaitingPeriod => "waiting periods",
        RuleType.Exclusion => "exclusions",
        RuleType.Coverage => "coverage rules",
        _ => "the reimbursement rate",
    };

    /// <summary>Keeps only the values a rule's type uses, so a type changed in the editor leaves nothing behind.</summary>
    public static BenefitRule ToEntity(RuleInput input, Guid versionId)
    {
        var type = EditableTypes[input.RuleType!];
        return new BenefitRule
        {
            Id = Guid.CreateVersion7(),
            RuleSetVersionId = versionId,
            RuleCode = input.RuleCode!.Trim(),
            RuleType = type,
            CategoryCode = UsesCategory(type) ? input.CategoryCode : null,
            ClauseText = input.ClauseText!.Trim(),
            ClauseRef = string.IsNullOrWhiteSpace(input.ClauseRef) ? null : input.ClauseRef.Trim(),
            ApplyOrder = (short)input.ApplyOrder!.Value,
            RateBp = type == RuleType.Coinsurance ? input.RateBp : null,
            LimitAmount = type == RuleType.Limit ? input.LimitAmount : null,
            LimitScope = type == RuleType.Limit ? Scopes[input.LimitScope!] : null,
            WaitingDays = type == RuleType.WaitingPeriod ? input.WaitingDays : null,
        };
    }

    private static bool UsesCategory(RuleType type) => type is RuleType.Coverage or RuleType.Exclusion or RuleType.WaitingPeriod;

    private static List<FieldError> CheckShape(IReadOnlyList<RuleInput> rules, string reservedCode)
    {
        var errors = new List<FieldError>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { reservedCode };

        for (var index = 0; index < rules.Count; index++)
        {
            var rule = rules[index];
            var at = $"rules[{index}]";
            var code = rule.RuleCode?.Trim();

            if (string.IsNullOrEmpty(code))
            {
                errors.Add(new FieldError($"{at}.ruleCode", "REQUIRED", "Enter a rule code."));
            }
            else if (code.Length > 20)
            {
                errors.Add(new FieldError($"{at}.ruleCode", "TOO_LONG", "A rule code can have at most 20 characters."));
            }
            else if (!seen.Add(code))
            {
                errors.Add(new FieldError($"{at}.ruleCode", "DUPLICATE", $"The rule code {code} is already used in this version."));
            }

            if (rule.RuleType is null || !EditableTypes.ContainsKey(rule.RuleType))
            {
                errors.Add(new FieldError($"{at}.ruleType", "OUT_OF_RANGE", "Choose a rule type."));
            }

            if (string.IsNullOrWhiteSpace(rule.ClauseText))
            {
                errors.Add(new FieldError($"{at}.clauseText", "REQUIRED", "Enter the clause text."));
            }

            if (rule.ClauseRef?.Length > 40)
            {
                errors.Add(new FieldError($"{at}.clauseRef", "TOO_LONG", "A clause number can have at most 40 characters."));
            }

            if (rule.CategoryCode?.Length > 40)
            {
                errors.Add(new FieldError($"{at}.categoryCode", "TOO_LONG", "A category code can have at most 40 characters."));
            }

            if (rule.ApplyOrder is null)
            {
                errors.Add(new FieldError($"{at}.applyOrder", "REQUIRED", "Enter the order of application."));
            }
            else if (rule.ApplyOrder is < short.MinValue or > short.MaxValue)
            {
                errors.Add(new FieldError($"{at}.applyOrder", "OUT_OF_RANGE", "That order of application is out of range."));
            }
        }

        return errors;
    }

    private static List<FieldError> CheckValues(IReadOnlyList<RuleInput> rules)
    {
        var errors = new List<FieldError>();

        for (var index = 0; index < rules.Count; index++)
        {
            var rule = rules[index];
            var at = $"rules[{index}]";

            switch (EditableTypes[rule.RuleType!])
            {
                case RuleType.Coverage or RuleType.Exclusion when string.IsNullOrEmpty(rule.CategoryCode):
                    errors.Add(new FieldError($"{at}.categoryCode", "REQUIRED", "Choose a benefit category."));
                    break;
                case RuleType.Coinsurance when rule.RateBp is not (>= 0 and <= 10_000):
                    errors.Add(new FieldError($"{at}.rateBp", "OUT_OF_RANGE", "Enter a reimbursement rate from 0% to 100%."));
                    break;
                case RuleType.Limit:
                    if (rule.LimitAmount is not > 0)
                    {
                        errors.Add(new FieldError($"{at}.limitAmount", "OUT_OF_RANGE", "Enter a limit above 0."));
                    }

                    if (rule.LimitScope is null || !Scopes.ContainsKey(rule.LimitScope))
                    {
                        errors.Add(new FieldError($"{at}.limitScope", "OUT_OF_RANGE", "Choose per visit or annual."));
                    }

                    break;
                case RuleType.WaitingPeriod when rule.WaitingDays is not >= 0:
                    errors.Add(new FieldError($"{at}.waitingDays", "OUT_OF_RANGE", "Enter the waiting period in days."));
                    break;
            }
        }

        return errors;
    }

    private static List<FieldError> CheckCategories(IReadOnlyList<RuleInput> rules, IReadOnlySet<string> activeCategories)
    {
        var errors = new List<FieldError>();

        for (var index = 0; index < rules.Count; index++)
        {
            var rule = rules[index];
            if (UsesCategory(EditableTypes[rule.RuleType!])
                && !string.IsNullOrEmpty(rule.CategoryCode)
                && !activeCategories.Contains(rule.CategoryCode))
            {
                errors.Add(new FieldError($"rules[{index}].categoryCode", "UNKNOWN", $"{rule.CategoryCode} is not a benefit category."));
            }
        }

        return errors;
    }

    /// <summary>
    /// The engine would quietly use the first or the lowest of these, so a repeat would look applied when it is not:
    /// one coverage or exclusion per category, one reimbursement rate, one limit per scope.
    /// </summary>
    private static List<FieldError> CheckRepeats(IReadOnlyList<RuleInput> rules)
    {
        var errors = new List<FieldError>();
        var categories = new HashSet<string>(StringComparer.Ordinal);
        var scopes = new HashSet<string>(StringComparer.Ordinal);
        var hasRate = false;

        for (var index = 0; index < rules.Count; index++)
        {
            var rule = rules[index];
            switch (EditableTypes[rule.RuleType!])
            {
                case RuleType.Coverage or RuleType.Exclusion when !categories.Add(rule.CategoryCode!):
                    errors.Add(new FieldError(
                        $"rules[{index}].categoryCode",
                        "DUPLICATE",
                        $"{rule.CategoryCode} already has a coverage or exclusion rule."));
                    break;
                case RuleType.Coinsurance when hasRate:
                    errors.Add(new FieldError($"rules[{index}].ruleType", "DUPLICATE", "A version has one reimbursement rate."));
                    break;
                case RuleType.Coinsurance:
                    hasRate = true;
                    break;
                case RuleType.Limit when !scopes.Add(rule.LimitScope!):
                    errors.Add(new FieldError($"rules[{index}].limitScope", "DUPLICATE", "A version has one limit for each scope."));
                    break;
            }
        }

        return errors;
    }
}
