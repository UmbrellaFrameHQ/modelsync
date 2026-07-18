using System;
using System.Text;

namespace UmbrellaFrame.ModelSync.Core
{
    public static class MigrationExecutionJsonReport
    {
        public static string Create(MigrationExecutionResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            var builder = new StringBuilder();
            builder.AppendLine("{");
            AppendProperty(builder, "state", result.State.ToString(), 1, comma: true);
            AppendProperty(builder, "succeeded", result.Succeeded, 1, comma: true);
            AppendProperty(builder, "atomicity", result.Atomicity.ToString(), 1, comma: true);
            AppendProperty(builder, "lockAcquired", result.LockAcquired, 1, comma: true);
            AppendProperty(builder, "transactionStarted", result.TransactionStarted, 1, comma: true);
            AppendProperty(builder, "historyWritten", result.HistoryWritten, 1, comma: true);
            AppendProperty(builder, "errorCode", result.ErrorCode, 1, comma: true);
            AppendProperty(builder, "errorMessage", result.ErrorMessage, 1, comma: true);
            AppendProperty(builder, "innerErrorMessage", result.InnerErrorMessage, 1, comma: true);
            AppendProperty(builder, "startedAt", result.StartedAt.ToString("O"), 1, comma: true);
            AppendProperty(builder, "completedAt", result.CompletedAt.ToString("O"), 1, comma: true);
            AppendProperty(builder, "durationMs", Math.Round(result.Duration.TotalMilliseconds, 2), 1, comma: true);

            Indent(builder, 1).AppendLine("\"items\": [");
            for (var index = 0; index < result.Items.Count; index++)
            {
                var item = result.Items[index];
                Indent(builder, 2).AppendLine("{");
                AppendProperty(builder, "category", item.Category.ToString(), 3, comma: true);
                AppendProperty(builder, "scriptId", item.ScriptId, 3, comma: true);
                AppendProperty(builder, "name", item.Name, 3, comma: true);
                AppendProperty(builder, "source", item.Source, 3, comma: true);
                AppendProperty(builder, "action", item.Action.ToString(), 3, comma: true);
                AppendProperty(builder, "executionMode", item.ExecutionMode.ToString(), 3, comma: true);
                AppendProperty(builder, "existingHash", item.ExistingHash, 3, comma: true);
                AppendProperty(builder, "targetHash", item.TargetHash, 3, comma: true);
                AppendProperty(builder, "startedAt", item.StartedAt.ToString("O"), 3, comma: true);
                AppendProperty(builder, "completedAt", item.CompletedAt.ToString("O"), 3, comma: true);
                AppendProperty(builder, "durationMs", Math.Round(item.Duration.TotalMilliseconds, 2), 3, comma: true);
                AppendProperty(builder, "batchCount", item.BatchCount, 3, comma: true);
                AppendProperty(builder, "completedBatchCount", item.CompletedBatchCount, 3, comma: true);
                AppendProperty(builder, "failureStage", item.FailureStage, 3, comma: true);
                AppendProperty(builder, "errorCode", item.ErrorCode, 3, comma: true);
                AppendProperty(builder, "errorMessage", item.ErrorMessage, 3, comma: true);
                AppendProperty(builder, "innerErrorMessage", item.InnerErrorMessage, 3, comma: true);
                AppendProperty(builder, "providerErrorCode", item.ProviderErrorCode, 3, comma: true);
                AppendNullable(builder, "providerErrorNumber", item.ProviderErrorNumber, 3, comma: true);
                AppendProperty(builder, "providerErrorState", item.ProviderErrorState, 3, comma: true);
                AppendProperty(builder, "providerErrorSeverity", item.ProviderErrorSeverity, 3, comma: true);
                AppendNullable(builder, "errorLineNumber", item.ErrorLineNumber, 3, comma: true);
                AppendProperty(builder, "errorObjectName", item.ErrorObjectName, 3, comma: true);
                AppendNullable(builder, "failedBatchIndex", item.FailedBatchIndex, 3, comma: true);
                AppendProperty(builder, "failedBatchPreview", BoundPreview(item.FailedBatchPreview), 3, comma: true);
                AppendProperty(builder, "decisionReason", item.DecisionReason, 3, comma: true);
                AppendProperty(builder, "legacyHashAdopted", item.LegacyHashAdopted, 3, comma: false);
                Indent(builder, 2).Append(index == result.Items.Count - 1 ? "}" : "},").AppendLine();
            }

            Indent(builder, 1).AppendLine("]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string BoundPreview(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var normalized = value.Replace("\r\n", "\n").Replace("\r", "\n");
            return normalized.Length <= 1024 ? normalized : normalized.Substring(0, 1024);
        }

        private static void AppendNullable(StringBuilder builder, string name, int? value, int level, bool comma)
        {
            Indent(builder, level)
                .Append('"')
                .Append(name)
                .Append("\": ")
                .Append(value.HasValue ? value.Value.ToString() : "null")
                .Append(comma ? "," : string.Empty)
                .AppendLine();
        }

        private static void AppendProperty(StringBuilder builder, string name, string value, int level, bool comma)
        {
            Indent(builder, level)
                .Append('"')
                .Append(name)
                .Append("\": \"")
                .Append(Escape(value))
                .Append('"')
                .Append(comma ? "," : string.Empty)
                .AppendLine();
        }

        private static void AppendProperty(StringBuilder builder, string name, bool value, int level, bool comma)
        {
            Indent(builder, level)
                .Append('"')
                .Append(name)
                .Append("\": ")
                .Append(value ? "true" : "false")
                .Append(comma ? "," : string.Empty)
                .AppendLine();
        }

        private static void AppendProperty(StringBuilder builder, string name, double value, int level, bool comma)
        {
            Indent(builder, level)
                .Append('"')
                .Append(name)
                .Append("\": ")
                .Append(value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Append(comma ? "," : string.Empty)
                .AppendLine();
        }

        private static void AppendProperty(StringBuilder builder, string name, int value, int level, bool comma)
        {
            Indent(builder, level)
                .Append('"')
                .Append(name)
                .Append("\": ")
                .Append(value)
                .Append(comma ? "," : string.Empty)
                .AppendLine();
        }

        private static StringBuilder Indent(StringBuilder builder, int level)
        {
            return builder.Append(new string(' ', level * 2));
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length + 8);
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (ch < ' ')
                            builder.Append("\\u").Append(((int)ch).ToString("x4"));
                        else
                            builder.Append(ch);
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
