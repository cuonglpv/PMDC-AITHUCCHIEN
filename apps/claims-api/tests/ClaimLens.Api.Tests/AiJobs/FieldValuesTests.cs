using ClaimLens.Api.Features.Evidence;

namespace ClaimLens.Api.Tests.AiJobs;

/// <summary>Money, dates and quantities as Vietnamese documents print them; anything unsure is not read.</summary>
public class FieldValuesTests
{
    [Theory]
    [InlineData("1.400.000", 1_400_000L)]
    [InlineData("1,400,000", 1_400_000L)]
    [InlineData("1 400 000 đ", 1_400_000L)]
    [InlineData("300000 VND", 300_000L)]
    [InlineData("300.000đ", 300_000L)]
    [InlineData("0", 0L)]
    [InlineData("1.400,000", null)]
    [InlineData("1.400.00", null)]
    [InlineData("-300.000", null)]
    [InlineData("1.400.000,50", null)]
    [InlineData("một triệu", null)]
    public void Money(string text, long? expected) => Assert.Equal(expected, FieldValues.Money(text));

    [Theory]
    [InlineData("12/10/2026", "2026-10-12")]
    [InlineData("2/3/2026", "2026-03-02")]
    [InlineData("12-10-2026", "2026-10-12")]
    [InlineData("2026-10-12", "2026-10-12")]
    [InlineData("Ngày 12 tháng 10 năm 2026", "2026-10-12")]
    [InlineData("10/13/2026", null)]
    [InlineData("31/02/2026", null)]
    [InlineData("12 Oct 2026", null)]
    public void Date(string text, string? expected) =>
        Assert.Equal(expected is null ? null : DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), FieldValues.Date(text));

    [Theory]
    [InlineData("2", "2")]
    [InlineData("1,5", "1.5")]
    [InlineData("10 viên", "10")]
    [InlineData("1.000", null)]
    [InlineData("0", null)]
    [InlineData("hai", null)]
    public void Quantity(string text, string? expected) =>
        Assert.Equal(expected is null ? null : decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), FieldValues.Quantity(text));
}
