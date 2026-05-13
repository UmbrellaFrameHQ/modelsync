using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using UmbrellaFrame.ModelSync.Core.Analyzers.Resources;

namespace UmbrellaFrame.ModelSync.Core.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MissingDbColumnTypeAttributeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MSYNC001";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticId,
            title: AnalyzerResources.Get("MSYNC001_Title"),
            messageFormat: AnalyzerResources.Get("MSYNC001_MessageFormat"),
            category: "ModelSync",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: AnalyzerResources.Get("MSYNC001_Description"));

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

            // Sınıfta herhangi bir *TableName attribute'u var mı kontrol et
            bool hasTableNameAttr = classSymbol.GetAttributes().Any(a =>
                a.AttributeClass?.Name.EndsWith("TableNameAttribute") == true);

            if (!hasTableNameAttr) return;

            // Public instance property'leri tara
            foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
            {
                if (member.DeclaredAccessibility != Accessibility.Public) continue;
                if (member.IsStatic) continue;

                bool hasColumnTypeAttr = member.GetAttributes().Any(a =>
                    a.AttributeClass?.Name.EndsWith("ColumnTypeAttribute") == true);

                if (!hasColumnTypeAttr)
                {
                    var location = member.Locations.FirstOrDefault();
                    if (location != null)
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(Rule, location, member.Name));
                    }
                }
            }
        }
    }
}
