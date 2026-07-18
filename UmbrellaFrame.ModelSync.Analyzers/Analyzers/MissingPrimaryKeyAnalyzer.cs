using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using UmbrellaFrame.ModelSync.Core.Analyzers.Resources;

namespace UmbrellaFrame.ModelSync.Core.Analyzers
{
    /// <summary>
    /// MSYNC003 — Reports a warning when a model class decorated with a table name attribute
    /// has no property marked with a primary key attribute.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MissingPrimaryKeyAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MSYNC003";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticId,
            title: AnalyzerResources.Get("MSYNC003_Title"),
            messageFormat: AnalyzerResources.Get("MSYNC003_MessageFormat"),
            category: "ModelSync",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: AnalyzerResources.Get("MSYNC003_Description"));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
        }

        private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;
            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl);
            if (classSymbol == null) return;

            bool hasTableNameAttr = classSymbol.GetAttributes().Any(a =>
                a.AttributeClass != null &&
                AnalyzerAttributeNames.IsTableNameAttribute(a.AttributeClass.Name));

            if (!hasTableNameAttr) return;

            bool hasPrimaryKey = classSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Any(p => p.GetAttributes().Any(a =>
                    a.AttributeClass?.Name.EndsWith("PrimaryKeyAttribute") == true ||
                    a.AttributeClass?.Name.EndsWith("ColumnPrimaryKeyAttribute") == true));

            if (hasPrimaryKey) return;

            var location = classDecl.Identifier.GetLocation();
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, classSymbol.Name));
        }
    }
}
