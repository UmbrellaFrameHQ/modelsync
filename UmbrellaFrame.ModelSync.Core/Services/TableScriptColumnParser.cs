using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UmbrellaFrame.ModelSync.Core.Services
{
    /// <summary>Best-effort parser for simple CREATE TABLE column definitions.</summary>
    public static class TableScriptColumnParser
    {
        private static readonly Regex CreateTablePattern = new Regex(
            @"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<name>(?:\[[^\]]+\]|`[^`]+`|""[^""]+""|\w+)(?:\s*\.\s*(?:\[[^\]]+\]|`[^`]+`|""[^""]+""|\w+))?)\s*\((?<cols>[\s\S]*?)\)\s*(?:;|$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ColumnPattern = new Regex(
            @"^(?:\[(?<bracket>[^\]]+)\]|`(?<backtick>[^`]+)`|""(?<quote>[^""]+)""|(?<bare>\w+))\s+(?<definition>[\s\S]+)$",
            RegexOptions.Compiled);

        public static IReadOnlyList<TableColumnDefinition> Parse(string sql, string defaultSchema)
        {
            var result = new List<TableColumnDefinition>();
            foreach (Match tableMatch in CreateTablePattern.Matches(sql ?? string.Empty))
            {
                var (schema, table) = ParseObjectName(tableMatch.Groups["name"].Value, defaultSchema);
                foreach (var rawLine in SplitColumnLines(tableMatch.Groups["cols"].Value))
                {
                    var line = rawLine.Trim().TrimEnd(',').Trim();
                    if (string.IsNullOrWhiteSpace(line) || IsConstraintLine(line))
                        continue;

                    var colMatch = ColumnPattern.Match(line);
                    if (!colMatch.Success)
                        continue;

                    var column = First(colMatch, "bracket", "backtick", "quote", "bare");
                    var definition = colMatch.Groups["definition"].Value.Trim();
                    if (string.IsNullOrWhiteSpace(column) || string.IsNullOrWhiteSpace(definition))
                        continue;

                    result.Add(new TableColumnDefinition
                    {
                        Schema = schema,
                        Table = table,
                        Column = column,
                        Definition = definition
                    });
                }
            }

            return result;
        }

        private static IEnumerable<string> SplitColumnLines(string cols)
        {
            var depth = 0;
            var start = 0;
            for (var i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c == '(') depth++;
                else if (c == ')' && depth > 0) depth--;
                else if (c == ',' && depth == 0)
                {
                    yield return cols.Substring(start, i - start);
                    start = i + 1;
                }
            }

            if (start < cols.Length)
                yield return cols.Substring(start);
        }

        private static (string Schema, string Table) ParseObjectName(string raw, string defaultSchema)
        {
            var parts = raw.Split('.');
            if (parts.Length == 2)
                return (Unquote(parts[0]), Unquote(parts[1]));
            return (defaultSchema, Unquote(raw));
        }

        private static string Unquote(string value)
        {
            var text = (value ?? string.Empty).Trim();
            if ((text.StartsWith("[") && text.EndsWith("]")) ||
                (text.StartsWith("`") && text.EndsWith("`")) ||
                (text.StartsWith("\"") && text.EndsWith("\"")))
            {
                return text.Substring(1, text.Length - 2);
            }

            return text;
        }

        private static bool IsConstraintLine(string line)
        {
            var lower = line.TrimStart().ToLowerInvariant();
            return lower.StartsWith("constraint ") ||
                   lower.StartsWith("primary key") ||
                   lower.StartsWith("foreign key") ||
                   lower.StartsWith("unique ") ||
                   lower.StartsWith("check ") ||
                   lower.StartsWith("index ") ||
                   lower.StartsWith("key ");
        }

        private static string First(Match match, params string[] names)
        {
            foreach (var name in names)
            {
                var group = match.Groups[name];
                if (group.Success)
                    return group.Value;
            }

            return string.Empty;
        }
    }
}
