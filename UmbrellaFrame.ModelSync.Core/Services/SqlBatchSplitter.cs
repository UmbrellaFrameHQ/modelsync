using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UmbrellaFrame.ModelSync.Core.Services
{
    /// <summary>Splits SQL scripts into executable batches.</summary>
    public static class SqlBatchSplitter
    {
        public static IReadOnlyList<string> SingleBatch(string sql)
            => new[] { sql };

        public static IReadOnlyList<string> SplitSqlServerGoBatches(string sql)
        {
            var batches = Regex.Split(sql ?? string.Empty, @"^\s*GO\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var result = new List<string>();
            foreach (var batch in batches)
            {
                if (!string.IsNullOrWhiteSpace(batch))
                    result.Add(batch);
            }

            return result;
        }
    }
}
