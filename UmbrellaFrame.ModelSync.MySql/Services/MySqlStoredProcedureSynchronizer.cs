using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Interfaces;
using UmbrellaFrame.ModelSync.Core.Services;

namespace UmbrellaFrame.ModelSync.MySql
{
    /// <summary>
    /// MySQL/MariaDB stored procedure synchronizer.
    /// MySQL does not support <c>CREATE OR ALTER PROCEDURE</c>, so changed procedures are recreated.
    /// </summary>
    public sealed class MySqlStoredProcedureSynchronizer : IStoredProcedureSynchronizer
    {
        private static readonly Regex SafeIdentifierPattern =
            new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        private static readonly Regex ProcedureHeaderPattern =
            new Regex(@"^\s*CREATE\s+(?:DEFINER\s*=\s*.+?\s+)?PROCEDURE\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex ProcedureNamePattern =
            new Regex(
                @"^\s*CREATE\s+(?:DEFINER\s*=\s*.+?\s+)?PROCEDURE\s+(?:(?:`(?<schemaQuoted>[^`]+)`|(?<schemaBare>[A-Za-z_][A-Za-z0-9_]*))\s*\.\s*)?(?:`(?<nameQuoted>[^`]+)`|(?<nameBare>[A-Za-z_][A-Za-z0-9_]*))(?=\s|\(|$)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex DefinerHeaderPattern =
            new Regex(@"^\s*CREATE\s+DEFINER\s*=\s*.+?\s+PROCEDURE\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private readonly string _connectionString;
        private readonly ILogger<MySqlStoredProcedureSynchronizer> _logger;
        private readonly List<StoredProcedureDefinition> _definitions = new List<StoredProcedureDefinition>();

        /// <summary>Creates a MySQL/MariaDB stored procedure synchronizer.</summary>
        public MySqlStoredProcedureSynchronizer(
            string connectionString,
            ILogger<MySqlStoredProcedureSynchronizer> logger = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));

            _connectionString = connectionString;
            _logger = logger ?? NullLogger<MySqlStoredProcedureSynchronizer>.Instance;
        }

        /// <inheritdoc/>
        public void RegisterProcedure(StoredProcedureDefinition definition)
        {
            ValidateDefinition(definition);
            _definitions.Add(definition);
        }

        /// <inheritdoc/>
        public StoredProcedureDefinition RegisterProcedureFile(string path, string name = null, string schema = "dbo")
        {
            var definition = StoredProcedureDefinition.FromFile(path, name, schema);
            RegisterProcedure(definition);
            return definition;
        }

        /// <inheritdoc/>
        public async Task<StoredProcedureSyncPlan> CompareAsync(
            StoredProcedureDefinition definition,
            CancellationToken cancellationToken = default)
        {
            ValidateDefinition(definition);
            var currentSql = await ReadCurrentDefinitionAsync(definition, cancellationToken).ConfigureAwait(false);
            return BuildPlan(definition, currentSql);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<StoredProcedureSyncPlan>> CompareRegisteredAsync(CancellationToken cancellationToken = default)
        {
            var plans = new List<StoredProcedureSyncPlan>();
            foreach (var definition in _definitions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                plans.Add(await CompareAsync(definition, cancellationToken).ConfigureAwait(false));
            }

            return plans;
        }

        /// <inheritdoc/>
        public async Task ApplyAsync(StoredProcedureSyncPlan plan, CancellationToken cancellationToken = default)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!plan.HasChanges)
                return;

            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var dropCommand = new MySqlCommand(BuildDropSql(plan.Definition), connection))
                {
                    await dropCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                using (var createCommand = new MySqlCommand(plan.Definition.Sql.Trim(), connection))
                {
                    await createCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            _logger.LogInformation("Stored procedure synchronized: {Schema}.{Name} ({ChangeType})",
                plan.Definition.Schema,
                plan.Definition.Name,
                plan.ChangeType);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<StoredProcedureSyncPlan>> SyncRegisteredAsync(CancellationToken cancellationToken = default)
        {
            var plans = await CompareRegisteredAsync(cancellationToken).ConfigureAwait(false);
            foreach (var plan in plans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ApplyAsync(plan, cancellationToken).ConfigureAwait(false);
            }

            return plans;
        }

        /// <summary>Builds a dry-run plan from a project definition and an optional current database definition.</summary>
        public StoredProcedureSyncPlan BuildPlan(StoredProcedureDefinition definition, string? currentSql)
        {
            ValidateDefinition(definition);

            var applySql = BuildApplySql(definition);
            var targetHash = SqlDefinitionNormalizer.ComputeHash(ToComparableSql(definition.Sql));

            if (string.IsNullOrWhiteSpace(currentSql))
            {
                return new StoredProcedureSyncPlan
                {
                    Definition = definition,
                    ChangeType = StoredProcedureChangeType.Create,
                    CurrentHash = null,
                    TargetHash = targetHash,
                    SqlToApply = applySql
                };
            }

            var currentHash = SqlDefinitionNormalizer.ComputeHash(ToComparableSql(currentSql));
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

        /// <summary>Builds the MySQL/MariaDB recreate script for the procedure.</summary>
        public string BuildApplySql(StoredProcedureDefinition definition)
        {
            ValidateDefinition(definition);
            ValidateSqlMatchesDefinition(definition);
            return $"{BuildDropSql(definition)}{Environment.NewLine}{definition.Sql.Trim()}";
        }

        private static string BuildDropSql(StoredProcedureDefinition definition)
            => $"DROP PROCEDURE IF EXISTS `{definition.Schema}`.`{definition.Name}`;";

        private async Task<string?> ReadCurrentDefinitionAsync(StoredProcedureDefinition definition, CancellationToken cancellationToken)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var command = new MySqlCommand($"SHOW CREATE PROCEDURE `{definition.Schema}`.`{definition.Name}`;", connection))
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        return null;

                    var ordinal = reader.GetOrdinal("Create Procedure");
                    return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
                }
            }
        }

        private static string ToComparableSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("Procedure SQL cannot be empty.", nameof(sql));

            if (!ProcedureHeaderPattern.IsMatch(sql))
            {
                throw new InvalidOperationException("MySQL stored procedure SQL must start with CREATE PROCEDURE.");
            }

            var comparable = DefinerHeaderPattern.Replace(sql.Trim(), "CREATE PROCEDURE", 1);
            return comparable.Replace("`", string.Empty);
        }

        private static void ValidateDefinition(StoredProcedureDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            ValidateIdentifier(definition.Schema, nameof(definition.Schema));
            ValidateIdentifier(definition.Name, nameof(definition.Name));
            if (string.IsNullOrWhiteSpace(definition.Sql))
                throw new ArgumentException("Procedure SQL cannot be empty.", nameof(definition));
        }

        private static void ValidateSqlMatchesDefinition(StoredProcedureDefinition definition)
        {
            var match = ProcedureNamePattern.Match(definition.Sql ?? string.Empty);
            if (!match.Success)
                throw new InvalidOperationException("Stored procedure SQL must include a procedure name after CREATE PROCEDURE.");

            var schema = FirstGroupValue(match, "schemaQuoted", "schemaBare");
            var name = FirstGroupValue(match, "nameQuoted", "nameBare");

            if (!string.IsNullOrWhiteSpace(schema) &&
                !string.Equals(schema, definition.Schema, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Stored procedure SQL schema '{schema}' does not match registered schema '{definition.Schema}'.");
            }

            if (!string.Equals(name, definition.Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Stored procedure SQL name '{name}' does not match registered name '{definition.Name}'.");
            }
        }

        private static string FirstGroupValue(Match match, string firstName, string secondName)
        {
            var first = match.Groups[firstName];
            if (first.Success)
                return first.Value;

            var second = match.Groups[secondName];
            return second.Success ? second.Value : string.Empty;
        }

        private static void ValidateIdentifier(string identifier, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(identifier) || !SafeIdentifierPattern.IsMatch(identifier))
            {
                throw new ArgumentException(
                    $"Invalid SQL identifier '{identifier}'. Identifiers must match ^[A-Za-z_][A-Za-z0-9_]*$.",
                    parameterName);
            }
        }
    }
}
