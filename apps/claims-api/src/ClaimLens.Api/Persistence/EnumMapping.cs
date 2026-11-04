using Npgsql;
using Npgsql.NameTranslation;

namespace ClaimLens.Api.Persistence;

/// <summary>Enum members are PascalCase in C#, and UPPER_SNAKE or lower_snake in PostgreSQL as the DBML has them.</summary>
public static partial class EnumMapping
{
    private static readonly INpgsqlNameTranslator LowerSnake = new NpgsqlSnakeCaseNameTranslator();
    private static readonly INpgsqlNameTranslator UpperSnake = new UpperSnakeNameTranslator();

    private sealed class UpperSnakeNameTranslator : INpgsqlNameTranslator
    {
        private static readonly NpgsqlSnakeCaseNameTranslator Snake = new();

        public string TranslateTypeName(string clrName) => Snake.TranslateTypeName(clrName);

        public string TranslateMemberName(string clrName) => Snake.TranslateMemberName(clrName).ToUpperInvariant();
    }
}
