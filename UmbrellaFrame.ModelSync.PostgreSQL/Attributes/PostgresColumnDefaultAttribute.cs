using UmbrellaFrame.ModelSync.Core;

namespace UmbrellaFrame.ModelSync.PostgreSQL
{
    public enum PostgresDefaultExpression
    {
        GenRandomUuid,
        CurrentTimestamp,
        Now
    }

    public sealed class PostgresColumnDefaultAttribute : DbColumnDefaultAttribute
    {
        public PostgresColumnDefaultAttribute(string literalValue)
            : base(literalValue)
        {
        }

        public PostgresColumnDefaultAttribute(PostgresDefaultExpression expression)
            : base(ToSql(expression))
        {
        }

        private static string ToSql(PostgresDefaultExpression expression)
        {
            switch (expression)
            {
                case PostgresDefaultExpression.GenRandomUuid:
                    return "gen_random_uuid()";
                case PostgresDefaultExpression.CurrentTimestamp:
                    return "CURRENT_TIMESTAMP";
                case PostgresDefaultExpression.Now:
                    return "NOW()";
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(expression), expression, null);
            }
        }
    }
}
