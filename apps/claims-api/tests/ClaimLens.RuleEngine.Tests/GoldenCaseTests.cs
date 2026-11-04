using ClaimLens.RuleEngine.Input;
using ClaimLens.RuleEngine.Output;

namespace ClaimLens.RuleEngine.Tests;

/// <summary>US11 and US12: the numbers in fixtures/golden, which the documents and the prototypes also use.</summary>
public class GoldenCaseTests
{
    [Theory]
    [InlineData("limit-capped")]
    [InlineData("excluded-line")]
    public void Totals_match_the_fixture(string scenario)
    {
        var (result, expected) = Run(scenario);

        Assert.Equal(expected.ClaimedTotal, result.ClaimedTotal);
        Assert.Equal(expected.EligibleTotal, result.EligibleTotal);
        Assert.Equal(expected.AfterRateAmount, result.AfterRateAmount);
        Assert.Equal(expected.PerVisitLimit, result.PerVisitLimit);
        Assert.Equal(expected.AnnualLimit, result.AnnualLimit);
        Assert.Equal(expected.RemainingLimit, result.RemainingLimit);
        Assert.Equal(expected.PayableAmount, result.PayableAmount);
        Assert.Equal(expected.ProjectedRemainingAfter, result.ProjectedRemainingAfter);
    }

    [Theory]
    [InlineData("limit-capped")]
    [InlineData("excluded-line")]
    public void Each_line_cites_the_rule_that_decided_it(string scenario)
    {
        var (result, expected) = Run(scenario);

        var actual = result.Lines.Select(line => (line.LineNo, line.EngineResult, line.EngineCoveredAmount, line.EngineRuleCode));
        var wanted = expected.Lines.Select(line => (line.LineNo, line.Result, line.Covered, line.RuleCode));
        Assert.Equal(wanted, actual);
    }

    [Theory]
    [InlineData("limit-capped")]
    [InlineData("excluded-line")]
    public void Formula_shows_each_step(string scenario)
    {
        var (result, expected) = Run(scenario);

        var actual = result.Formula.Select(row => (row.Label, row.RuleCode, row.Delta, row.Total));
        var wanted = expected.Formula.Select(row => (row.Label, row.RuleCode, row.Delta, row.Total));
        Assert.Equal(wanted, actual);
    }

    [Fact]
    public void Same_input_gives_the_same_result()
    {
        var input = Input("limit-capped");

        var first = RuleEvaluator.Evaluate(input);
        var second = RuleEvaluator.Evaluate(input with
        {
            Lines = input.Lines.Reverse().ToList(),
            Rules = input.Rules.Reverse().ToList(),
        });

        Assert.Equal(first.Lines, second.Lines);
        Assert.Equal(first.Formula, second.Formula);
        Assert.Equal(first.PayableAmount, second.PayableAmount);
    }

    internal static EvaluationInput Input(string scenario)
    {
        var file = GoldenFile.Scenario(scenario).Engine.Input;
        return new EvaluationInput(
            file.ServiceDate,
            file.FirstEnrolledOn,
            file.CoverageStart,
            file.CoverageEnd,
            file.ConsumedBefore,
            file.Lines
                .Select(line => new LineInput(GoldenFile.IdFor($"line-{line.LineNo}"), line.LineNo, line.CategoryCode, line.ClaimedAmount))
                .ToList(),
            GoldenFile.GoldOpRules());
    }

    private static (EvaluationResult Result, GoldenFile.EngineExpected Expected) Run(string scenario) =>
        (RuleEvaluator.Evaluate(Input(scenario)), GoldenFile.Scenario(scenario).Engine.Expected);
}
