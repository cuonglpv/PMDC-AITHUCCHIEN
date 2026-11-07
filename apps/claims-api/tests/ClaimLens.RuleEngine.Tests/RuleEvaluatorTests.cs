using ClaimLens.RuleEngine.Input;

namespace ClaimLens.RuleEngine.Tests;

public class RuleEvaluatorTests
{
    private static readonly EvaluationInput Golden = GoldenCaseTests.Input("limit-capped");

    [Fact]
    public void Service_date_outside_coverage_excludes_every_line_and_cites_eligibility()
    {
        var result = RuleEvaluator.Evaluate(Golden with { ServiceDate = new DateOnly(2027, 1, 5) });

        Assert.All(result.Lines, line =>
        {
            Assert.Equal(LineResult.Excluded, line.EngineResult);
            Assert.Equal("ELIG-01", line.EngineRuleCode);
        });
        Assert.Equal(0, result.PayableAmount);
    }

    [Fact]
    public void A_sample_claim_has_no_policy_so_eligibility_passes()
    {
        var result = RuleEvaluator.Evaluate(Golden with
        {
            CoverageStart = null,
            CoverageEnd = null,
            TerminatedOn = null,
            ServiceDate = new DateOnly(2031, 1, 5),
        });

        Assert.Equal(1_100_000, result.EligibleTotal);
    }

    [Theory]
    [InlineData("2026-10-12", LineResult.Excluded, "ELIG-01")]
    [InlineData("2026-09-01", LineResult.Excluded, "ELIG-01")]
    [InlineData("2026-10-13", LineResult.Covered, "COV-01")]
    public void A_policy_is_not_in_force_on_or_after_the_day_it_ends(string terminatedOn, LineResult expected, string ruleCode)
    {
        var input = Golden with { TerminatedOn = DateOnly.Parse(terminatedOn, System.Globalization.CultureInfo.InvariantCulture) };

        var line = RuleEvaluator.Evaluate(input).Lines[0];

        Assert.Equal(expected, line.EngineResult);
        Assert.Equal(ruleCode, line.EngineRuleCode);
    }

    // docs/11-audit-and-roadmap.md, F-01: a coverage rule ordered first used to pay a line outside the policy year.
    [Fact]
    public void A_coverage_ordered_before_eligibility_cannot_pay_outside_the_policy_year()
    {
        var input = new EvaluationInput(
            ServiceDate: new DateOnly(2027, 2, 1),
            FirstEnrolledOn: new DateOnly(2026, 1, 1),
            CoverageStart: new DateOnly(2026, 1, 1),
            CoverageEnd: new DateOnly(2026, 12, 31),
            TerminatedOn: null,
            ConsumedBefore: 0,
            Lines: [new LineInput(Guid.Empty, 1, "CONSULTATION", 300_000)],
            Rules:
            [
                new RuleInput(Guid.Empty, "ELIG-01", RuleType.Eligibility, 0),
                new RuleInput(Guid.Empty, "COV-01", RuleType.Coverage, -5, "CONSULTATION"),
            ]);

        var result = RuleEvaluator.Evaluate(input);

        Assert.Equal(LineResult.Excluded, result.Lines[0].EngineResult);
        Assert.Equal("ELIG-01", result.Lines[0].EngineRuleCode);
        Assert.Equal(0, result.PayableAmount);
    }

    // F-01, second case: a coverage rule ordered before a waiting period used to pay a line still in the wait.
    [Fact]
    public void A_coverage_ordered_before_a_waiting_period_cannot_pay_during_the_wait()
    {
        var input = new EvaluationInput(
            ServiceDate: new DateOnly(2026, 10, 12),
            FirstEnrolledOn: new DateOnly(2026, 10, 1),
            CoverageStart: new DateOnly(2026, 10, 1),
            CoverageEnd: new DateOnly(2027, 9, 30),
            TerminatedOn: null,
            ConsumedBefore: 0,
            Lines: [new LineInput(Guid.Empty, 1, "CONSULTATION", 300_000)],
            Rules:
            [
                new RuleInput(Guid.Empty, "COV-01", RuleType.Coverage, 15, "CONSULTATION"),
                new RuleInput(Guid.Empty, "WAIT-30", RuleType.WaitingPeriod, 20, WaitingDays: 30),
            ]);

        var result = RuleEvaluator.Evaluate(input);

        Assert.Equal(LineResult.Excluded, result.Lines[0].EngineResult);
        Assert.Equal("WAIT-30", result.Lines[0].EngineRuleCode);
        Assert.Equal(0, result.PayableAmount);
    }

    [Fact]
    public void An_exclusion_ordered_first_still_cites_eligibility_when_the_policy_is_not_in_force()
    {
        var rules = Golden.Rules.Select(rule => rule.RuleCode == "EX-04" ? rule with { ApplyOrder = -10 } : rule).ToList();

        var result = RuleEvaluator.Evaluate(Golden with { Rules = rules, ServiceDate = new DateOnly(2027, 1, 5) });

        Assert.Equal("ELIG-01", result.Lines[3].EngineRuleCode);
    }

    [Theory]
    [InlineData("in force")]
    [InlineData("outside the policy year")]
    [InlineData("in the waiting period")]
    [InlineData("terminated")]
    public void Apply_order_across_kinds_changes_nothing(string situation)
    {
        var input = situation switch
        {
            "outside the policy year" => Golden with { ServiceDate = new DateOnly(2027, 1, 5) },
            "in the waiting period" => Golden with { FirstEnrolledOn = new DateOnly(2026, 10, 1) },
            "terminated" => Golden with { TerminatedOn = new DateOnly(2026, 10, 1) },
            _ => Golden,
        };
        var expected = RuleEvaluator.Evaluate(input);
        var reversed = input.Rules.Select(rule => rule with { ApplyOrder = 100 - rule.ApplyOrder }).ToList();

        var result = RuleEvaluator.Evaluate(input with { Rules = reversed });

        Assert.Equal(expected.Lines, result.Lines);
        Assert.Equal(expected.Formula, result.Formula);
        Assert.Equal(expected.PayableAmount, result.PayableAmount);
    }

    [Fact]
    public void Within_a_kind_apply_order_decides_which_rule_is_cited()
    {
        var rules = Golden.Rules
            .Append(new RuleInput(Guid.Empty, "WAIT-90", RuleType.WaitingPeriod, 15, "CONSULTATION", WaitingDays: 90))
            .ToList();
        var input = Golden with { Rules = rules, FirstEnrolledOn = new DateOnly(2026, 10, 1) };

        var result = RuleEvaluator.Evaluate(input);

        Assert.Equal("WAIT-90", result.Lines[0].EngineRuleCode);
        Assert.Equal("WAIT-30", result.Lines[1].EngineRuleCode);
    }

    [Theory]
    [InlineData("2026-09-30", LineResult.Covered, "COV-01")]
    [InlineData("2026-10-01", LineResult.Excluded, "WAIT-30")]
    public void Waiting_period_counts_from_first_enrolment(string enrolledOn, LineResult expected, string ruleCode)
    {
        var input = Golden with
        {
            ServiceDate = new DateOnly(2026, 10, 30),
            FirstEnrolledOn = DateOnly.Parse(enrolledOn, System.Globalization.CultureInfo.InvariantCulture),
        };

        var line = RuleEvaluator.Evaluate(input).Lines[0];

        Assert.Equal(expected, line.EngineResult);
        Assert.Equal(ruleCode, line.EngineRuleCode);
    }

    [Fact]
    public void Per_visit_limit_caps_when_it_is_the_lower_one()
    {
        var input = Golden with
        {
            ConsumedBefore = 0,
            Lines = [new LineInput(Guid.Empty, 1, "CONSULTATION", 4_000_000)],
        };

        var result = RuleEvaluator.Evaluate(input);

        Assert.Equal(3_200_000, result.AfterRateAmount);
        Assert.Equal(2_000_000, result.RemainingLimit);
        Assert.Equal(2_000_000, result.PayableAmount);
        Assert.Equal(3_000_000, result.ProjectedRemainingAfter);
        Assert.Equal("LIM-VISIT", result.Formula[^1].RuleCode);
        Assert.Equal("Capped by the per-visit limit", result.Formula[^1].Label);
    }

    [Fact]
    public void Exhausted_balance_pays_nothing_and_does_not_go_negative()
    {
        var result = RuleEvaluator.Evaluate(Golden with { ConsumedBefore = 5_300_000 });

        Assert.Equal(-300_000, result.RemainingLimit);
        Assert.Equal(0, result.PayableAmount);
        Assert.Equal(0, result.ProjectedRemainingAfter);
    }

    [Fact]
    public void Rate_rounds_down_to_a_whole_dong()
    {
        var input = Golden with { Lines = [new LineInput(Guid.Empty, 1, "CONSULTATION", 333_333)] };

        var result = RuleEvaluator.Evaluate(input);

        Assert.Equal(266_666, result.AfterRateAmount);
    }

    [Fact]
    public void Override_changes_the_final_result_and_the_engine_result_stays_visible()
    {
        var lines = Golden.Lines.ToList();
        lines[3] = lines[3] with { Override = new LineOverride(LineResult.PartiallyCovered, 100_000) };

        var result = RuleEvaluator.Evaluate(Golden with { Lines = lines, ConsumedBefore = 0 });

        var line = result.Lines[3];
        Assert.Equal(LineResult.Excluded, line.EngineResult);
        Assert.Equal("EX-04", line.EngineRuleCode);
        Assert.True(line.IsOverridden);
        Assert.Equal(LineResult.PartiallyCovered, line.FinalResult);
        Assert.Equal(100_000, line.FinalCoveredAmount);
        Assert.Equal(1_200_000, result.EligibleTotal);
        Assert.Contains(result.Formula, row => row.Label == "Override on line 4" && row.Delta == 100_000);
    }

    [Fact]
    public void An_override_is_still_capped_by_the_remaining_limit()
    {
        var lines = Golden.Lines.ToList();
        lines[3] = lines[3] with { Override = new LineOverride(LineResult.Covered, 300_000) };

        var result = RuleEvaluator.Evaluate(Golden with { Lines = lines });

        Assert.Equal(1_400_000, result.EligibleTotal);
        Assert.Equal(800_000, result.PayableAmount);
    }

    [Theory]
    [InlineData(LineResult.Covered, 100_000)]
    [InlineData(LineResult.Excluded, 1)]
    [InlineData(LineResult.PartiallyCovered, 0)]
    [InlineData(LineResult.PartiallyCovered, 300_000)]
    [InlineData(LineResult.PartiallyCovered, 300_001)]
    public void An_override_that_does_not_fit_its_result_is_refused(LineResult result, long amount)
    {
        var lines = Golden.Lines.ToList();
        lines[3] = lines[3] with { Override = new LineOverride(result, amount) };

        Assert.Throws<ArgumentException>(() => RuleEvaluator.Evaluate(Golden with { Lines = lines }));
    }

    [Fact]
    public void A_category_without_a_rule_is_reported_by_name()
    {
        var input = Golden with { Lines = [new LineInput(Guid.Empty, 1, "DENTAL", 100_000)] };

        var error = Assert.Throws<RuleSetIncompleteException>(() => RuleEvaluator.Evaluate(input));

        Assert.Equal("DENTAL", error.CategoryCode);
    }

    [Fact]
    public void No_limits_and_no_coinsurance_pays_the_eligible_total()
    {
        var input = Golden with
        {
            Rules = Golden.Rules.Where(rule => rule.Type is not (RuleType.Limit or RuleType.Coinsurance)).ToList(),
        };

        var result = RuleEvaluator.Evaluate(input);

        Assert.Equal(1_100_000, result.PayableAmount);
        Assert.Null(result.AnnualLimit);
        Assert.Equal(0, result.ProjectedRemainingAfter);
    }
}
