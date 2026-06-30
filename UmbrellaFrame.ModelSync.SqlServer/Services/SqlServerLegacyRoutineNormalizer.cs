using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UmbrellaFrame.ModelSync.SqlServer
{
    internal static class SqlServerLegacyRoutineNormalizer
    {
        private static readonly Regex GoPattern = new Regex(@"^\s*GO(?:\s+\d+)?\s*(?:--.*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SqlCmdPattern = new Regex(@"^\s*:(?:setvar|r)\b", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex ProcedureHeaderPattern = new Regex(@"\b(?:CREATE\s+OR\s+ALTER|CREATE|ALTER)\s+(?:PROCEDURE|PROC)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SetOptionPattern = new Regex(@"^\s*SET\s+(?:ANSI_NULLS|QUOTED_IDENTIFIER)\s+(?:ON|OFF)\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PrintOnlyPattern = new Regex(@"^\s*PRINT\s+(?:N)?'[^']*'\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

        public static string Normalize(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new InvalidOperationException("LegacyRoutineInvalidDefinition");
            if (SqlCmdPattern.IsMatch(sql))
                throw new InvalidOperationException("LegacyRoutineUnsupportedSqlCmd");

            var batches = SplitGoBatches(sql).Where(b => !string.IsNullOrWhiteSpace(b)).ToList();
            var procedureBatches = batches
                .Select(batch => new { Batch = batch, Match = ProcedureHeaderPattern.Match(batch) })
                .Where(x => x.Match.Success)
                .ToList();

            if (procedureBatches.Count == 0)
                throw new InvalidOperationException("LegacyRoutineInvalidDefinition");
            if (procedureBatches.Count > 1)
                throw new InvalidOperationException("LegacyRoutineMultipleDefinitions");

            foreach (var batch in batches)
            {
                if (ReferenceEquals(batch, procedureBatches[0].Batch))
                    continue;
                if (IsAllowedSideBatch(batch))
                    continue;
                throw new InvalidOperationException("LegacyRoutineExecutableSideBatch");
            }

            var procedure = procedureBatches[0].Batch.Substring(procedureBatches[0].Match.Index).Trim();
            if (!HasProcedureBody(procedure))
                throw new InvalidOperationException("LegacyRoutineInvalidDefinition");
            return procedure;
        }

        private static IReadOnlyList<string> SplitGoBatches(string sql)
        {
            var result = new List<string>();
            var current = new List<string>();
            foreach (var line in Regex.Split(sql ?? string.Empty, "\r\n|\n|\r"))
            {
                var go = GoPattern.Match(line);
                if (go.Success)
                {
                    if (Regex.IsMatch(line, @"\bGO\s+\d+\b", RegexOptions.IgnoreCase))
                        throw new InvalidOperationException("LegacyRoutineExecutableSideBatch");
                    result.Add(string.Join(Environment.NewLine, current));
                    current.Clear();
                    continue;
                }
                current.Add(line);
            }
            result.Add(string.Join(Environment.NewLine, current));
            return result;
        }

        private static bool IsAllowedSideBatch(string batch)
        {
            var stripped = StripComments(batch).Trim();
            if (string.IsNullOrWhiteSpace(stripped))
                return true;
            if (SetOptionPattern.IsMatch(stripped))
                return true;
            if (PrintOnlyPattern.IsMatch(stripped))
                return true;
            return false;
        }

        private static bool HasProcedureBody(string procedure)
            => Regex.IsMatch(procedure, @"\bAS\b", RegexOptions.IgnoreCase);

        private static string StripComments(string sql)
        {
            var noBlock = Regex.Replace(sql ?? string.Empty, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"^\s*--.*$", string.Empty, RegexOptions.Multiline);
        }
    }
}
