using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UmbrellaFrame.ModelSync.Core.SqlGeneration;

namespace UmbrellaFrame.ModelSync.Core.Services
{
    public sealed class StoredProcedureSqlPlanner
    {
        private static readonly Regex SafeIdentifierPattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private static readonly Regex BatchSeparatorPattern = new Regex(@"^\s*GO\s*(?:--.*)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private readonly ModelSyncSqlDialect _dialect;
        private readonly ModelSyncProviderDescriptor _descriptor;

        public StoredProcedureSqlPlanner(ModelSyncProviderDescriptor descriptor)
        {
            _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            _dialect = new ModelSyncSqlDialect(descriptor);
        }

        public StoredProcedureSyncPlan BuildPlan(StoredProcedureDefinition definition, string? currentSql)
        {
            ValidateDefinition(definition);
            var applySql = BuildApplySql(definition);
            var targetHash = SqlDefinitionNormalizer.ComputeHash(BuildComparableSql(definition.Sql));

            if (string.IsNullOrWhiteSpace(currentSql))
            {
                return new StoredProcedureSyncPlan
                {
                    Definition = definition,
                    ChangeType = StoredProcedureChangeType.Create,
                    TargetHash = targetHash,
                    SqlToApply = applySql
                };
            }

            var currentHash = SqlDefinitionNormalizer.ComputeHash(BuildComparableSql(currentSql));
            var hasChanges = !string.Equals(currentHash, targetHash, StringComparison.Ordinal);
            return new StoredProcedureSyncPlan
            {
                Definition = definition,
                ChangeType = hasChanges ? StoredProcedureChangeType.Alter : StoredProcedureChangeType.None,
                CurrentHash = currentHash,
                TargetHash = targetHash,
                SqlToApply = hasChanges ? applySql : string.Empty
            };
        }

        public string BuildApplySql(StoredProcedureDefinition definition)
        {
            ValidateDefinition(definition);
            ValidateSqlMatchesDefinition(definition);
            switch (_descriptor.RoutineCreationMode)
            {
                case RoutineCreationMode.CreateOrAlter:
                    return RewriteHeader(definition.Sql, @"^\s*(CREATE\s+OR\s+ALTER|CREATE|ALTER)\s+(PROCEDURE|PROC)\b", "CREATE OR ALTER PROCEDURE");
                case RoutineCreationMode.CreateOrReplace:
                    return RewriteHeader(definition.Sql, @"^\s*(CREATE\s+OR\s+REPLACE|CREATE|ALTER)\s+PROCEDURE\b", "CREATE OR REPLACE PROCEDURE");
                case RoutineCreationMode.DropCreate:
                    return BuildDropSql(definition) + Environment.NewLine + definition.Sql.Trim();
                default:
                    throw new NotSupportedException("Stored procedures are not supported by this provider descriptor.");
            }
        }

        public ModelSyncSqlCommand BuildReadCurrentDefinitionPlan(StoredProcedureDefinition definition)
        {
            ValidateDefinition(definition);
            switch (_descriptor.RoutineCatalogStyle)
            {
                case RoutineCatalogStyle.SystemModuleCatalog:
                    return new ModelSyncSqlCommand(
                        "SELECT sm.definition FROM sys.sql_modules sm JOIN sys.objects o ON sm.object_id = o.object_id JOIN sys.schemas s ON o.schema_id = s.schema_id WHERE o.type = 'P' AND s.name = @schema AND o.name = @name;",
                        ModelSyncSqlPurpose.StoredProcedure,
                        RoutineParameters(definition));
                case RoutineCatalogStyle.ShowCreateRoutine:
                    return new ModelSyncSqlCommand(
                        "SHOW CREATE PROCEDURE " + _dialect.Qualify(definition.Schema, definition.Name) + ";",
                        ModelSyncSqlPurpose.StoredProcedure);
                case RoutineCatalogStyle.FunctionCatalog:
                    return new ModelSyncSqlCommand(
                        "SELECT pg_get_functiondef(p.oid) FROM pg_proc p JOIN pg_namespace n ON p.pronamespace = n.oid WHERE p.prokind = 'p' AND n.nspname = @schema AND p.proname = @name;",
                        ModelSyncSqlPurpose.StoredProcedure,
                        RoutineParameters(definition));
                default:
                    throw new NotSupportedException("Stored procedures are not supported by this provider descriptor.");
            }
        }

        public string BuildComparableSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("Procedure SQL cannot be empty.", nameof(sql));

            if (_descriptor.RoutineCreationMode == RoutineCreationMode.DropCreate)
            {
                var comparable = RewriteHeader(sql, @"^\s*CREATE\s+(?:DEFINER\s*=\s*.+?\s+)?PROCEDURE\b", "CREATE PROCEDURE", RegexOptions.Singleline);
                return comparable.Replace("`", string.Empty);
            }

            if (_descriptor.RoutineCreationMode == RoutineCreationMode.CreateOrAlter)
                return RewriteHeader(sql, @"^\s*(CREATE\s+OR\s+ALTER|CREATE|ALTER)\s+(PROCEDURE|PROC)\b", "CREATE OR ALTER PROCEDURE");
            if (_descriptor.RoutineCreationMode == RoutineCreationMode.CreateOrReplace)
            {
                var comparable = RewriteHeader(sql, @"^\s*(CREATE\s+OR\s+REPLACE|CREATE|ALTER)\s+PROCEDURE\b", "CREATE OR REPLACE PROCEDURE");
                comparable = Regex.Replace(comparable, @"\$[A-Za-z_][A-Za-z0-9_]*\$", "$$$$");
                return TrimFinalTerminator(comparable);
            }
            throw new NotSupportedException("Stored procedures are not supported by this provider descriptor.");
        }

        public string BuildDropSql(StoredProcedureDefinition definition)
            => "DROP PROCEDURE IF EXISTS " + _dialect.Qualify(definition.Schema, definition.Name) + ";";

        private void ValidateDefinition(StoredProcedureDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            ValidateIdentifier(definition.Schema, nameof(definition.Schema));
            ValidateIdentifier(definition.Name, nameof(definition.Name));
            if (string.IsNullOrWhiteSpace(definition.Sql))
                throw new ArgumentException("Procedure SQL cannot be empty.", nameof(definition));
        }

        private void ValidateSqlMatchesDefinition(StoredProcedureDefinition definition)
        {
            var (schema, name) = ExtractRoutineNameParts(definition.Sql);
            if (!string.IsNullOrWhiteSpace(schema) && !string.Equals(schema, definition.Schema, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Stored procedure SQL schema '{schema}' does not match registered schema '{definition.Schema}'.");
            if (!string.Equals(name, definition.Name, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Stored procedure SQL name '{name}' does not match registered name '{definition.Name}'.");
        }

        private static string RewriteHeader(string sql, string pattern, string replacement, RegexOptions extra = RegexOptions.None)
        {
            if (BatchSeparatorPattern.IsMatch(sql))
                throw new InvalidOperationException("Stored procedure SQL files must not contain batch separators. Keep one procedure definition per file.");

            var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | extra);
            if (!regex.IsMatch(sql))
                throw new InvalidOperationException("Stored procedure SQL has an unsupported routine header.");
            return regex.Replace(sql.Trim(), replacement, 1);
        }

        private static string TrimFinalTerminator(string sql)
        {
            var trimmed = sql.Trim();
            return trimmed.EndsWith(";", StringComparison.Ordinal) ? trimmed.Substring(0, trimmed.Length - 1) : trimmed;
        }

        private static (string Schema, string Name) ExtractRoutineNameParts(string sql)
        {
            var match = Regex.Match(sql ?? string.Empty,
                @"^\s*(?:CREATE\s+OR\s+ALTER|CREATE\s+OR\s+REPLACE|CREATE|ALTER)\s+(?:DEFINER\s*=\s*.+?\s+)?(?:PROCEDURE|PROC)\s+(?:(?:\[(?<schemaBracket>[^\]]+)\]|`(?<schemaBacktick>[^`]+)`|""(?<schemaQuote>[^""]+)""|(?<schemaBare>[A-Za-z_][A-Za-z0-9_]*))\s*\.\s*)?(?:\[(?<nameBracket>[^\]]+)\]|`(?<nameBacktick>[^`]+)`|""(?<nameQuote>[^""]+)""|(?<nameBare>[A-Za-z_][A-Za-z0-9_]*))(?=\s|\(|$)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
                throw new InvalidOperationException("Stored procedure SQL must include a procedure name after the routine header.");
            return (First(match, "schemaBracket", "schemaBacktick", "schemaQuote", "schemaBare"), First(match, "nameBracket", "nameBacktick", "nameQuote", "nameBare"));
        }

        private static IReadOnlyList<ModelSyncSqlParameter> RoutineParameters(StoredProcedureDefinition definition)
            => new[] { new ModelSyncSqlParameter("@schema", definition.Schema), new ModelSyncSqlParameter("@name", definition.Name) };

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

        private static void ValidateIdentifier(string identifier, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(identifier) || !SafeIdentifierPattern.IsMatch(identifier))
                throw new ArgumentException($"Invalid SQL identifier '{identifier}'.", parameterName);
        }
    }
}
