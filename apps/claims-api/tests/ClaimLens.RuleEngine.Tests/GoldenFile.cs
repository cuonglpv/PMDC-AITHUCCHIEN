using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaimLens.RuleEngine.Input;

namespace ClaimLens.RuleEngine.Tests;

/// <summary>Reads fixtures/golden, which every test suite shares. Ids are derived from names, so runs are repeatable.</summary>
internal static class GoldenFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static IReadOnlyList<RuleInput> GoldOpRules()
    {
        var file = Read<RuleSetFile>("rule-set-gold-op-v3.json");
        return file.Rules
            .Select(rule => new RuleInput(
                IdFor(rule.RuleCode),
                rule.RuleCode,
                rule.Type,
                rule.ApplyOrder,
                rule.CategoryCode,
                rule.RateBp,
                rule.LimitAmount,
                rule.LimitScope,
                rule.WaitingDays))
            .ToList();
    }

    public static ExpectedFile Scenario(string name) => Read<ExpectedFile>(Path.Combine(name, "expected.json"));

    public static Guid IdFor(string name) => new(MD5.HashData(Encoding.UTF8.GetBytes(name)));

    private static T Read<T>(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "golden", relativePath);
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options)
            ?? throw new InvalidOperationException($"Empty fixture: {path}");
    }

    public sealed record RuleSetFile(IReadOnlyList<RuleRow> Rules);

    public sealed record RuleRow(
        string RuleCode,
        RuleType Type,
        int ApplyOrder,
        string? CategoryCode,
        int? RateBp,
        long? LimitAmount,
        LimitScope? LimitScope,
        int? WaitingDays);

    public sealed record ExpectedFile(string Scenario, EngineCase Engine);

    public sealed record EngineCase(EngineInput Input, EngineExpected Expected);

    public sealed record EngineInput(
        DateOnly ServiceDate,
        DateOnly FirstEnrolledOn,
        DateOnly CoverageStart,
        DateOnly CoverageEnd,
        DateOnly? TerminatedOn,
        long ConsumedBefore,
        IReadOnlyList<EngineLine> Lines);

    public sealed record EngineLine(int LineNo, string CategoryCode, long ClaimedAmount);

    public sealed record EngineExpected(
        long ClaimedTotal,
        long EligibleTotal,
        long AfterRateAmount,
        long? PerVisitLimit,
        long? AnnualLimit,
        long RemainingLimit,
        long PayableAmount,
        long ProjectedRemainingAfter,
        IReadOnlyList<ExpectedLine> Lines,
        IReadOnlyList<ExpectedFormulaRow> Formula);

    public sealed record ExpectedLine(int LineNo, LineResult Result, long Covered, string RuleCode);

    public sealed record ExpectedFormulaRow(string Label, string? RuleCode, long? Delta, long Total);
}
