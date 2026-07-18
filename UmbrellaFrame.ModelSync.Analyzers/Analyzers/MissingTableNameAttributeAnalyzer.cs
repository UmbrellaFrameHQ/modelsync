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
    /// MSYNC002 — Reports a warning when a class has at least one column-type attribute
    /// on its properties but is missing a table-name attribute.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MissingTableNameAttributeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MSYNC002";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticId,
            title: AnalyzerResources.Get("MSYNC002_Title"),
            messageFormat: AnalyzerResources.Get("MSYNC002_MessageFormat"),
            category: "ModelSync",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: AnalyzerResources.Get("MSYNC002_Description"));

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

            if (hasTableNameAttr) return;

            bool anyColumnTypeAttr = classSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic)
                .Any(p => p.GetAttributes().Any(a =>
                    a.AttributeClass?.Name.EndsWith("ColumnTypeAttribute") == true));

            if (!anyColumnTypeAttr) return;

            var location = classDecl.Identifier.GetLocation();
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, classSymbol.Name));
        }
    }
}
