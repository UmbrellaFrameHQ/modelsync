using System.IO.Compression;
using System.Text.RegularExpressions;

namespace UmbrellaFrame.ModelSync.RepositoryChecks;

internal static class Program
{
    private static readonly string ShellWord = string.Concat("power", "shell");
    private static readonly string ShortShellWord = string.Concat("p", "wsh");
    private static readonly string VerifyNoShellCommand = "verify-no-" + ShellWord;
    private const string CurrentReleaseVersion = "1.2.0";
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
            !nugetReadme.Contains($"1.2.0 Operational Hardening", StringComparison.Ordinal))
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
        var clean = "<Project><PropertyGroup><Version>1.2.0</Version></PropertyGroup></Project>";
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
