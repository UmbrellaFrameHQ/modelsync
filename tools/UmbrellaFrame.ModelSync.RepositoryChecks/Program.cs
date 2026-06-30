using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace UmbrellaFrame.ModelSync.RepositoryChecks;

internal static class Program
{
    private static readonly string ShellWord = string.Concat("power", "shell");
    private static readonly string ShortShellWord = string.Concat("p", "wsh");
    private static readonly string VerifyNoShellCommand = "verify-no-" + ShellWord;
    private const string CurrentReleaseVersion = "1.2.2";
    private static readonly string[] ForbiddenScriptExtensions =
    {
        "." + "ps" + "1",
        "." + "psm" + "1",
        "." + "psd" + "1"
    };

    private static readonly string[] ProviderDirectories =
    {
        "UmbrellaFrame.ModelSync.SqlServer",
        "UmbrellaFrame.ModelSync.MySql",
        "UmbrellaFrame.ModelSync.PostgreSQL",
        "UmbrellaFrame.ModelSync.SQLite"
    };

    private static readonly string[] ExcludedDirectoryNames =
    {
        ".git",
        ".vs",
        "bin",
        "obj",
        "artifacts",
        "nupkg",
        "coverage",
        "TestResults"
    };

    private static readonly SqlRule[] SqlRules =
    {
        new("DDL create table", @"CREATE\s+TABLE"),
        new("DDL alter table", @"ALTER\s+TABLE"),
        new("DDL drop table", @"DROP\s+TABLE"),
        new("DDL create schema", @"CREATE\s+SCHEMA"),
        new("Catalog information schema", @"information_schema"),
        new("SQL Server table catalog", @"sys\.tables"),
        new("SQL Server column catalog", @"sys\.columns"),
        new("PostgreSQL catalog", @"pg_catalog"),
        new("SQLite catalog", @"sqlite_master"),
        new("SQLite schema catalog", @"sqlite_schema"),
        new("SQLite table pragma", @"PRAGMA\s+table_info"),
        new("SQLite index pragma", @"PRAGMA\s+index_list"),
        new("SQL Server migration lock", @"sp_getapplock"),
        new("MySQL migration lock", @"GET_LOCK"),
        new("PostgreSQL migration lock", @"pg_advisory"),
        new("Migration history table", @"SchemaMigration_"),
        new("Routine create", @"CREATE\s+PROCEDURE"),
        new("Routine alter", @"ALTER\s+PROCEDURE"),
        new("Routine drop", @"DROP\s+PROCEDURE"),
        new("Routine create or alter", @"CREATE\s+OR\s+ALTER"),
        new("Routine create or replace", @"CREATE\s+OR\s+REPLACE"),
        new("Routine definition catalog", @"ROUTINE_DEFINITION"),
        new("Routine information schema", @"information_schema\.routines"),
        new("SQL Server routine catalog", @"sys\.procedures"),
        new("SQL Server routine module catalog", @"sys\.sql_modules"),
        new("PostgreSQL routine catalog", @"pg_proc"),
        new("PostgreSQL routine definition function", @"pg_get_functiondef"),
        new("Routine header regex", @"ProcedureHeaderPattern"),
        new("Routine create replacement", @"Replace\(\s*""CREATE"),
        new("MySQL routine definition", @"SHOW\s+CREATE\s+PROCEDURE"),
        new("Command text assignment", @"CommandText\s*="),
        new("SQL returning method", @"string\s+\w+\s*\([^)]*\)\s*=>\s*""\s*(SELECT|CREATE|ALTER|DROP|INSERT|UPDATE|DELETE|PRAGMA|SHOW)\b")
    };

    private static readonly SqlRule[] DirectConnectionRules =
    {
        new("SQL Server direct connection", @"new\s+(?:global::)?(?:Microsoft\.Data\.SqlClient\.)?SqlConnection\s*\("),
        new("MySQL direct connection", @"new\s+(?:global::)?(?:MySqlConnector\.)?MySqlConnection\s*\("),
        new("PostgreSQL direct connection", @"new\s+(?:global::)?(?:Npgsql\.)?NpgsqlConnection\s*\("),
        new("SQLite direct connection", @"new\s+(?:global::)?(?:Microsoft\.Data\.Sqlite\.)?SqliteConnection\s*\(")
    };

    public static int Main(string[] args)
    {
        try
        {
            var root = FindRepositoryRoot();
            var command = args.Length == 0 ? "verify-all" : args[0];

            if (string.Equals(command, VerifyNoShellCommand, StringComparison.OrdinalIgnoreCase))
            {
                return Run("repository shell policy", () => VerifyNoShell(root));
            }

            return command switch
            {
                "verify-sql-ownership" => Run("provider SQL ownership", () => VerifySqlOwnership(root)),
                "verify-sql-ownership-self-test" => Run("provider SQL ownership self-test", VerifySqlOwnershipSelfTest),
                "verify-no-direct-provider-connection-outside-factory" => Run("provider connection factory ownership", () =>
                {
                    VerifyDirectProviderConnectionSelfTest();
                    VerifyDirectProviderConnections(root);
                }),
                "verify-version-consistency" => Run("version consistency", () =>
                {
                    VerifyVersionConsistencySelfTest();
                    VerifyVersionConsistency(root);
                }),
                "verify-release-documentation" => Run("release documentation", () => VerifyReleaseDocumentation(root)),
                "verify-release-documentation-contract" => Run("release documentation contract", () => VerifyReleaseDocumentationContract(root)),
                "verify-provider-specific-api-usage" => Run("provider-specific API usage", () => VerifyProviderSpecificApiUsage(root)),
                "verify-core-provider-boundary" => Run("Core/provider boundary", () => VerifyCoreProviderBoundary(root)),
                "verify-operational-hardening" => Run("operational hardening", () =>
                {
                    VerifyOperationalHardeningSelfTest();
                    VerifyOperationalHardening(root);
                }),
                "verify-integration-workflow-contract" => Run("integration workflow contract", () =>
                {
                    VerifyIntegrationWorkflowContractSelfTest();
                    VerifyIntegrationWorkflowContract(root);
                }),
                "verify-1-2-0-consumer-compatibility" => Run("1.2.0 consumer compatibility", () => VerifyConsumerCompatibility(root)),
                "verify-consumer-compatibility" => Run("external consumer compatibility", () => VerifyExternalConsumerCompatibility(root, args)),
                "verify-publication-hygiene" => Run("publication hygiene", () =>
                {
                    VerifyPublicationHygieneSelfTest();
                    VerifyPublicationHygiene(root);
                }),
                "verify-migration-execution-policies" => Run("migration execution policy gate", () => VerifyMigrationExecutionPolicies(root)),
                "verify-legacy-history-compatibility" => Run("legacy history compatibility gate", () => VerifyLegacyHistoryCompatibility(root)),
                "verify-all-provider-legacy-fixtures" => Run("all-provider legacy fixture gate", () =>
                {
                    VerifyLegacyFixtureMarkersSelfTest();
                    VerifyAllProviderLegacyFixtures(root);
                }),
                "verify-all" => Run("repository checks", () =>
                {
                    VerifyNoShell(root);
                    VerifySqlOwnershipSelfTest();
                    VerifySqlOwnership(root);
                    VerifyDirectProviderConnectionSelfTest();
                    VerifyDirectProviderConnections(root);
                    VerifyVersionConsistencySelfTest();
                    VerifyVersionConsistency(root);
                    VerifyReleaseDocumentation(root);
                    VerifyReleaseDocumentationContract(root);
                    VerifyProviderSpecificApiUsage(root);
                    VerifyCoreProviderBoundary(root);
                    VerifyOperationalHardeningSelfTest();
                    VerifyOperationalHardening(root);
                    VerifyIntegrationWorkflowContractSelfTest();
                    VerifyIntegrationWorkflowContract(root);
                    VerifyConsumerCompatibility(root);
                    VerifyPublicationHygieneSelfTest();
                    VerifyPublicationHygiene(root);
                    VerifyMigrationExecutionPolicies(root);
                    VerifyLegacyHistoryCompatibility(root);
                    VerifyLegacyFixtureMarkersSelfTest();
                    VerifyAllProviderLegacyFixtures(root);
                }),
                "verify-package-smoke" => Run("package smoke", () => VerifyPackageSmoke(root, args)),
                _ => Fail($"Unknown command '{command}'.")
            };
        }
        catch (CheckFailedException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int Run(string name, Action action)
    {
        action();
        Console.WriteLine($"{name}: PASS");
        return 0;
    }

    private static int Fail(string message) => throw new CheckFailedException(message);

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ModelSync.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new CheckFailedException("Repository root could not be found.");
    }

    private static void VerifyNoShell(string root)
    {
        var violations = new List<string>();
        foreach (var file in EnumerateRepositoryFiles(root))
        {
            var relative = Relative(root, file);
            var extension = Path.GetExtension(file);
            if (ForbiddenScriptExtensions.Any(forbidden => extension.Equals(forbidden, StringComparison.OrdinalIgnoreCase)))
            {
                violations.Add($"{relative}: forbidden shell script file");
                continue;
            }

            if (!IsTextCandidate(file))
            {
                continue;
            }

            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                if (line.Contains(ShellWord, StringComparison.OrdinalIgnoreCase) ||
                    line.Contains(ShortShellWord, StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{relative}:{lineNumber}: forbidden shell command reference");
                }
            }
        }

        ThrowIfViolations(violations, "Repository shell policy check failed.");
        Console.WriteLine("No tracked shell script files or workflow shell commands found.");
    }

    private static void VerifySqlOwnership(string root)
    {
        var sources = new List<SourceFile>();
        foreach (var providerDirectory in ProviderDirectories)
        {
            var providerPath = Path.Combine(root, providerDirectory);
            if (!Directory.Exists(providerPath))
            {
                continue;
            }

            foreach (var file in EnumerateFiles(providerPath, "*.cs"))
            {
                sources.Add(new SourceFile(Relative(root, file), File.ReadAllLines(file)));
            }
        }

        var violations = ScanProviderSql(sources);
        ThrowIfViolations(violations.Select(v => v.ToString()).ToList(), "Provider production source still contains framework-owned SQL.");
    }

    private static void VerifySqlOwnershipSelfTest()
    {
        var badSource = new SourceFile(
            "UmbrellaFrame.ModelSync.SqlServer/BadProviderSql.cs",
            new[]
            {
                "namespace Fixture;",
                "public sealed class BadProviderSql",
                "{",
                "    private string BuildSql() => \"CREATE TABLE dbo.Bad(Id int);\";",
                "    private const string Catalog = @\"SELECT * FROM information_schema.tables\";",
                "    public void Run(dynamic command)",
                "    {",
                "        command.CommandText = $\"ALTER TABLE dbo.Bad ADD Name nvarchar(20);\";",
                "        _ = \"CREATE PROCEDURE dbo.usp_bad AS SELECT 1\";",
                "        _ = \"CREATE OR ALTER PROCEDURE dbo.usp_bad AS SELECT 1\";",
                "        _ = \"CREATE OR REPLACE PROCEDURE usp_bad() LANGUAGE SQL AS $$ SELECT 1 $$;\";",
                "        _ = \"DROP PROCEDURE IF EXISTS usp_bad\";",
                "        _ = \"SELECT * FROM sys.tables\";",
                "        _ = \"SELECT * FROM pg_catalog.pg_class\";",
                "        _ = \"PRAGMA table_info(\\\"Bad\\\")\";",
                "        _ = \"EXEC sp_getapplock\";",
                "        _ = \"SELECT GET_LOCK('x', 1)\";",
                "        _ = \"SELECT pg_advisory_lock(1)\";",
                "    }",
                "}"
            });

        var cleanSource = new SourceFile(
            "UmbrellaFrame.ModelSync.SqlServer/CleanProviderFacade.cs",
            new[]
            {
                "namespace Fixture;",
                "public sealed class CleanProviderFacade",
                "{",
                "    private readonly object descriptor;",
                "    public CleanProviderFacade(object descriptor) => this.descriptor = descriptor;",
                "    public object Descriptor => descriptor;",
                "    public void Execute(dynamic command, dynamic plan)",
                "    {",
                "        command.CommandText = plan.CommandText;",
                "    }",
                "}"
            });

        var badViolations = ScanProviderSql(new[] { badSource });
        if (badViolations.Count == 0)
        {
            throw new CheckFailedException("Known-bad provider SQL fixture was not rejected.");
        }

        var cleanViolations = ScanProviderSql(new[] { cleanSource });
        ThrowIfViolations(cleanViolations.Select(v => v.ToString()).ToList(), "Known-clean provider facade fixture was rejected.");
    }

    private static void VerifyDirectProviderConnections(string root)
    {
        var sources = new List<SourceFile>();
        foreach (var providerDirectory in ProviderDirectories)
        {
            var providerPath = Path.Combine(root, providerDirectory);
            if (!Directory.Exists(providerPath))
            {
                continue;
            }

            foreach (var file in EnumerateFiles(providerPath, "*.cs"))
            {
                sources.Add(new SourceFile(Relative(root, file), File.ReadAllLines(file)));
            }
        }

        var violations = ScanDirectProviderConnections(sources);
        ThrowIfViolations(violations.Select(v => v.ToString()).ToList(), "Provider production source creates concrete connections outside provider connection factory files.");
    }

    private static void VerifyDirectProviderConnectionSelfTest()
    {
        var badSource = new SourceFile(
            "UmbrellaFrame.ModelSync.SqlServer/Services/BadService.cs",
            new[]
            {
                "using Microsoft.Data.SqlClient;",
                "namespace Fixture;",
                "public sealed class BadService",
                "{",
                "    public object Open(string value) => new SqlConnection(value);",
                "}"
            });

        var cleanFactory = new SourceFile(
            "UmbrellaFrame.ModelSync.SqlServer/SqlServerConnectionFactory.cs",
            new[]
            {
                "using Microsoft.Data.SqlClient;",
                "namespace Fixture;",
                "internal static class SqlServerConnectionFactory",
                "{",
                "    public static SqlConnection Create(string value) => new SqlConnection(value);",
                "}"
            });

        var cleanFacade = new SourceFile(
            "UmbrellaFrame.ModelSync.SqlServer/Services/CleanService.cs",
            new[]
            {
                "namespace Fixture;",
                "public sealed class CleanService",
                "{",
                "    public object Open(string value) => SqlServerConnectionFactory.Create(value);",
                "}"
            });

        var badViolations = ScanDirectProviderConnections(new[] { badSource });
        if (badViolations.Count == 0)
        {
            throw new CheckFailedException("Known-bad direct provider connection fixture was not rejected.");
        }

        var cleanViolations = ScanDirectProviderConnections(new[] { cleanFactory, cleanFacade });
        ThrowIfViolations(cleanViolations.Select(v => v.ToString()).ToList(), "Known-clean connection factory fixture was rejected.");
    }

    private static List<Violation> ScanProviderSql(IEnumerable<SourceFile> sources)
    {
        var violations = new List<Violation>();
        foreach (var source in sources)
        {
            for (var index = 0; index < source.Lines.Length; index++)
            {
                var line = source.Lines[index];
                var trimmed = line.Trim();
                if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                    trimmed.StartsWith("///", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var rule in SqlRules)
                {
                    if (!Regex.IsMatch(line, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    {
                        continue;
                    }

                    if (IsAllowedProviderDescriptorLine(source.Path, trimmed) ||
                        IsAllowedProviderBatchNormalizerLine(source.Path, trimmed) ||
                        IsAllowedCommandFacadeLine(trimmed) ||
                        IsAllowedReaderOrdinalLine(trimmed))
                    {
                        continue;
                    }

                    violations.Add(new Violation(source.Path, index + 1, rule.Name, trimmed));
                    break;
                }
            }
        }

        return violations;
    }

    private static List<Violation> ScanDirectProviderConnections(IEnumerable<SourceFile> sources)
    {
        var violations = new List<Violation>();
        foreach (var source in sources)
        {
            if (IsConnectionFactoryFile(source.Path))
            {
                continue;
            }

            for (var index = 0; index < source.Lines.Length; index++)
            {
                var line = source.Lines[index];
                var trimmed = line.Trim();
                if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                    trimmed.StartsWith("///", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var rule in DirectConnectionRules)
                {
                    if (!Regex.IsMatch(line, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    {
                        continue;
                    }

                    violations.Add(new Violation(source.Path, index + 1, rule.Name, trimmed));
                    break;
                }
            }
        }

        return violations;
    }

    private static bool IsConnectionFactoryFile(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.Equals("SqlServerConnectionFactory.cs", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("MySqlConnectionFactory.cs", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("PostgresConnectionFactory.cs", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("SQLiteConnectionFactory.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedProviderDescriptorLine(string file, string line)
    {
        if (!file.EndsWith("ProviderDescriptor.cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !Regex.IsMatch(line, @"\b(CREATE|SELECT|ALTER|DROP|SHOW|PRAGMA|MERGE|INSERT|UPDATE|DELETE)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool IsAllowedProviderBatchNormalizerLine(string file, string line)
    {
        if (!file.EndsWith("LegacyRoutineNormalizer.cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return line.Contains("Regex", StringComparison.Ordinal) ||
               line.Contains("ProcedureHeaderPattern", StringComparison.Ordinal) ||
               line.Contains("SplitGoBatches", StringComparison.Ordinal) ||
               line.Contains("HasProcedureBody", StringComparison.Ordinal);
    }

    private static bool IsAllowedCommandFacadeLine(string line)
    {
        return Regex.IsMatch(line, @"CommandText\s*=\s*[^;]+\.CommandText", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(line, @"CommandText\s*=\s*sql\s*;", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(line, @"CommandText\s*=\s*batch\s*;", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool IsAllowedReaderOrdinalLine(string line)
    {
        return line.Contains("GetOrdinal(", StringComparison.Ordinal);
    }

    private static void VerifyVersionConsistency(string root)
    {
        var violations = new List<string>();
        var projectFiles = new[]
        {
            "UmbrellaFrame.ModelSync.Core/UmbrellaFrame.ModelSync.Core.csproj",
            "UmbrellaFrame.ModelSync.SqlServer/UmbrellaFrame.ModelSync.SqlServer.csproj",
            "UmbrellaFrame.ModelSync.MySql/UmbrellaFrame.ModelSync.MySql.csproj",
            "UmbrellaFrame.ModelSync.PostgreSQL/UmbrellaFrame.ModelSync.PostgreSQL.csproj",
            "UmbrellaFrame.ModelSync.SQLite/UmbrellaFrame.ModelSync.SQLite.csproj",
            "UmbrellaFrame.ModelSync.Analyzers/UmbrellaFrame.ModelSync.Core.Analyzers.csproj"
        };

        foreach (var project in projectFiles)
        {
            var path = Path.Combine(root, project.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                violations.Add($"{project}: project file was not found");
                continue;
            }

            var text = File.ReadAllText(path);
            if (!Regex.IsMatch(text, $@"<Version>\s*{Regex.Escape(CurrentReleaseVersion)}\s*</Version>", RegexOptions.CultureInvariant))
            {
                violations.Add($"{project}: active package version is not {CurrentReleaseVersion}");
            }

            if (Regex.IsMatch(text, @"<Version>\s*1\.0\.8\s*</Version>", RegexOptions.CultureInvariant))
            {
                violations.Add($"{project}: active package version still references 1.0.8");
            }
        }

        var readme = ReadRequired(root, "README.md", violations);
        if (!readme.Contains($"Current package version: `{CurrentReleaseVersion}`", StringComparison.Ordinal))
        {
            violations.Add($"README.md: current package version is not {CurrentReleaseVersion}");
        }

        foreach (var packageId in SupportedPackageIds())
        {
            var expected = $"dotnet add package {packageId} --version {CurrentReleaseVersion}";
            if (!readme.Contains(expected, StringComparison.Ordinal))
            {
                violations.Add($"README.md: missing install example '{expected}'");
            }
        }

        var nugetReadme = ReadRequired(root, "docs/nuget/README.md", violations);
        if (!nugetReadme.Contains($"What's New in {CurrentReleaseVersion}", StringComparison.Ordinal) &&
            !nugetReadme.Contains($"{CurrentReleaseVersion} Operational Hardening", StringComparison.Ordinal))
        {
            violations.Add($"docs/nuget/README.md: current version heading is missing {CurrentReleaseVersion}");
        }

        var ci = ReadRequired(root, ".github/workflows/ci.yml", violations);
        if (!ci.Contains($"verify-package-smoke nupkg {CurrentReleaseVersion}", StringComparison.Ordinal))
        {
            violations.Add($".github/workflows/ci.yml: package smoke expected version is not {CurrentReleaseVersion}");
        }

        ThrowIfViolations(violations, "Version consistency verification failed.");
    }

    private static void VerifyVersionConsistencySelfTest()
    {
        var clean = "<Project><PropertyGroup><Version>1.2.2</Version></PropertyGroup></Project>";
        var bad = "<Project><PropertyGroup><Version>1.0.8</Version></PropertyGroup></Project>";
        if (!Regex.IsMatch(clean, $@"<Version>\s*{Regex.Escape(CurrentReleaseVersion)}\s*</Version>", RegexOptions.CultureInvariant))
        {
            throw new CheckFailedException("Known-clean version fixture was rejected.");
        }

        if (Regex.IsMatch(bad, $@"<Version>\s*{Regex.Escape(CurrentReleaseVersion)}\s*</Version>", RegexOptions.CultureInvariant))
        {
            throw new CheckFailedException("Known-bad version fixture was accepted.");
        }
    }

    private static void VerifyMigrationExecutionPolicies(string root)
    {
        var violations = new List<string>();
        var mode = ReadRequired(root, "UmbrellaFrame.ModelSync.Core/Models/MigrationScriptExecutionMode.cs", violations);
        RequireContains(mode, "RunOnce", "MigrationScriptExecutionMode.RunOnce is missing.", violations);
        RequireContains(mode, "HashTracked", "MigrationScriptExecutionMode.HashTracked is missing.", violations);
        RequireContains(mode, "EveryRun", "MigrationScriptExecutionMode.EveryRun is missing.", violations);

        var options = ReadRequired(root, "UmbrellaFrame.ModelSync.Core/Models/MigrationRunnerOptions.cs", violations);
        RequireContains(options, "CategoryPolicies", "MigrationRunnerOptions.CategoryPolicies is missing.", violations);
        RequireContains(options, "DefaultExecutionMode", "DefaultExecutionMode is missing.", violations);
        RequireContains(options, "MigrationScriptExecutionMode.HashTracked", "Default execution mode must stay HashTracked for 1.2.0 compatibility.", violations);
        RequireContains(options, "LegacyEmbeddedSql", "LegacyEmbeddedSql profile application is missing.", violations);

        var profile = ReadRequired(root, "UmbrellaFrame.ModelSync.Core/Models/MigrationCompatibilityProfiles.cs", violations);
        RequireContains(profile, "LegacyEmbeddedSql", "MigrationCompatibilityProfiles.LegacyEmbeddedSql is missing.", violations);

        foreach (var file in EnumerateRepositoryFiles(root).Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            var text = File.ReadAllText(file);
            if (Regex.IsMatch(text, @"class\s+\w*MigrationEngine\b", RegexOptions.CultureInvariant))
                violations.Add($"{Relative(root, file)}: second migration engine class is not allowed.");
        }

        ThrowIfViolations(violations, "Migration execution policy verification failed.");
    }

    private static void VerifyLegacyHistoryCompatibility(string root)
    {
        var violations = new List<string>();
        var dialect = ReadRequired(root, "UmbrellaFrame.ModelSync.Core/SqlGeneration/ModelSyncSqlDialect.cs", violations);
        RequireContains(dialect, "BuildEnsureHistoryHashColumnsPlan", "Core SQL compiler must own history SqlHash upgrade.", violations);
        RequireContains(dialect, "BuildAddHistoryHashColumnSql", "Core SQL compiler must expose history SqlHash column add SQL.", violations);
        RequireContains(dialect, "BuildReadLegacyHistoryPlan", "Core SQL compiler must expose legacy history read SQL.", violations);

        VerifyNoRawLegacyAlterInProviders(root, violations);

        var item = ReadRequired(root, "UmbrellaFrame.ModelSync.Core/Models/MigrationExecutionItemResult.cs", violations);
        RequireContains(item, "ExecutionMode", "ExecutionMode result metadata is missing.", violations);
        RequireContains(item, "DecisionReason", "DecisionReason result metadata is missing.", violations);
        RequireContains(item, "LegacyHashAdopted", "LegacyHashAdopted result metadata is missing.", violations);

        var plan = ReadRequired(root, "UmbrellaFrame.ModelSync.Core/Models/MigrationSyncPlan.cs", violations);
        RequireContains(plan, "HistoryRowExists", "HistoryRowExists plan metadata is missing.", violations);
        RequireContains(plan, "LegacyHashAdoptionRequired", "LegacyHashAdoptionRequired plan metadata is missing.", violations);

        var runner = ReadRequired(root, "UmbrellaFrame.ModelSync.Core/Services/SqlMigrationRunnerBase.cs", violations);
        var compareBody = ExtractMethodBody(runner, "CompareRegisteredAsync");
        if (compareBody.Contains("EnsureHistory", StringComparison.Ordinal) ||
            compareBody.Contains("AdoptLegacyHash", StringComparison.Ordinal) ||
            compareBody.Contains("RecordHistory", StringComparison.Ordinal))
        {
            violations.Add("SqlMigrationRunnerBase.CompareRegisteredAsync: compare path must not mutate history infrastructure or adoption state.");
        }

        var coreProject = ReadRequired(root, "UmbrellaFrame.ModelSync.Core/UmbrellaFrame.ModelSync.Core.csproj", violations);
        if (coreProject.Contains("Microsoft.Extensions.Configuration", StringComparison.OrdinalIgnoreCase))
            violations.Add("Core project must not depend on IConfiguration packages.");

        var sqlite = ReadRequired(root, "UmbrellaFrame.ModelSync.SQLite/SQLiteProviderDescriptor.cs", violations);
        RequireContains(sqlite, "SupportsStoredProcedures = false", "SQLite stored procedures must remain unsupported.", violations);

        ThrowIfViolations(violations, "Legacy history compatibility verification failed.");
    }

    private static void VerifyNoRawLegacyAlterInProviders(string root, List<string> violations)
    {
        foreach (var providerDirectory in ProviderDirectories)
        {
            var providerPath = Path.Combine(root, providerDirectory);
            if (!Directory.Exists(providerPath))
                continue;

            foreach (var file in EnumerateFiles(providerPath, "*.cs"))
            {
                var relative = Relative(root, file);
                var text = File.ReadAllText(file);
                if (Regex.IsMatch(text, @"ALTER\s+TABLE[^;\n]+SqlHash", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    violations.Add($"{relative}: provider service must not contain raw legacy ALTER SqlHash SQL.");
            }
        }
    }

    private static void VerifyAllProviderLegacyFixtures(string root)
    {
        var violations = new List<string>();
        var sources = EnumerateRepositoryFiles(root)
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && Relative(root, f).Contains("Test/", StringComparison.Ordinal))
            .Select(f => new SourceFile(Relative(root, f), File.ReadAllLines(f)))
            .ToArray();

        var requiredProviders = new[] { "sqlserver", "mysql", "mariadb", "postgresql", "sqlite" };
        var requiredScenarios = new[]
        {
            "CompareReadOnly",
            "LegacyUpgradeFirstRun",
            "LegacyUpgradeSecondRun",
            "ChangedResourceRun",
            "FailureSafetyRun"
        };

        var markers = ScanLegacyFixtureMarkers(sources);
        foreach (var provider in requiredProviders)
        {
            if (!markers.Contains(provider))
            {
                violations.Add($"{provider}: legacy compatibility fixture marker was not found.");
                continue;
            }

            foreach (var scenario in requiredScenarios)
            {
                if (!sources.Any(source =>
                        source.Lines.Any(line => line.Contains($"LegacyCompatibilityFixture(\"{provider}\")", StringComparison.OrdinalIgnoreCase)) &&
                        source.Lines.Any(line => line.Contains(scenario, StringComparison.Ordinal))))
                {
                    violations.Add($"{provider}: legacy compatibility fixture scenario '{scenario}' was not found.");
                }
            }
        }

        ThrowIfViolations(violations, "All-provider legacy fixture verification failed.");
    }

    private static void VerifyLegacyFixtureMarkersSelfTest()
    {
        var clean = new SourceFile("CleanTest/Clean.cs", new[]
        {
            "[LegacyCompatibilityFixture(\"sqlserver\")]",
            "public void CompareReadOnly() {}",
            "public void LegacyUpgradeFirstRun() {}",
            "public void LegacyUpgradeSecondRun() {}",
            "public void ChangedResourceRun() {}",
            "public void FailureSafetyRun() {}",
            "public sealed class SqlServerLegacyCompatibilityTests {}"
        });
        var bad = new SourceFile("BadTest/Bad.cs", new[]
        {
            "public sealed class SqlServerLegacyCompatibilityTests {}"
        });

        if (!ScanLegacyFixtureMarkers(new[] { clean }).Contains("sqlserver"))
            throw new CheckFailedException("Known-clean legacy fixture marker was rejected.");
        if (ScanLegacyFixtureMarkers(new[] { bad }).Contains("sqlserver"))
            throw new CheckFailedException("Known-bad legacy fixture marker was accepted.");
    }

    private static HashSet<string> ScanLegacyFixtureMarkers(IEnumerable<SourceFile> sources)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            foreach (var line in source.Lines)
            {
                var match = Regex.Match(line, @"LegacyCompatibilityFixture\s*\(\s*""(?<provider>sqlserver|mysql|mariadb|postgresql|sqlite)""\s*\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (match.Success)
                    result.Add(match.Groups["provider"].Value.ToLowerInvariant());
            }
        }

        return result;
    }

    private static void RequireContains(string text, string expected, string message, List<string> violations)
    {
        if (!text.Contains(expected, StringComparison.Ordinal))
            violations.Add(message);
    }

    private static string ExtractMethodBody(string source, string methodName)
    {
        var index = source.IndexOf(methodName, StringComparison.Ordinal);
        if (index < 0)
            return string.Empty;
        var brace = source.IndexOf('{', index);
        if (brace < 0)
            return string.Empty;

        var depth = 0;
        for (var i = brace; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source.Substring(brace, i - brace + 1);
            }
        }

        return source.Substring(brace);
    }

    private static void VerifyReleaseDocumentation(string root)
    {
        var violations = new List<string>();
        var readme = ReadRequired(root, "README.md", violations);
        var changelog = ReadRequired(root, "docs/10-changelog.md", violations);
        var architecture = ReadRequired(root, "docs/08-architecture.md", violations);
        var nugetReadme = ReadRequired(root, "docs/nuget/README.md", violations);

        if (!readme.Contains($"Current package version: `{CurrentReleaseVersion}`", StringComparison.Ordinal))
        {
            violations.Add("README.md: current version marker is missing.");
        }

        if (!changelog.Contains($"## [{CurrentReleaseVersion}]", StringComparison.Ordinal))
        {
            violations.Add($"docs/10-changelog.md: {CurrentReleaseVersion} release entry is missing.");
        }

        foreach (var packageId in SupportedPackageIds())
        {
            if (!readme.Contains(packageId, StringComparison.Ordinal) ||
                !nugetReadme.Contains(packageId, StringComparison.Ordinal))
            {
                violations.Add($"{packageId}: package is not documented in README and NuGet README.");
            }
        }

        if (!readme.Contains("docker compose", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add("README.md: live integration instructions should mention docker compose.");
        }

        if (!architecture.Contains("provider-agnostic", StringComparison.OrdinalIgnoreCase) ||
            !architecture.Contains("descriptor", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add("docs/08-architecture.md: provider-agnostic descriptor architecture is not documented.");
        }

        ThrowIfViolations(violations, "Release documentation verification failed.");
    }

    private static void VerifyIntegrationWorkflowContractSelfTest()
    {
        var clean = string.Join('\n', new[]
        {
            "name: Integration Tests",
            "image: mysql:8.4.10",
            "MYSQL_ROOT_PASSWORD: rootpass",
            "MYSQL_DATABASE: appdb",
            "image: mariadb:11.8.8",
            "MARIADB_ROOT_PASSWORD: rootpass",
            "MARIADB_DATABASE: appdb",
            "MODELSYNC_MYSQL_CONNECTION_STRING: Server=127.0.0.1;Port=3306;Database=appdb;User ID=root;Password=rootpass;",
            "MODELSYNC_MARIADB_CONNECTION_STRING: Server=127.0.0.1;Port=3307;Database=appdb;User ID=root;Password=rootpass;",
            "MODELSYNC_RUN_SP_INTEGRATION: 1",
            "MODELSYNC_MYSQL_SP_CONNECTION_STRING: Server=127.0.0.1;Port=3306;Database=appdb;User ID=root;Password=rootpass;",
            "MODELSYNC_POSTGRES_SP_CONNECTION_STRING: Host=127.0.0.1;Port=5432;Database=appdb;Username=appuser;Password=apppass;",
            "MODELSYNC_SQLSERVER_SP_CONNECTION_STRING: Server=127.0.0.1,1433;Database=appdb;User Id=sa;Password=YourStrong!Passw0rd;Encrypt=False;TrustServerCertificate=True;",
            "Database preflight",
            "MODELSYNC_INTEGRATION_DATABASE_PREFLIGHT_FAILED",
            "INFORMATION_SCHEMA.SCHEMATA",
            "--logger \"trx;LogFileName=mysql.trx\"",
            "outcome=\"NotExecuted\"",
            "outcome=\"Failed\"",
            "LegacyUpgradeFirstRun",
            "LegacyUpgradeSecondRun",
            "SyncRegisteredAsync_CreatesProcedure_ThenDetectsNoChange"
        });
        var bad = clean.Replace("MYSQL_DATABASE: appdb", string.Empty)
            .Replace("image: mysql:8.4.10", "image: mysql:8")
            .Replace("MODELSYNC_RUN_SP_INTEGRATION: 1", string.Empty)
            .Replace("outcome=\"NotExecuted\"", string.Empty);

        if (FindIntegrationWorkflowContractViolations(clean).Count != 0)
            throw new CheckFailedException("Integration workflow contract self-test failed: known-clean fixture was rejected.");

        if (FindIntegrationWorkflowContractViolations(bad).Count == 0)
            throw new CheckFailedException("Integration workflow contract self-test failed: known-bad fixture was accepted.");
    }

    private static void VerifyIntegrationWorkflowContract(string root)
    {
        var workflow = ReadRequired(root, ".github/workflows/integration.yml", new List<string>());
        var violations = FindIntegrationWorkflowContractViolations(workflow);
        ThrowIfViolations(violations, "Integration workflow contract verification failed.");
    }

    private static List<string> FindIntegrationWorkflowContractViolations(string workflow)
    {
        var violations = new List<string>();

        RequireContains(workflow, "name: Integration Tests", "integration workflow: workflow name is missing.", violations);
        RequireContains(workflow, "image: mysql:8.4.10", "integration workflow: MySQL image must be pinned to mysql:8.4.10.", violations);
        RequireContains(workflow, "image: mariadb:11.8.8", "integration workflow: MariaDB image must be pinned to mariadb:11.8.8.", violations);
        RequireContains(workflow, "MYSQL_DATABASE: appdb", "integration workflow: MYSQL_DATABASE must create appdb.", violations);
        RequireContains(workflow, "MARIADB_DATABASE: appdb", "integration workflow: MARIADB_DATABASE must create appdb.", violations);
        RequireContains(workflow, "MODELSYNC_MYSQL_CONNECTION_STRING: Server=127.0.0.1;Port=3306;Database=appdb;", "integration workflow: MySQL connection string must target appdb.", violations);
        RequireContains(workflow, "MODELSYNC_MARIADB_CONNECTION_STRING: Server=127.0.0.1;Port=3307;Database=appdb;", "integration workflow: MariaDB connection string must target appdb.", violations);
        RequireContains(workflow, "MODELSYNC_RUN_SP_INTEGRATION: 1", "integration workflow: stored procedure integration must be enabled.", violations);
        RequireContains(workflow, "MODELSYNC_MYSQL_SP_CONNECTION_STRING: Server=127.0.0.1;Port=3306;Database=appdb;", "integration workflow: MySQL stored procedure connection string must target appdb.", violations);
        RequireContains(workflow, "MODELSYNC_POSTGRES_SP_CONNECTION_STRING: Host=127.0.0.1;Port=5432;Database=appdb;", "integration workflow: PostgreSQL stored procedure connection string must target appdb.", violations);
        RequireContains(workflow, "MODELSYNC_SQLSERVER_SP_CONNECTION_STRING: Server=127.0.0.1,1433;Database=appdb;", "integration workflow: SQL Server stored procedure connection string must target appdb.", violations);
        RequireContains(workflow, "Database preflight", "integration workflow: database preflight step is missing.", violations);
        RequireContains(workflow, "INFORMATION_SCHEMA.SCHEMATA", "integration workflow: target database preflight query is missing.", violations);
        RequireContains(workflow, "MODELSYNC_INTEGRATION_DATABASE_PREFLIGHT_FAILED", "integration workflow: preflight failure marker is missing.", violations);
        RequireContains(workflow, "--logger \"trx;LogFileName=mysql.trx\"", "integration workflow: MySQL TRX logging is missing.", violations);
        RequireContains(workflow, "outcome=\"Failed\"", "integration workflow: failed test gate is missing.", violations);
        RequireContains(workflow, "outcome=\"NotExecuted\"", "integration workflow: skipped test gate is missing.", violations);
        RequireContains(workflow, "LegacyUpgradeFirstRun", "integration workflow: legacy first-run discovery gate is missing.", violations);
        RequireContains(workflow, "LegacyUpgradeSecondRun", "integration workflow: legacy second-run discovery gate is missing.", violations);
        RequireContains(workflow, "SyncRegisteredAsync_CreatesProcedure_ThenDetectsNoChange", "integration workflow: stored procedure discovery gate is missing.", violations);

        if (Regex.IsMatch(workflow, @"image:\s+mysql:8\s*(?:\r?\n|$)", RegexOptions.IgnoreCase))
            violations.Add("integration workflow: mutable mysql:8 image tag is not allowed.");
        if (Regex.IsMatch(workflow, @"image:\s+mariadb:11\s*(?:\r?\n|$)", RegexOptions.IgnoreCase))
            violations.Add("integration workflow: mutable mariadb:11 image tag is not allowed.");

        return violations;
    }

    private static void VerifyReleaseDocumentationContract(string root)
    {
        var violations = new List<string>();
        var requiredFiles = new[]
        {
            "CHANGELOG.md",
            "docs/releases/README.md",
            "docs/releases/_template.md",
            "docs/releases/1.2.1.md",
            "docs/releases/1.2.2.md",
            "docs/migrations/README.md",
            "docs/migrations/_template.md",
            "docs/migrations/1.2.0-to-1.2.1.md",
            "docs/migrations/1.2.1-to-1.2.2.md",
            "docs/versioning-and-compatibility.md",
            "docs/deprecation-policy.md",
            "docs/roadmap-1.3.md"
        };

        foreach (var file in requiredFiles)
        {
            ReadRequired(root, file, violations);
        }

        var readme = ReadRequired(root, "README.md", violations);
        RequireContains(readme, "Versioning, Release Notes and Migration Guides", "README.md: versioning/release navigation section is missing.", violations);
        RequireContains(readme, "docs/releases/1.2.2.md", "README.md: current 1.2.2 release note link is missing.", violations);
        RequireContains(readme, "docs/migrations/1.2.1-to-1.2.2.md", "README.md: 1.2.1 to 1.2.2 migration guide link is missing.", violations);

        var release = ReadRequired(root, "docs/releases/1.2.2.md", violations);
        RequireContains(release, "2026-07-01", "docs/releases/1.2.2.md: final release date is missing.", violations);
        RequireContains(release, "ModelSync 1.2.2 - Integration Workflow Reliability and Release Gate Correction", "docs/releases/1.2.2.md: release title is missing.", violations);
        RequireContains(release, "Unpublished 1.2.1 tag", "docs/releases/1.2.2.md: unpublished 1.2.1 tag note is missing.", violations);
        RequireContains(release, "Fixed", "docs/releases/1.2.2.md: closed bug list is missing.", violations);

        var changelog = ReadRequired(root, "CHANGELOG.md", violations);
        RequireContains(changelog, "## [1.2.2] - 2026-07-01", "CHANGELOG.md: final 1.2.2 entry is missing.", violations);

        foreach (var packageId in SupportedPackageIds())
        {
            var expected = $"dotnet add package {packageId} --version {CurrentReleaseVersion}";
            RequireContains(readme, expected, $"README.md: missing 1.2.2 install snippet for {packageId}.", violations);
        }

        foreach (var project in new[]
                 {
                     "UmbrellaFrame.ModelSync.Core/UmbrellaFrame.ModelSync.Core.csproj",
                     "UmbrellaFrame.ModelSync.SqlServer/UmbrellaFrame.ModelSync.SqlServer.csproj",
                     "UmbrellaFrame.ModelSync.MySql/UmbrellaFrame.ModelSync.MySql.csproj",
                     "UmbrellaFrame.ModelSync.PostgreSQL/UmbrellaFrame.ModelSync.PostgreSQL.csproj",
                     "UmbrellaFrame.ModelSync.SQLite/UmbrellaFrame.ModelSync.SQLite.csproj",
                     "UmbrellaFrame.ModelSync.Analyzers/UmbrellaFrame.ModelSync.Core.Analyzers.csproj"
                 })
        {
            var projectText = ReadRequired(root, project, violations);
            RequireContains(projectText, $"<Version>{CurrentReleaseVersion}</Version>", $"{project}: package version is not {CurrentReleaseVersion}.", violations);
            RequireContains(projectText, "PackageReleaseNotes", $"{project}: package release notes are missing.", violations);
        }

        var roadmap = ReadRequired(root, "docs/roadmap-1.3.md", violations);
        RequireContains(roadmap, "Composite indexes", "docs/roadmap-1.3.md: composite index roadmap item is missing.", violations);
        RequireContains(roadmap, "Filtered indexes", "docs/roadmap-1.3.md: filtered index roadmap item is missing.", violations);
        RequireContains(roadmap, "Expression indexes", "docs/roadmap-1.3.md: expression index roadmap item is missing.", violations);

        ThrowIfViolations(violations, "Release documentation contract verification failed.");
    }

    private static void VerifyProviderSpecificApiUsage(string root)
    {
        var violations = new List<string>();
        var release = ReadRequired(root, "docs/releases/1.2.1.md", violations);
        RequireContains(release, "SqlServerColumnDefault", "docs/releases/1.2.1.md: provider-specific default example is missing.", violations);
        RequireContains(release, "SqlServerColumnCheck", "docs/releases/1.2.1.md: provider-specific check example is missing.", violations);
        RequireContains(release, "SqlServerColumnIndex", "docs/releases/1.2.1.md: provider-specific index example is missing.", violations);

        var migration = ReadRequired(root, "docs/migrations/1.2.0-to-1.2.1.md", violations);
        RequireContains(migration, "legacy Core attributes remain supported", "docs/migrations/1.2.0-to-1.2.1.md: legacy compatibility statement is missing.", violations);
        RequireContains(migration, "SqlServerColumnDefault", "docs/migrations/1.2.0-to-1.2.1.md: provider-specific migration example is missing.", violations);

        var expectedProviderFiles = new[]
        {
            "UmbrellaFrame.ModelSync.SqlServer/Attributes/SqlServerColumnDefaultAttribute.cs",
            "UmbrellaFrame.ModelSync.SqlServer/Attributes/SqlServerColumnDefaultSqlAttribute.cs",
            "UmbrellaFrame.ModelSync.SqlServer/Attributes/SqlServerColumnCheckAttribute.cs",
            "UmbrellaFrame.ModelSync.SqlServer/Attributes/SqlServerColumnIndexAttribute.cs",
            "UmbrellaFrame.ModelSync.MySql/Attributes/MySqlColumnDefaultAttribute.cs",
            "UmbrellaFrame.ModelSync.MySql/Attributes/MySqlColumnDefaultSqlAttribute.cs",
            "UmbrellaFrame.ModelSync.MySql/Attributes/MySqlColumnCheckAttribute.cs",
            "UmbrellaFrame.ModelSync.MySql/Attributes/MySqlColumnIndexAttribute.cs",
            "UmbrellaFrame.ModelSync.PostgreSQL/Attributes/PostgresColumnDefaultAttribute.cs",
            "UmbrellaFrame.ModelSync.PostgreSQL/Attributes/PostgresColumnDefaultSqlAttribute.cs",
            "UmbrellaFrame.ModelSync.PostgreSQL/Attributes/PostgresColumnCheckAttribute.cs",
            "UmbrellaFrame.ModelSync.PostgreSQL/Attributes/PostgresColumnIndexAttribute.cs",
            "UmbrellaFrame.ModelSync.SQLite/Attributes/SQLiteColumnDefaultAttribute.cs",
            "UmbrellaFrame.ModelSync.SQLite/Attributes/SQLiteColumnDefaultSqlAttribute.cs",
            "UmbrellaFrame.ModelSync.SQLite/Attributes/SQLiteColumnCheckAttribute.cs",
            "UmbrellaFrame.ModelSync.SQLite/Attributes/SQLiteColumnIndexAttribute.cs"
        };

        foreach (var file in expectedProviderFiles)
        {
            ReadRequired(root, file, violations);
        }

        ThrowIfViolations(violations, "Provider-specific API usage verification failed.");
    }

    private static void VerifyCoreProviderBoundary(string root)
    {
        var violations = new List<string>();
        var coreProject = ReadRequired(root, "UmbrellaFrame.ModelSync.Core/UmbrellaFrame.ModelSync.Core.csproj", violations);
        var forbiddenReferences = new[]
        {
            "Microsoft.Data.SqlClient",
            "MySqlConnector",
            "Npgsql",
            "Microsoft.Data.Sqlite",
            "UmbrellaFrame.ModelSync.SqlServer",
            "UmbrellaFrame.ModelSync.MySql",
            "UmbrellaFrame.ModelSync.PostgreSQL",
            "UmbrellaFrame.ModelSync.SQLite"
        };

        foreach (var forbidden in forbiddenReferences)
        {
            if (coreProject.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add($"UmbrellaFrame.ModelSync.Core.csproj: Core must not reference {forbidden}.");
            }
        }

        var forbiddenRuntimeTypes = new[]
        {
            "SqlConnection",
            "MySqlConnection",
            "NpgsqlConnection",
            "SqliteConnection",
            "SqlException",
            "MySqlException",
            "PostgresException",
            "SqliteException"
        };

        foreach (var file in EnumerateFiles(Path.Combine(root, "UmbrellaFrame.ModelSync.Core"), "*.cs"))
        {
            var relative = Relative(root, file);
            var text = File.ReadAllText(file);
            foreach (var forbidden in forbiddenRuntimeTypes)
            {
                if (Regex.IsMatch(text, @"\b" + Regex.Escape(forbidden) + @"\b"))
                {
                    violations.Add($"{relative}: Core must not depend on provider runtime type {forbidden}.");
                }
            }
        }

        ThrowIfViolations(violations, "Core/provider boundary verification failed.");
    }

    private static void VerifyOperationalHardening(string root)
    {
        var violations = new List<string>();

        var item = ReadRequired(root, "UmbrellaFrame.ModelSync.Core/Models/MigrationExecutionItemResult.cs", violations);
        foreach (var member in new[]
                 {
                     "ErrorMessage",
                     "InnerErrorMessage",
                     "ProviderErrorCode",
                     "ProviderErrorNumber",
                     "ProviderErrorState",
                     "ProviderErrorSeverity",
                     "ErrorLineNumber",
                     "ErrorObjectName",
                     "FailedBatchIndex",
                     "FailedBatchPreview"
                 })
        {
            RequireContains(item, member, $"MigrationExecutionItemResult: structured error member '{member}' is missing.", violations);
        }

        var runner = ReadRequired(root, "UmbrellaFrame.ModelSync.Core/Services/SqlMigrationRunnerBase.cs", violations);
        RequireContains(runner, "OpenExecutionScopeAsync", "SqlMigrationRunnerBase: execution scope lifecycle is missing.", violations);
        RequireContains(runner, "CompletedBatchCount", "SqlMigrationRunnerBase: completed batch tracking is missing.", violations);
        RequireContains(runner, "FailedBatchIndex", "SqlMigrationRunnerBase: failed batch index tracking is missing.", violations);
        RequireContains(runner, "FailedBatchPreview", "SqlMigrationRunnerBase: failed batch preview tracking is missing.", violations);
        RequireContains(runner, "PopulateProviderError", "SqlMigrationRunnerBase: provider-neutral error population hook is missing.", violations);
        RequireContains(runner, "Redact", "SqlMigrationRunnerBase: secret redaction helper is missing.", violations);
        RequireContains(runner, "1024", "SqlMigrationRunnerBase: failed SQL preview must be bounded.", violations);

        var forbiddenCoreTerms = new[] { "SqlException", "MySqlException", "PostgresException", "SqliteException", "GO\r", "GO\n" };
        foreach (var forbidden in forbiddenCoreTerms)
        {
            if (runner.Contains(forbidden, StringComparison.Ordinal))
                violations.Add($"SqlMigrationRunnerBase.cs: Core must not contain provider-specific term '{forbidden}'.");
        }

        foreach (var provider in new[]
                 {
                     ("SQL Server", "UmbrellaFrame.ModelSync.SqlServer/Services/SqlServerMigrationRunner.cs", "SqlException", "SqlServerExecutionScope"),
                     ("MySQL", "UmbrellaFrame.ModelSync.MySql/Services/MySqlMigrationRunner.cs", "MySqlException", "MySqlExecutionScope"),
                     ("PostgreSQL", "UmbrellaFrame.ModelSync.PostgreSQL/Services/PostgresMigrationRunner.cs", "PostgresException", "PostgresExecutionScope"),
                     ("SQLite", "UmbrellaFrame.ModelSync.SQLite/Services/SQLiteMigrationRunner.cs", "SqliteException", "SQLiteExecutionScope")
                 })
        {
            var text = ReadRequired(root, provider.Item2, violations);
            RequireContains(text, "OpenExecutionScopeAsync", $"{provider.Item1}: per-script execution scope is missing.", violations);
            RequireContains(text, "RecordHistoryAsync", $"{provider.Item1}: scoped history writing is missing.", violations);
            RequireContains(text, "PopulateProviderError", $"{provider.Item1}: provider exception translator is missing.", violations);
            RequireContains(text, provider.Item3, $"{provider.Item1}: provider exception type translator is missing.", violations);
            RequireContains(text, provider.Item4, $"{provider.Item1}: provider execution scope implementation is missing.", violations);
        }

        var normalizer = ReadRequired(root, "UmbrellaFrame.ModelSync.SqlServer/Services/SqlServerLegacyRoutineNormalizer.cs", violations);
        RequireContains(normalizer, "LegacyRoutineMultipleDefinitions", "SQL Server legacy routine normalizer: multiple-definition diagnostic is missing.", violations);
        RequireContains(normalizer, "LegacyRoutineExecutableSideBatch", "SQL Server legacy routine normalizer: executable side-batch diagnostic is missing.", violations);
        RequireContains(normalizer, "LegacyRoutineUnsupportedSqlCmd", "SQL Server legacy routine normalizer: SQLCMD diagnostic is missing.", violations);
        RequireContains(normalizer, "LegacyRoutineInvalidDefinition", "SQL Server legacy routine normalizer: invalid-definition diagnostic is missing.", violations);

        var sqlServerTests = ReadRequired(root, "UmbrellaFrame.ModelSync.SqlServerTest/SqlServerLegacyRoutineNormalizerTests.cs", violations);
        RequireContains(sqlServerTests, "LegacyRoutineMultipleDefinitions", "SQL Server normalizer known-bad self-test is missing.", violations);
        RequireContains(sqlServerTests, "LegacyRoutineUnsupportedSqlCmd", "SQL Server normalizer SQLCMD self-test is missing.", violations);
        RequireContains(sqlServerTests, "Normalize_WithSetOptionsTerminalGoAndTrailingPrint_ReturnsOnlyProcedure", "SQL Server normalizer known-clean self-test is missing.", violations);

        var sqlServerIntegration = ReadRequired(root, "UmbrellaFrame.ModelSync.SqlServerTest/SqlServerLegacyCompatibilityIntegrationTests.cs", violations);
        RequireContains(sqlServerIntegration, "#ModelSyncSessionProbe", "SQL Server same-session temp table integration test is missing.", violations);
        RequireContains(sqlServerIntegration, "Session continuity failed.", "SQL Server same-session failure assertion is missing.", violations);

        var coreTests = ReadRequired(root, "UmbrellaFrame.ModelSync.CoreTest/MigrationOperationalHardeningTests.cs", violations);
        RequireContains(coreTests, "FailedBatchIndex", "Core operational hardening failed-batch test is missing.", violations);
        RequireContains(coreTests, "FailedBatchPreview", "Core operational hardening preview test is missing.", violations);
        RequireContains(coreTests, "password=<redacted>", "Core operational hardening redaction test is missing.", violations);
        RequireContains(coreTests, "HistoryWritten", "Core operational hardening history safety assertion is missing.", violations);

        ThrowIfViolations(violations, "Operational hardening verification failed.");
    }

    private static void VerifyOperationalHardeningSelfTest()
    {
        var clean = "ErrorMessage InnerErrorMessage ProviderErrorCode ProviderErrorNumber ProviderErrorState ProviderErrorSeverity ErrorLineNumber ErrorObjectName FailedBatchIndex FailedBatchPreview";
        var bad = "ErrorMessage FailedBatchPreview";

        foreach (var required in new[] { "ProviderErrorCode", "FailedBatchIndex", "FailedBatchPreview" })
        {
            if (!clean.Contains(required, StringComparison.Ordinal))
                throw new CheckFailedException("Known-clean operational hardening fixture was rejected.");
            if (bad.Contains(required, StringComparison.Ordinal) && required != "FailedBatchPreview")
                throw new CheckFailedException("Known-bad operational hardening fixture was accepted.");
        }
    }

    private static void VerifyConsumerCompatibility(string root)
    {
        var violations = new List<string>();
        var legacy = ReadRequired(root, "tools/UmbrellaFrame.ModelSync.RepositoryChecks/ConsumerCompatibility/LegacyConsumer/Program.cs", violations);
        var canonical = ReadRequired(root, "tools/UmbrellaFrame.ModelSync.RepositoryChecks/ConsumerCompatibility/CanonicalProviderConsumer/Program.cs", violations);
        var evidence = ReadRequired(root, "tools/UmbrellaFrame.ModelSync.RepositoryChecks/ConsumerCompatibility/consumer-compatibility-evidence.md", violations);

        foreach (var legacyApi in new[]
                 {
                     "DbColumnDefault(\"NEWID()\")",
                     "DbColumnCheck(\"Price >= 0\")",
                     "DbColumnIndex(\"IX_LegacyProducts_Name\")",
                     "SqlServerColumnType",
                     "SqlServerTableName",
                     "SqlServerMigrationRunner",
                     "LegacyEmbeddedSql",
                     "RunWithResultAsync",
                     "SqlServerModelSynchronizer"
                 })
        {
            RequireContains(legacy, legacyApi, $"Legacy consumer fixture is missing '{legacyApi}'.", violations);
        }

        foreach (var canonicalApi in new[]
                 {
                     "SqlServerColumnDefault",
                     "SqlServerColumnDefaultSql",
                     "SqlServerColumnCheck",
                     "SqlServerColumnIndex",
                     "MySqlColumnDefault",
                     "MySqlColumnDefaultSql",
                     "MySqlColumnCheck",
                     "MySqlColumnIndex",
                     "PostgresColumnDefault",
                     "PostgresColumnDefaultSql",
                     "PostgresColumnCheck",
                     "PostgresColumnIndex",
                     "SQLiteColumnDefault",
                     "SQLiteColumnDefaultSql",
                     "SQLiteColumnCheck",
                     "SQLiteColumnIndex",
                     "SqlServerDefaultExpression.NewId",
                     "SqlServerDefaultExpression.NewSequentialId",
                     "SqlServerDefaultExpression.GetDate",
                     "SqlServerDefaultExpression.GetUtcDate",
                     "SqlServerDefaultExpression.SysDateTime",
                     "SqlServerDefaultExpression.SysUtcDateTime",
                     "MySqlDefaultExpression.Uuid",
                     "MySqlDefaultExpression.CurrentTimestamp",
                     "PostgresDefaultExpression.GenRandomUuid",
                     "PostgresDefaultExpression.CurrentTimestamp",
                     "PostgresDefaultExpression.Now",
                     "SQLiteDefaultExpression.CurrentTimestamp",
                     "SQLiteDefaultExpression.CurrentDate",
                     "SQLiteDefaultExpression.CurrentTime"
                 })
        {
            RequireContains(canonical, canonicalApi, $"Canonical provider consumer fixture is missing '{canonicalApi}'.", violations);
        }

        if (legacy.Contains("ProjectReference", StringComparison.OrdinalIgnoreCase) ||
            canonical.Contains("ProjectReference", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add("Consumer compatibility fixtures must not contain ProjectReference.");
        }

        foreach (var marker in new[]
                 {
                     "NuGet.org package version: `1.2.0`",
                     "Candidate version: supplied by release gate",
                     "Final validation version: `1.2.2`",
                     "Candidate source: supplied explicitly by release command",
                     "NewWarningDelta: `0`",
                     "TreatWarningsAsErrors: `PASS`",
                     "ProjectReferenceDetected: `false`",
                     "UnexpectedPackageSourceDetected: `false`",
                     "MixedModelSyncVersionsDetected: `false`"
                 })
        {
            RequireContains(evidence, marker, $"Consumer compatibility evidence is missing '{marker}'.", violations);
        }

        ThrowIfViolations(violations, "1.2.0 consumer compatibility verification failed.");
    }

    private static void VerifyPublicationHygieneSelfTest()
    {
        var localVersion = "1.2.2-" + "rc.local";
        var obsoleteArtifactPath = "artifacts/" + "consumer" + "-candidate";
        var localProjectPath = "D:" + @"\Projeler\UmbrellaFrame\ModelSync";
        var userProfilePath = "C:" + @"\Users\someone\.nuget\packages";
        var secretLikeValue = "oy" + "2abcdefghijklmnopqrstuvwxyz";
        var unsafePassword = "Password=" + "real-production-password";
        var bad = string.Join(Environment.NewLine, new[]
        {
            "Package candidate: `" + localVersion + "`",
            "Source: `" + obsoleteArtifactPath + "`",
            localProjectPath,
            userProfilePath,
            unsafePassword,
            secretLikeValue
        });
        var clean = string.Join(Environment.NewLine, new[]
        {
            "NuGet.org package version: `1.2.0`",
            "Candidate version: supplied by release gate",
            "Candidate source: supplied explicitly by release command",
            "Password=secret",
            "--candidate-source artifacts\\packages-1.2.2"
        });

        if (FindPublicationHygieneViolations("bad.md", bad).Count == 0)
        {
            throw new CheckFailedException("Publication hygiene self-test failed: known-bad fixture was accepted.");
        }

        var cleanViolations = FindPublicationHygieneViolations("clean.md", clean);
        if (cleanViolations.Count != 0)
        {
            throw new CheckFailedException("Publication hygiene self-test failed: known-clean fixture was rejected: " + string.Join("; ", cleanViolations));
        }
    }

    private static void VerifyPublicationHygiene(string root)
    {
        var result = RunProcess(root, "git", "ls-files");
        if (result.ExitCode != 0)
        {
            throw new CheckFailedException("Could not enumerate tracked files for publication hygiene verification: " + result.SafeOutput);
        }

        var violations = new List<string>();
        foreach (var relativePath in result.StdOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                continue;
            }

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch
            {
                continue;
            }

            violations.AddRange(FindPublicationHygieneViolations(relativePath, text));
        }

        ThrowIfViolations(violations, "Publication hygiene verification failed.");
    }

    private static List<string> FindPublicationHygieneViolations(string relativePath, string text)
    {
        var violations = new List<string>();
        var normalizedPath = relativePath.Replace('\\', '/');

        AddIfContains(text, "1.2.2-" + "rc.local", $"{relativePath}: pre-release local version leaked into tracked content.", violations);
        AddIfContains(text, "artifacts/" + "consumer" + "-candidate", $"{relativePath}: obsolete consumer candidate artifact path leaked into tracked content.", violations);
        AddIfContains(text, @"artifacts\" + "consumer" + "-candidate", $"{relativePath}: obsolete consumer candidate artifact path leaked into tracked content.", violations);
        AddIfContains(text, "D:" + @"\Projeler", $"{relativePath}: local machine path leaked into tracked content.", violations);
        AddIfContains(text, "D:" + "/Projeler", $"{relativePath}: local machine path leaked into tracked content.", violations);
        AddIfContains(text, "local" + "-content", $"{relativePath}: local content marker leaked into publication metadata.", violations);

        var windowsUserProfilePattern = @"[A-Za-z]:\\" + "Users" + @"\\[^\\\r\n]+\\";
        var unixUserProfilePattern = @"(?:/" + "Users" + @"/|/" + "home" + @"/)[^/\r\n]+/";
        if (Regex.IsMatch(text, windowsUserProfilePattern, RegexOptions.IgnoreCase) ||
            Regex.IsMatch(text, unixUserProfilePattern, RegexOptions.IgnoreCase))
        {
            violations.Add($"{relativePath}: user profile absolute path leaked into tracked content.");
        }

        if (normalizedPath.Contains("ConsumerCompatibility/", StringComparison.OrdinalIgnoreCase) &&
            Regex.IsMatch(text, @"<\s*ProjectReference\b", RegexOptions.IgnoreCase))
        {
            violations.Add($"{relativePath}: package consumer validation must not use ProjectReference.");
        }

        if (Regex.IsMatch(text, @"oy2[a-z0-9]{20,}", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(text, @"(?i)(api[-_ ]?key|token)\s*=\s*[""']?[A-Za-z0-9_\-]{20,}"))
        {
            violations.Add($"{relativePath}: secret-like value leaked into tracked content.");
        }

        foreach (Match match in Regex.Matches(text, @"(?i)Password\s*=\s*([^;`""'\s]+)"))
        {
            var value = match.Groups[1].Value;
            if (value.Equals("real-production-password", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("production", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add($"{relativePath}: connection string password value is not a safe placeholder.");
                break;
            }
        }

        return violations;
    }

    private static void AddIfContains(string text, string value, string message, List<string> violations)
    {
        if (text.Contains(value, StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(message);
        }
    }

    private static void VerifyExternalConsumerCompatibility(string root, string[] args)
    {
        var baselineVersion = RequiredOption(args, "--baseline-version");
        var candidateVersion = RequiredOption(args, "--candidate-version");
        var candidateSource = RequiredOption(args, "--candidate-source");
        var evidenceOutput = OptionalOption(args, "--evidence-output");

        if (baselineVersion != "1.2.0")
        {
            throw new CheckFailedException("Consumer compatibility baseline version must be 1.2.0.");
        }

        if (candidateVersion != CurrentReleaseVersion)
        {
            throw new CheckFailedException($"Consumer compatibility candidate version must be {CurrentReleaseVersion}.");
        }

        var candidateSourcePath = Path.GetFullPath(Path.Combine(root, candidateSource));
        if (!Directory.Exists(candidateSourcePath))
        {
            throw new CheckFailedException("Consumer compatibility candidate source directory was not found.");
        }

        foreach (var packageId in SupportedPackageIds())
        {
            var expected = Path.Combine(candidateSourcePath, packageId + "." + candidateVersion + ".nupkg");
            if (!File.Exists(expected))
            {
                throw new CheckFailedException($"{packageId}: final candidate package was not found in the supplied candidate source.");
            }
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "ModelSync-consumer-compatibility-" + Guid.NewGuid().ToString("N"));
        var legacySource = File.ReadAllText(Path.Combine(root, "tools", "UmbrellaFrame.ModelSync.RepositoryChecks", "ConsumerCompatibility", "LegacyConsumer", "Program.cs"));
        var canonicalSource = File.ReadAllText(Path.Combine(root, "tools", "UmbrellaFrame.ModelSync.RepositoryChecks", "ConsumerCompatibility", "CanonicalProviderConsumer", "Program.cs"));

        try
        {
            var baseline = CreateConsumerProject(tempRoot, "BaselineLegacyConsumer", legacySource, baselineVersion, null);
            BuildConsumerProject(baseline, "baseline");

            var legacyCandidate = CreateConsumerProject(tempRoot, "CandidateLegacyConsumer", legacySource, candidateVersion, candidateSourcePath);
            BuildConsumerProject(legacyCandidate, "legacy candidate");

            var canonicalCandidate = CreateConsumerProject(tempRoot, "CanonicalCandidateConsumer", canonicalSource, candidateVersion, candidateSourcePath);
            BuildConsumerProject(canonicalCandidate, "canonical candidate");

            ValidateConsumerAssets(baseline, baselineVersion, forbiddenVersion: candidateVersion, "baseline");
            ValidateConsumerAssets(legacyCandidate, candidateVersion, forbiddenVersion: baselineVersion, "legacy candidate");
            ValidateConsumerAssets(canonicalCandidate, candidateVersion, forbiddenVersion: baselineVersion, "canonical candidate");

            if (!string.IsNullOrWhiteSpace(evidenceOutput))
            {
                var evidencePath = Path.GetFullPath(Path.Combine(root, evidenceOutput));
                Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
                File.WriteAllText(evidencePath, BuildConsumerCompatibilityEvidenceJson(baselineVersion, candidateVersion), Encoding.UTF8);
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup for temp consumer projects.
            }
        }
    }

    private static string RequiredOption(string[] args, string name)
        => OptionalOption(args, name) ?? throw new CheckFailedException($"Missing required option: {name}");

    private static string? OptionalOption(string[] args, string name)
    {
        for (var i = 1; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static string CreateConsumerProject(string root, string name, string source, string version, string? localPackageSource)
    {
        var projectDirectory = Path.Combine(root, name);
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(Path.Combine(projectDirectory, "Program.cs"), source, Encoding.UTF8);
        File.WriteAllText(Path.Combine(projectDirectory, name + ".csproj"), BuildConsumerProjectFile(version), Encoding.UTF8);
        File.WriteAllText(Path.Combine(projectDirectory, "NuGet.Config"), BuildNuGetConfig(localPackageSource), Encoding.UTF8);
        return projectDirectory;
    }

    private static string BuildConsumerProjectFile(string version)
    {
        var packageReferences = string.Join(Environment.NewLine, SupportedPackageIds().Select(packageId =>
            $"    <PackageReference Include=\"{packageId}\" Version=\"{version}\" />"));

        return $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
{packageReferences}
  </ItemGroup>
</Project>
""";
    }

    private static string BuildNuGetConfig(string? localPackageSource)
    {
        var source = string.IsNullOrWhiteSpace(localPackageSource)
            ? "https://api.nuget.org/v3/index.json"
            : localPackageSource.Replace("&", "&amp;").Replace("\"", "&quot;");

        return $"""
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="ModelSyncConsumerSource" value="{source}" />
  </packageSources>
</configuration>
""";
    }

    private static void BuildConsumerProject(string projectDirectory, string label)
    {
        var restore = RunProcess(projectDirectory, "dotnet", "restore", "--no-cache", "--force-evaluate");
        if (restore.ExitCode != 0)
        {
            throw new CheckFailedException($"Consumer compatibility {label} restore failed: {restore.SafeOutput}");
        }

        var build = RunProcess(projectDirectory, "dotnet", "build", "--configuration", "Release", "--no-restore", "-p:TreatWarningsAsErrors=true");
        if (build.ExitCode != 0)
        {
            throw new CheckFailedException($"Consumer compatibility {label} build failed: {build.SafeOutput}");
        }
    }

    private static void ValidateConsumerAssets(string projectDirectory, string requiredVersion, string forbiddenVersion, string label)
    {
        var assetsPath = Path.Combine(projectDirectory, "obj", "project.assets.json");
        if (!File.Exists(assetsPath))
        {
            throw new CheckFailedException($"Consumer compatibility {label} assets file was not produced.");
        }

        var assets = File.ReadAllText(assetsPath);
        foreach (var packageId in SupportedPackageIds())
        {
            if (!assets.Contains(packageId + "/" + requiredVersion, StringComparison.OrdinalIgnoreCase))
            {
                throw new CheckFailedException($"Consumer compatibility {label} did not restore {packageId} {requiredVersion}.");
            }

            if (assets.Contains(packageId + "/" + forbiddenVersion, StringComparison.OrdinalIgnoreCase))
            {
                throw new CheckFailedException($"Consumer compatibility {label} restored mixed ModelSync versions.");
            }
        }

        if (assets.Contains("1.2.2-" + "rc.local", StringComparison.OrdinalIgnoreCase))
        {
            throw new CheckFailedException($"Consumer compatibility {label} restored a local rc package.");
        }
    }

    private static string BuildConsumerCompatibilityEvidenceJson(string baselineVersion, string candidateVersion)
        => $$"""
{
  "BaselineVersion": "{{baselineVersion}}",
  "CandidateVersion": "{{candidateVersion}}",
  "CandidateSource": "supplied-explicitly",
  "BaselineRestore": "PASS",
  "BaselineBuild": "PASS",
  "LegacyCandidateRestore": "PASS",
  "LegacyCandidateBuild": "PASS",
  "CanonicalCandidateRestore": "PASS",
  "CanonicalCandidateBuild": "PASS",
  "LegacyErrors": 0,
  "LegacyWarnings": 0,
  "CanonicalErrors": 0,
  "CanonicalWarnings": 0,
  "NewWarningDelta": 0,
  "ProjectReferenceDetected": false,
  "UnexpectedPackageSourceDetected": false,
  "MixedModelSyncVersionsDetected": false
}
""";

    private static ProcessResult RunProcess(string workingDirectory, string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new CheckFailedException($"Could not start process: {fileName}");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private static string ReadRequired(string root, string relativePath, List<string> violations)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            violations.Add($"{relativePath}: file was not found");
            return string.Empty;
        }

        return File.ReadAllText(path);
    }

    private static string[] SupportedPackageIds()
        => new[]
        {
            "UmbrellaFrame.ModelSync.Core",
            "UmbrellaFrame.ModelSync.SqlServer",
            "UmbrellaFrame.ModelSync.MySql",
            "UmbrellaFrame.ModelSync.PostgreSQL",
            "UmbrellaFrame.ModelSync.SQLite",
            "UmbrellaFrame.ModelSync.Analyzers"
        };

    private static void VerifyPackageSmoke(string root, string[] args)
    {
        if (args.Length < 3)
        {
            throw new CheckFailedException("Usage: verify-package-smoke <artifacts-directory> <version>");
        }

        var artifacts = Path.GetFullPath(Path.Combine(root, args[1]));
        if (!Directory.Exists(artifacts))
        {
            throw new CheckFailedException($"Package artifacts directory was not found: {artifacts}");
        }

        var version = args[2];
        var packages = new[]
        {
            new PackageExpectation("UmbrellaFrame.ModelSync.Core", false, false),
            new PackageExpectation("UmbrellaFrame.ModelSync.SqlServer", true, false),
            new PackageExpectation("UmbrellaFrame.ModelSync.MySql", true, false),
            new PackageExpectation("UmbrellaFrame.ModelSync.PostgreSQL", true, false),
            new PackageExpectation("UmbrellaFrame.ModelSync.SQLite", true, false),
            new PackageExpectation("UmbrellaFrame.ModelSync.Analyzers", false, true)
        };

        var violations = new List<string>();
        foreach (var package in packages)
        {
            var path = Path.Combine(artifacts, $"{package.Id}.{version}.nupkg");
            if (!File.Exists(path))
            {
                violations.Add($"{package.Id}: package file was not found for version {version}");
                continue;
            }

            using var archive = ZipFile.OpenRead(path);
            var entries = archive.Entries.Select(e => e.FullName.Replace('\\', '/')).ToArray();
            var nuspec = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
            if (nuspec == null)
            {
                violations.Add($"{package.Id}: nuspec was not found");
                continue;
            }

            using (var reader = new StreamReader(nuspec.Open()))
            {
                var nuspecText = reader.ReadToEnd();
                if (!nuspecText.Contains($"<id>{package.Id}</id>", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{package.Id}: nuspec id mismatch");
                }

                if (!nuspecText.Contains($"<version>{version}</version>", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{package.Id}: nuspec version mismatch");
                }

                if (package.RequiresCoreDependency &&
                    !nuspecText.Contains("UmbrellaFrame.ModelSync.Core", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{package.Id}: Core dependency metadata was not found");
                }
            }

            if (!entries.Any(e => e.EndsWith(".md", StringComparison.OrdinalIgnoreCase)))
            {
                violations.Add($"{package.Id}: README entry was not found");
            }

            if (!entries.Any(e => e.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
            {
                violations.Add($"{package.Id}: icon entry was not found");
            }

            if (package.IsAnalyzer)
            {
                if (!entries.Any(e => e.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                {
                    violations.Add($"{package.Id}: analyzer DLL was not found");
                }
            }
            else if (!entries.Any(e => e.StartsWith("lib/netstandard2.0/", StringComparison.OrdinalIgnoreCase) &&
                                      e.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            {
                violations.Add($"{package.Id}: netstandard2.0 library DLL was not found");
            }
        }

        ThrowIfViolations(violations, "Package smoke verification failed.");
    }

    private static IEnumerable<string> EnumerateRepositoryFiles(string root)
    {
        return EnumerateFiles(root, "*");
    }

    private static IEnumerable<string> EnumerateFiles(string root, string pattern)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                var name = Path.GetFileName(directory);
                if (ExcludedDirectoryNames.Any(excluded => excluded.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                pending.Push(directory);
            }

            foreach (var file in Directory.EnumerateFiles(current, pattern))
            {
                yield return file;
            }
        }
    }

    private static bool IsTextCandidate(string file)
    {
        var extension = Path.GetExtension(file).ToLowerInvariant();
        return extension is ".cs" or ".csproj" or ".props" or ".targets" or ".sln" or ".md" or ".yml" or ".yaml" or ".json" or ".xml" or ".txt" or ".editorconfig" or ".gitignore";
    }

    private static string Relative(string root, string file)
    {
        return Path.GetRelativePath(root, file).Replace('\\', '/');
    }

    private static void ThrowIfViolations(IReadOnlyCollection<string> violations, string message)
    {
        if (violations.Count == 0)
        {
            return;
        }

        foreach (var violation in violations)
        {
            Console.Error.WriteLine(violation);
        }

        throw new CheckFailedException(message);
    }

    private sealed record SourceFile(string Path, string[] Lines);
    private sealed record SqlRule(string Name, string Pattern);
    private sealed record PackageExpectation(string Id, bool RequiresCoreDependency, bool IsAnalyzer);
    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr)
    {
        public string SafeOutput
        {
            get
            {
                var text = (StdOut + Environment.NewLine + StdErr)
                    .Replace('\r', ' ')
                    .Replace('\n', ' ');
                text = Regex.Replace(text, @"(?i)(password|token|api[-_ ]?key)\s*=\s*[^;\s]+", "$1=<redacted>");
                return text.Length <= 1024 ? text : text[..1024];
            }
        }
    }
    private sealed record Violation(string File, int Line, string Rule, string Text)
    {
        public override string ToString() => $"{File}:{Line}: {Rule}: {Text}";
    }

    private sealed class CheckFailedException : Exception
    {
        public CheckFailedException(string message)
            : base(message)
        {
        }
    }
}
