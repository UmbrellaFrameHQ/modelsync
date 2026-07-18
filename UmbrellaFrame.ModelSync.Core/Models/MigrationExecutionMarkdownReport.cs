using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace UmbrellaFrame.ModelSync.Core
{
    public static class MigrationExecutionMarkdownReport
    {
        public static string Create(MigrationExecutionResult result, string title = "ModelSync Migration Report")
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            var builder = new StringBuilder();
            builder.Append("# ").AppendLine(Sanitize(title));
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.Append("| State | Succeeded | Atomicity | Lock Acquired | Transaction Started | History Written | Duration |").AppendLine();
            builder.Append("|---|---:|---|---:|---:|---:|---:|").AppendLine();
            builder.Append("| ")
                .Append(result.State)
                .Append(" | ")
                .Append(result.Succeeded ? "Yes" : "No")
                .Append(" | ")
                .Append(result.Atomicity)
                .Append(" | ")
                .Append(result.LockAcquired ? "Yes" : "No")
                .Append(" | ")
                .Append(result.TransactionStarted ? "Yes" : "No")
                .Append(" | ")
                .Append(result.HistoryWritten ? "Yes" : "No")
                .Append(" | ")
                .Append(FormatDuration(result.Duration))
                .AppendLine(" |");

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage) || !string.IsNullOrWhiteSpace(result.ErrorCode))
            {
                builder.AppendLine();
                builder.AppendLine("## Root Failure");
                builder.AppendLine();
                builder.Append("- Error code: ").AppendLine(Sanitize(result.ErrorCode));
                builder.Append("- Message: ").AppendLine(Sanitize(result.ErrorMessage));
                if (!string.IsNullOrWhiteSpace(result.InnerErrorMessage))
                    builder.Append("- Inner message: ").AppendLine(Sanitize(result.InnerErrorMessage));
            }

            builder.AppendLine();
            builder.AppendLine("## Counts");
            builder.AppendLine();
            builder.Append("| Action | Count |").AppendLine();
            builder.Append("|---|---:|").AppendLine();

            foreach (var group in result.Items.GroupBy(i => i.Action).OrderBy(g => g.Key.ToString()))
            {
                builder.Append("| ")
                    .Append(group.Key)
                    .Append(" | ")
                    .Append(group.Count())
                    .AppendLine(" |");
            }

            if (result.Items.Count == 0)
                builder.AppendLine("| None | 0 |");

            builder.AppendLine();
            builder.AppendLine("## Items");
            builder.AppendLine();
            builder.Append("| Category | Script | Action | Mode | Batches | Duration | Error |").AppendLine();
            builder.Append("|---|---|---|---|---:|---:|---|").AppendLine();

            foreach (var item in result.Items)
            {
                builder.Append("| ")
                    .Append(item.Category)
                    .Append(" | ")
                    .Append(Sanitize(Prefer(item.Name, item.ScriptId, item.Source)))
                    .Append(" | ")
                    .Append(item.Action)
                    .Append(" | ")
                    .Append(item.ExecutionMode)
                    .Append(" | ")
                    .Append(item.CompletedBatchCount)
                    .Append("/")
                    .Append(item.BatchCount)
                    .Append(" | ")
                    .Append(FormatDuration(item.Duration))
                    .Append(" | ")
                    .Append(Sanitize(FirstNonEmpty(item.ErrorCode, item.ProviderErrorCode, item.ErrorMessage)))
                    .AppendLine(" |");
            }

            var failures = result.Items
                .Where(i => i.Action == MigrationExecutionAction.Failed || !string.IsNullOrWhiteSpace(i.ErrorMessage))
                .ToList();

            if (failures.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Failures");

                foreach (var item in failures)
                {
                    builder.AppendLine();
                    builder.Append("### ").AppendLine(Sanitize(Prefer(item.Name, item.ScriptId, item.Source)));
                    builder.AppendLine();
                    builder.Append("- Category: ").AppendLine(item.Category.ToString());
                    builder.Append("- Failed batch: ").AppendLine(item.FailedBatchIndex?.ToString() ?? string.Empty);
                    builder.Append("- Stage: ").AppendLine(Sanitize(item.FailureStage));
                    builder.Append("- Provider code: ").AppendLine(Sanitize(FirstNonEmpty(item.ProviderErrorCode, item.ProviderErrorNumber?.ToString() ?? string.Empty)));
                    builder.Append("- Message: ").AppendLine(Sanitize(item.ErrorMessage));

                    if (!string.IsNullOrWhiteSpace(item.FailedBatchPreview))
                    {
                        builder.AppendLine();
                        builder.AppendLine("```sql");
                        builder.AppendLine(SanitizePreview(item.FailedBatchPreview));
                        builder.AppendLine("```");
                    }
                }
            }

            return builder.ToString();
        }

        private static string Prefer(params string[] values)
        {
            return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }

        private static string FormatDuration(TimeSpan duration)
        {
            return duration.TotalMilliseconds < 1000
                ? duration.TotalMilliseconds.ToString("0.##", CultureInfo.InvariantCulture) + " ms"
                : duration.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture) + " s";
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("|", "\\|")
                .Trim();
        }

        private static string SanitizePreview(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var normalized = value.Replace("\r\n", "\n").Replace("\r", "\n");
            return normalized.Length <= 1024 ? normalized : normalized.Substring(0, 1024);
        }
    }
}
