using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using UmbrellaFrame.ModelSync.Core.Analyzers;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.MySql;

namespace UmbrellaFrame.ModelSync.AnalyzersTest;

[TestFixture]
public class AnalyzerDiagnosticsTests
{
    [Test]
    public async Task MSYNC001_TableModelWithPublicPropertyMissingColumnType_ReportsDiagnostic()
    {
        const string source = @"
using UmbrellaFrame.ModelSync.MySql;

[MySqlTableName(""products"")]
public class Product
{
    [MySqlColumnType(MySqlColumnType.INT)]
    [MySqlColumnPrimaryKey]
    public int Id { get; set; }

    public string Name { get; set; }
}";

        var diagnostics = await RunAnalyzerAsync(source, new MissingDbColumnTypeAttributeAnalyzer());

        Assert.That(diagnostics.Select(d => d.Id), Does.Contain("MSYNC001"));
    }

    [Test]
    public async Task MSYNC001_IgnoredProperty_DoesNotReportDiagnostic()
    {
        const string source = @"
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.MySql;

[MySqlTableName(""products"")]
public class Product
{
    [MySqlColumnType(MySqlColumnType.INT)]
    public int Id { get; set; }

    [DbIgnore]
    public string DisplayName { get; set; }
}";

        var diagnostics = await RunAnalyzerAsync(source, new MissingDbColumnTypeAttributeAnalyzer());

        Assert.That(diagnostics.Select(d => d.Id), Does.Not.Contain("MSYNC001"));
    }

    [Test]
    public async Task MSYNC002_ModelWithColumnTypeButNoTableName_ReportsDiagnostic()
    {
        const string source = @"
using System;

public sealed class MySqlColumnTypeAttribute : Attribute
{
    public MySqlColumnTypeAttribute(string columnType) { }
}

public class Product
{
    [MySqlColumnType(""INT"")]
    public int Id { get; set; }
}";

        var diagnostics = await RunAnalyzerAsync(source, new MissingTableNameAttributeAnalyzer());

        Assert.That(diagnostics.Select(d => d.Id), Does.Contain("MSYNC002"));
    }

    [Test]
    public async Task MSYNC003_TableModelWithoutPrimaryKey_ReportsDiagnostic()
    {
        const string source = @"
using UmbrellaFrame.ModelSync.MySql;

[MySqlTableName(""products"")]
public class Product
{
    [MySqlColumnType(MySqlColumnType.INT)]
    public int Id { get; set; }
}";

        var diagnostics = await RunAnalyzerAsync(source, new MissingPrimaryKeyAnalyzer());

        Assert.That(diagnostics.Select(d => d.Id), Does.Contain("MSYNC003"));
    }

    [TestCase("MSYNC004", "[DbIgnore, MySqlColumnType(MySqlColumnType.INT)] public int Value { get; set; }")]
    [TestCase("MSYNC005", "[DbColumnName(\"bad-name\"), MySqlColumnType(MySqlColumnType.INT)] public int Value { get; set; }")]
    [TestCase("MSYNC006", "[DbColumnDefault(\"0\"), MySqlColumnDefault(MySqlDefaultExpression.CurrentTimestamp), MySqlColumnType(MySqlColumnType.INT)] public int Value { get; set; }")]
    [TestCase("MSYNC007", "[MySqlColumnPrimaryKey(true), MySqlColumnType(MySqlColumnType.VARCHAR, \"50\")] public string Value { get; set; } = string.Empty;")]
    [TestCase("MSYNC008", "[MySqlColumnPrimaryKey, FakeColumnPrimaryKey, MySqlColumnType(MySqlColumnType.INT)] public int Value { get; set; }")]
    public async Task MappingConflictRules_ReportExpectedDiagnostic(string diagnosticId, string propertySource)
    {
        var source = @"
using System;
using UmbrellaFrame.ModelSync.Core;
using UmbrellaFrame.ModelSync.MySql;

public sealed class FakeColumnPrimaryKeyAttribute : Attribute { }

[MySqlTableName(""products"")]
public class Product
{
    " + propertySource + @"
}";

        var diagnostics = await RunAnalyzerAsync(source, new ModelMappingConflictAnalyzer());
        Assert.That(diagnostics.Select(d => d.Id), Does.Contain(diagnosticId));
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        string source,
        DiagnosticAnalyzer analyzer)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var runtimeReferences = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path));
        var references = runtimeReferences
            .Concat(new[]
            {
                MetadataReference.CreateFromFile(typeof(DbIgnoreAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(MySqlTableNameAttribute).Assembly.Location),
            })
            .GroupBy(reference => reference.Display, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());

        var compilation = CSharpCompilation.Create(
            "AnalyzerTestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create(analyzer));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }
}
