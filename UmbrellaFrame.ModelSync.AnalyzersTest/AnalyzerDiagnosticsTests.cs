using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using UmbrellaFrame.ModelSync.Core.Analyzers;
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

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        string source,
        DiagnosticAnalyzer analyzer)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(MySqlTableNameAttribute).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "AnalyzerTestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create(analyzer));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }
}
