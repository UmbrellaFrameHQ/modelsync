using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.Core.Interfaces;
using UmbrellaFrame.ModelSync.MySql;
using UmbrellaFrame.ModelSync.PostgreSQL;
using UmbrellaFrame.ModelSync.SQLite;
using UmbrellaFrame.ModelSync.SqlServer;

namespace UmbrellaFrame.ModelSync.Cli
{
    internal static class Program
    {
        private const int Success = 0;
        private const int UsageError = 2;
        private const int ExecutionError = 1;
        private const int Cancelled = 130;
        internal const string DefaultConnectionEnvironmentVariable = "MODELSYNC_CONNECTION_STRING";

        public static async Task<int> Main(string[] args)
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintHelp();
                return Success;
            }

            using (var cancellation = new CancellationTokenSource())
            {
                ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cancellation.Cancel();
                };

                Console.CancelKeyPress += cancelHandler;
                try
                {
                    var command = args[0].Trim().ToLowerInvariant();
                    var options = CliOptions.Parse(args.Skip(1));

                    switch (command)
                    {
                        case "version":
                            Console.WriteLine(GetVersion());
                            return Success;
                        case "validate":
                            return Validate(options);
                        case "run":
                            return await RunAsync(options, cancellation.Token).ConfigureAwait(false);
                        default:
                            Console.Error.WriteLine($"Unknown command: {args[0]}");
                            PrintHelp();
                            return UsageError;
                    }
                }
                catch (CliUsageException ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    return UsageError;
                }
                catch (OperationCanceledException)
                {
                    Console.Error.WriteLine("ModelSync operation was cancelled.");
                    return Cancelled;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(SafeMessage(ex));
                    return ExecutionError;
                }
                finally
                {
                    Console.CancelKeyPress -= cancelHandler;
                }
            }
        }

        private static async Task<int> RunAsync(CliOptions cli, CancellationToken cancellationToken)
        {
            var provider = cli.Require("provider");
            var connection = ResolveConnectionString(cli);
            var scripts = cli.Require("scripts");

            if (!cli.HasFlag("dry-run") && !cli.HasFlag("apply"))
                throw new CliUsageException("Applying migrations requires explicit --apply confirmation. Use --dry-run to preview changes.");

            var options = new MigrationRunnerOptions
            {
                HistorySchema = cli.Get("history-schema", "sec")
            };

            if (cli.HasFlag("legacy-profile"))
                options.ApplyCompatibilityProfile(MigrationCompatibilityProfiles.LegacyEmbeddedSql);

            var runner = CreateRunner(provider, connection, options);
            var definitions = LoadScriptDefinitions(scripts);
            foreach (var definition in definitions)
                runner.RegisterScript(definition);

            if (cli.HasFlag("dry-run"))
            {
                var plan = await runner.CompareRegisteredAsync(cancellationToken).ConfigureAwait(false);
                PrintDryRun(plan);
                return Success;
            }

            var result = await runner.RunWithResultAsync(cancellationToken).ConfigureAwait(false);
            WriteReports(cli, result);
            PrintSummary(result);
            return MapExecutionResultToExitCode(result);
        }

        internal static int MapExecutionResultToExitCode(MigrationExecutionResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (result.State == MigrationExecutionState.Cancelled)
                return Cancelled;
            return result.Succeeded ? Success : ExecutionError;
        }

        private static int Validate(CliOptions cli)
        {
            var scripts = cli.Require("scripts");
            var definitions = LoadScriptDefinitions(scripts);
            var duplicateKeys = definitions
                .GroupBy(definition => definition.Category + ":" + definition.Id, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (duplicateKeys.Count > 0)
                throw new CliUsageException("Duplicate migration script ids were found: " + string.Join(", ", duplicateKeys));

            Console.WriteLine("Validation: PASS");
            Console.WriteLine($"Scripts: {definitions.Count}");

            foreach (var group in definitions.GroupBy(definition => definition.Category).OrderBy(group => group.Key.ToString()))
                Console.WriteLine($"{group.Key}: {group.Count()}");

            return Success;
        }

        private static List<MigrationScriptDefinition> LoadScriptDefinitions(string scripts)
        {
            if (!Directory.Exists(scripts))
                throw new CliUsageException($"Scripts directory was not found: {scripts}");

            var files = Directory.GetFiles(scripts, "*.sql", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count == 0)
                throw new CliUsageException($"No .sql migration files were found under: {scripts}");

            return files.Select(file => MigrationScriptDefinition.FromFile(file)).ToList();
        }

        private static IMigrationRunner CreateRunner(string provider, string connection, MigrationRunnerOptions options)
        {
            switch (provider.Trim().ToLowerInvariant())
            {
                case "sqlserver":
                case "mssql":
                    return new SqlServerMigrationRunner(connection, options);
                case "mysql":
                case "mariadb":
                    return new MySqlMigrationRunner(connection, options);
                case "postgres":
                case "postgresql":
                    return new PostgresMigrationRunner(connection, options);
                case "sqlite":
                    return new SQLiteMigrationRunner(connection, options);
                default:
                    throw new CliUsageException("Unsupported provider. Use sqlserver, mysql, mariadb, postgresql, or sqlite.");
            }
        }

        private static void WriteReports(CliOptions cli, MigrationExecutionResult result)
        {
            var markdownPath = cli.Get("report-md", string.Empty);
            if (!string.IsNullOrWhiteSpace(markdownPath))
            {
                EnsureParentDirectory(markdownPath);
                File.WriteAllText(markdownPath, MigrationExecutionMarkdownReport.Create(result));
            }

            var jsonPath = cli.Get("report-json", string.Empty);
            if (!string.IsNullOrWhiteSpace(jsonPath))
            {
                EnsureParentDirectory(jsonPath);
                File.WriteAllText(jsonPath, MigrationExecutionJsonReport.Create(result));
            }
        }

        private static void EnsureParentDirectory(string path)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
        }

        private static void PrintSummary(MigrationExecutionResult result)
        {
            Console.WriteLine($"State: {result.State}");
            Console.WriteLine($"Succeeded: {result.Succeeded}");
            Console.WriteLine($"Items: {result.Items.Count}");
            Console.WriteLine("DurationMs: " + result.Duration.TotalMilliseconds.ToString("0.##", CultureInfo.InvariantCulture));

            foreach (var group in result.Items.GroupBy(item => item.Action).OrderBy(group => group.Key.ToString()))
                Console.WriteLine($"{group.Key}: {group.Count()}");
        }

        private static void PrintDryRun(IReadOnlyList<MigrationSyncPlan> plan)
        {
            Console.WriteLine("DryRun: true");
            Console.WriteLine($"Scripts: {plan.Count}");
            Console.WriteLine($"Changes: {plan.Count(item => item.HasChanges)}");

            foreach (var group in plan.GroupBy(item => item.ChangeType).OrderBy(group => group.Key.ToString()))
                Console.WriteLine($"{group.Key}: {group.Count()}");

            foreach (var item in plan.Where(item => item.HasChanges))
            {
                Console.WriteLine();
                Console.WriteLine($"{item.ChangeType}: {item.Definition.Category}/{item.Definition.Id} - {item.Definition.Name}");
                Console.WriteLine($"Mode: {item.ExecutionMode}");
                if (!string.IsNullOrWhiteSpace(item.DecisionReason))
                    Console.WriteLine($"Reason: {item.DecisionReason}");
                else if (!string.IsNullOrWhiteSpace(item.Reason))
                    Console.WriteLine($"Reason: {item.Reason}");
                Console.WriteLine($"History: {item.HistoryDecision}");
                foreach (var sql in item.PlannedExecutionSql)
                    Console.WriteLine($"Planned SQL: {sql}");
                foreach (var sql in item.RepairSql)
                    Console.WriteLine($"Repair suggestion: {sql}");
                foreach (var drift in item.UnappliedDrift)
                    Console.WriteLine($"Unapplied drift: {drift}");
            }
        }

        private static string GetVersion()
        {
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            return string.IsNullOrWhiteSpace(version) ? "1.4.0-rc.1" : version;
        }

        internal static string ResolveConnectionString(CliOptions cli)
        {
            var inline = cli.Get("connection", string.Empty);
            var environmentName = cli.Get("connection-env", string.Empty);

            if (!string.IsNullOrWhiteSpace(inline) && !string.IsNullOrWhiteSpace(environmentName))
                throw new CliUsageException("Use either --connection or --connection-env, not both.");

            if (!string.IsNullOrWhiteSpace(inline))
            {
                Console.Error.WriteLine("Warning: --connection may be visible in process listings. Prefer --connection-env.");
                return inline;
            }

            if (string.IsNullOrWhiteSpace(environmentName))
                environmentName = DefaultConnectionEnvironmentVariable;

            if (!Regex.IsMatch(environmentName, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new CliUsageException("The connection environment variable name is invalid.");

            var value = Environment.GetEnvironmentVariable(environmentName);
            if (string.IsNullOrWhiteSpace(value))
                throw new CliUsageException($"Connection string environment variable '{environmentName}' is not set.");

            return value;
        }

        internal static string SafeMessage(Exception ex)
        {
            var message = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
            var redacted = Regex.Replace(message, @"(?i)(password|pwd|token|api[_-]?key|secret|access[_-]?key)\s*=\s*[^;\s]+", "$1=<redacted>");
            return Regex.Replace(redacted, @"(?i)(password|pwd|token|api[_-]?key|secret|access[_-]?key)\s*[:=]\s*['""]?[^,'""\s;]+", "$1=<redacted>");
        }

        private static bool IsHelp(string value)
        {
            return string.Equals(value, "help", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase);
        }

        private static void PrintHelp()
        {
            Console.WriteLine("ModelSync CLI");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  modelsync version");
            Console.WriteLine("  modelsync validate --scripts <directory>");
            Console.WriteLine("  modelsync run --provider <provider> --connection-env <variable> --scripts <directory> --dry-run");
            Console.WriteLine("  modelsync run --provider <provider> --connection-env <variable> --scripts <directory> --apply [--report-md <path>] [--report-json <path>]");
            Console.WriteLine();
            Console.WriteLine("Providers:");
            Console.WriteLine("  sqlserver, mysql, mariadb, postgresql, sqlite");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine($"  --connection-env <name>     Reads the connection string from an environment variable. Default: {DefaultConnectionEnvironmentVariable}.");
            Console.WriteLine("  --connection <value>        Inline compatibility option; may be visible in process listings.");
            Console.WriteLine("  --history-schema <schema>   Defaults to sec.");
            Console.WriteLine("  --legacy-profile            Applies MigrationCompatibilityProfiles.LegacyEmbeddedSql.");
            Console.WriteLine("  --dry-run                   Compares registered scripts without applying them.");
            Console.WriteLine("  --apply                     Explicitly confirms migration execution.");
        }
    }

    internal sealed class CliOptions
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static CliOptions Parse(IEnumerable<string> args)
        {
            var result = new CliOptions();
            var tokens = args.ToList();

            for (var index = 0; index < tokens.Count; index++)
            {
                var token = tokens[index];
                if (!token.StartsWith("--", StringComparison.Ordinal))
                    throw new CliUsageException($"Unexpected argument: {token}");

                var name = token.Substring(2);
                if (string.IsNullOrWhiteSpace(name))
                    throw new CliUsageException("Option name cannot be empty.");

                if (name.Equals("legacy-profile", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("dry-run", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("apply", StringComparison.OrdinalIgnoreCase))
                {
                    result._flags.Add(name);
                    continue;
                }

                if (index + 1 >= tokens.Count || tokens[index + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new CliUsageException($"Missing value for option: --{name}");

                result._values[name] = tokens[++index];
            }

            return result;
        }

        public string Require(string name)
        {
            var value = Get(name, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
                throw new CliUsageException($"Missing required option: --{name}");

            return value;
        }

        public string Get(string name, string defaultValue)
        {
            return _values.TryGetValue(name, out var value) ? value : defaultValue;
        }

        public bool HasFlag(string name)
        {
            return _flags.Contains(name);
        }
    }

    internal sealed class CliUsageException : Exception
    {
        public CliUsageException(string message) : base(message)
        {
        }
    }
}
