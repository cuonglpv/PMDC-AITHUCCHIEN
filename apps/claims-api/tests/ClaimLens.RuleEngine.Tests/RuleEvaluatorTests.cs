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
        var result = RuleEvaluator.Evaluate(Golden with { CoverageStart = null, CoverageEnd = null, ServiceDate = new DateOnly(2031, 1, 5) });

        Assert.Equal(1_100_000, result.EligibleTotal);
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
