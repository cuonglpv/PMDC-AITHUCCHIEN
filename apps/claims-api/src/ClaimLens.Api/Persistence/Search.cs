namespace ClaimLens.Api.Persistence;

/// <summary>
/// `immutable_unaccent` from 0004_search.sql, mapped in <see cref="ClaimLensDbContext"/>. Vietnamese names are searched
/// without their marks, so "Nguyen Van An" finds "Nguyễn Văn An" and the trigram index on the same expression is used.
/// </summary>
public static class Search
{
    public static string Unaccent(string text) =>
        throw new InvalidOperationException("Unaccent runs in the database only, inside a LINQ query.");
}

/// <summary>Text the user typed, used inside a LIKE pattern, matches itself and nothing more.</summary>
public static class Like
{
    public const string EscapeCharacter = "\\";

    public static string Escape(string text) =>
        text.Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter, StringComparison.Ordinal)
            .Replace("%", EscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", EscapeCharacter + "_", StringComparison.Ordinal);
}
