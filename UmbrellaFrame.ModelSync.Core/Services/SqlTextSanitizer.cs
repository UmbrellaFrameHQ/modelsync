using System;
using System.Text;

namespace UmbrellaFrame.ModelSync.Core.Services
{
    internal static class SqlTextSanitizer
    {
        public static string StripComments(string sql)
            => Transform(sql, redactLiterals: false, stripComments: true);

        public static string RedactLiteralsAndComments(string sql)
            => Transform(sql, redactLiterals: true, stripComments: true);

        private static string Transform(string sql, bool redactLiterals, bool stripComments)
        {
            if (string.IsNullOrEmpty(sql))
                return string.Empty;

            var builder = new StringBuilder(sql.Length);
            for (var i = 0; i < sql.Length;)
            {
                if (TryReadOracleQuotedLiteral(sql, i, out var oracleEnd))
                {
                    AppendToken(builder, sql, i, oracleEnd, redactLiterals, "q'<redacted>'");
                    i = oracleEnd;
                    continue;
                }

                if (TryReadDollarQuotedLiteral(sql, i, out var dollarEnd))
                {
                    AppendToken(builder, sql, i, dollarEnd, redactLiterals, "$<redacted>$");
                    i = dollarEnd;
                    continue;
                }

                var current = sql[i];
                if (current == '\'' || current == '"' || current == '`' || current == '[')
                {
                    var end = ReadQuotedToken(sql, i, current);
                    var isValueLiteral = current == '\'' || (redactLiterals && current == '"');
                    AppendToken(builder, sql, i, end, redactLiterals && isValueLiteral, "'<redacted>'");
                    i = end;
                    continue;
                }

                if (stripComments && current == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
                {
                    builder.Append(' ');
                    i += 2;
                    while (i < sql.Length && sql[i] != '\n')
                        i++;
                    continue;
                }

                if (stripComments && current == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
                {
                    builder.Append(' ');
                    i = ReadBlockComment(sql, i + 2);
                    continue;
                }

                if (redactLiterals && char.IsDigit(current) && IsTokenBoundary(sql, i - 1))
                {
                    builder.Append("<number>");
                    i = ReadNumericToken(sql, i);
                    continue;
                }

                builder.Append(current);
                i++;
            }

            return builder.ToString();
        }

        private static void AppendToken(StringBuilder builder, string sql, int start, int end, bool redact, string replacement)
        {
            if (redact)
                builder.Append(replacement);
            else
                builder.Append(sql, start, end - start);
        }

        private static int ReadQuotedToken(string sql, int start, char opener)
        {
            var closer = opener == '[' ? ']' : opener;
            var i = start + 1;
            while (i < sql.Length)
            {
                if (sql[i] == '\\' && opener != '[' && i + 1 < sql.Length)
                {
                    i += 2;
                    continue;
                }

                if (sql[i] == closer)
                {
                    if (i + 1 < sql.Length && sql[i + 1] == closer)
                    {
                        i += 2;
                        continue;
                    }

                    return i + 1;
                }

                i++;
            }

            return sql.Length;
        }

        private static bool TryReadDollarQuotedLiteral(string sql, int start, out int end)
        {
            end = start;
            if (sql[start] != '$')
                return false;

            var tagEnd = start + 1;
            while (tagEnd < sql.Length && (char.IsLetterOrDigit(sql[tagEnd]) || sql[tagEnd] == '_'))
                tagEnd++;
            if (tagEnd >= sql.Length || sql[tagEnd] != '$')
                return false;

            var delimiter = sql.Substring(start, tagEnd - start + 1);
            var close = sql.IndexOf(delimiter, tagEnd + 1, StringComparison.Ordinal);
            end = close < 0 ? sql.Length : close + delimiter.Length;
            return true;
        }

        private static bool TryReadOracleQuotedLiteral(string sql, int start, out int end)
        {
            end = start;
            if (start + 2 >= sql.Length || (sql[start] != 'q' && sql[start] != 'Q') || sql[start + 1] != '\'')
                return false;

            var opener = sql[start + 2];
            var closer = opener == '[' ? ']' : opener == '{' ? '}' : opener == '(' ? ')' : opener == '<' ? '>' : opener;
            var close = sql.IndexOf(closer.ToString() + "'", start + 3, StringComparison.Ordinal);
            end = close < 0 ? sql.Length : close + 2;
            return true;
        }

        private static int ReadBlockComment(string sql, int start)
        {
            var depth = 1;
            var i = start;
            while (i < sql.Length && depth > 0)
            {
                if (i + 1 < sql.Length && sql[i] == '/' && sql[i + 1] == '*')
                {
                    depth++;
                    i += 2;
                }
                else if (i + 1 < sql.Length && sql[i] == '*' && sql[i + 1] == '/')
                {
                    depth--;
                    i += 2;
                }
                else
                {
                    i++;
                }
            }

            return i;
        }

        private static int ReadNumericToken(string sql, int start)
        {
            var i = start;
            if (i + 1 < sql.Length && sql[i] == '0' && (sql[i + 1] == 'x' || sql[i + 1] == 'X'))
            {
                i += 2;
                while (i < sql.Length && Uri.IsHexDigit(sql[i]))
                    i++;
                return i;
            }

            while (i < sql.Length && (char.IsDigit(sql[i]) || sql[i] == '.' || sql[i] == 'e' || sql[i] == 'E' || sql[i] == '+' || sql[i] == '-'))
                i++;
            return i;
        }

        private static bool IsTokenBoundary(string sql, int index)
            => index < 0 || !(char.IsLetterOrDigit(sql[index]) || sql[index] == '_' || sql[index] == '$');
    }
}
