using System.Globalization;
using System.Text.RegularExpressions;
using ClaimLens.Api.Domain.Enums;

namespace ClaimLens.Api.Features.Evidence;

/// <summary>A value as the claim_field columns hold it: the text, and the amount, date or quantity read from it.</summary>
public sealed record FieldValue(string Text, long? Amount, DateOnly? Date, decimal? Quantity);

/// <summary>
/// Reads money, dates and quantities as Vietnamese documents print them. It is strict on purpose: a value it cannot
/// read for certain is not guessed, and the field goes to a person instead.
/// </summary>
public static partial class FieldValues
{
    /// <summary>expense_line.quantity is numeric(10,2).</summary>
    private const decimal MaxQuantity = 99_999_999.99m;

    private static readonly string[] DateFormats = ["d/M/yyyy", "d-M-yyyy", "d.M.yyyy", "yyyy-MM-dd"];

    /// <summary>Null when the text is not a value of this field's kind.</summary>
    public static FieldValue? Read(FieldKey key, string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        return FieldCatalogue.KindOf(key) switch
        {
            FieldValueKind.Money => Money(trimmed) is { } amount ? new FieldValue(trimmed, amount, null, null) : null,
            FieldValueKind.Date => Date(trimmed) is { } date ? new FieldValue(trimmed, null, date, null) : null,
            FieldValueKind.Quantity => Quantity(trimmed) is { } quantity ? new FieldValue(trimmed, null, null, quantity) : null,
            _ => new FieldValue(trimmed, null, null, null),
        };
    }

    /// <summary>
    /// Whole VND: "1.400.000", "1,400,000", "1 400 000 đ", "300000 VND". Thousands are grouped by one separator in
    /// groups of three; anything else, such as a decimal part or a minus sign, is not read.
    /// </summary>
    public static long? Money(string text)
    {
        var bare = Currency().Replace(text, "").Trim();
        var match = GroupedDigits().Match(bare);
        if (!match.Success)
        {
            return null;
        }

        var separators = match.Groups["sep"].Captures.Select(capture => capture.Value).Distinct().Count();
        if (separators > 1)
        {
            return null;
        }

        var digits = string.Concat(bare.Where(char.IsAsciiDigit));
        return digits.Length <= 15 && long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : null;
    }

    /// <summary>Day first, as Vietnam writes it: "12/10/2026", "12-10-2026", "2026-10-12", "Ngày 12 tháng 10 năm 2026".</summary>
    public static DateOnly? Date(string text)
    {
        var words = VietnameseDate().Match(text);
        if (words.Success)
        {
            var day = int.Parse(words.Groups["d"].Value, CultureInfo.InvariantCulture);
            var month = int.Parse(words.Groups["m"].Value, CultureInfo.InvariantCulture);
            var year = int.Parse(words.Groups["y"].Value, CultureInfo.InvariantCulture);
            return month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
                ? new DateOnly(year, month, day)
                : null;
        }

        return DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    /// <summary>"2", "1.5", "1,5" or "10 viên": at most two decimals, with an optional unit after the number.</summary>
    public static decimal? Quantity(string text)
    {
        var match = QuantityText().Match(text);
        if (!match.Success)
        {
            return null;
        }

        var number = match.Groups["n"].Value.Replace(',', '.');
        return decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var quantity)
               && quantity is > 0 and <= MaxQuantity
            ? quantity
            : null;
    }

    [GeneratedRegex(@"(?:VNĐ|VND|đồng|đ|₫)\.?", RegexOptions.IgnoreCase)]
    private static partial Regex Currency();

    [GeneratedRegex(@"^\d{1,3}(?:(?<sep>[.,  ])\d{3})+$|^\d+$")]
    private static partial Regex GroupedDigits();

    [GeneratedRegex(@"^(?:ngày\s+)?(?<d>\d{1,2})\s+tháng\s+(?<m>\d{1,2})\s+năm\s+(?<y>\d{4})$", RegexOptions.IgnoreCase)]
    private static partial Regex VietnameseDate();

    [GeneratedRegex(@"^(?<n>\d{1,8}(?:[.,]\d{1,2})?)(?:\s*\p{L}+)?$")]
    private static partial Regex QuantityText();
}
