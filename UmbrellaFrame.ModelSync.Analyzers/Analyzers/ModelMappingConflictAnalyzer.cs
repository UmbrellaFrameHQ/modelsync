using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using UmbrellaFrame.ModelSync.Core.Analyzers.Resources;

namespace UmbrellaFrame.ModelSync.Core.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ModelMappingConflictAnalyzer : DiagnosticAnalyzer
    {
        private static readonly Regex Identifier = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        private static readonly DiagnosticDescriptor IgnoreConflict = Create("MSYNC004");

        private static readonly DiagnosticDescriptor InvalidColumnName = Create("MSYNC005");

        private static readonly DiagnosticDescriptor ConflictingGeneratedValue = Create("MSYNC006");

        private static readonly DiagnosticDescriptor UnsupportedGeneratedValueType = Create("MSYNC007");

        private static readonly DiagnosticDescriptor MultiplePrimaryKeys = Create("MSYNC008");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                IgnoreConflict,
                InvalidColumnName,
                ConflictingGeneratedValue,
                UnsupportedGeneratedValueType,
                MultiplePrimaryKeys);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
        }

        private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
        {
            var declaration = (PropertyDeclarationSyntax)context.Node;
            var property = context.SemanticModel.GetDeclaredSymbol(declaration);
            if (property == null)
                return;

            var attributes = property.GetAttributes();
            var names = attributes
                .Select(attribute => attribute.AttributeClass?.Name ?? string.Empty)
                .ToArray();

            var ignored = names.Any(name => name == "DbIgnoreAttribute");
            var mapped = names.Any(name =>
                name != "DbIgnoreAttribute" &&
                (name.Contains("Column") || name.Contains("ForeignKey")));

            if (ignored && mapped)
                Report(context, IgnoreConflict, declaration, property.Name);

            var columnName = attributes.FirstOrDefault(attribute => attribute.AttributeClass?.Name == "DbColumnNameAttribute");
            if (columnName != null && columnName.ConstructorArguments.Length > 0)
            {
                var value = columnName.ConstructorArguments[0].Value as string;
                if (string.IsNullOrWhiteSpace(value) || !Identifier.IsMatch(value))
                    Report(context, InvalidColumnName, declaration, property.Name, value ?? string.Empty);
            }

            var defaultCount = names.Count(name =>
                name.EndsWith("ColumnDefaultAttribute") ||
                name.EndsWith("ColumnDefaultSqlAttribute"));
            if (defaultCount > 1)
                Report(context, ConflictingGeneratedValue, declaration, property.Name);

            var primaryKeys = attributes
                .Where(attribute => (attribute.AttributeClass?.Name ?? string.Empty).EndsWith("ColumnPrimaryKeyAttribute"))
                .ToArray();

            if (primaryKeys.Length > 1)
                Report(context, MultiplePrimaryKeys, declaration, property.Name);

            if (primaryKeys.Any(IsAutoIncrement) && !IsIntegral(property.Type))
                Report(context, UnsupportedGeneratedValueType, declaration, property.Name, property.Type.ToDisplayString());
        }

        private static bool IsAutoIncrement(AttributeData attribute)
            => attribute.ConstructorArguments.Length > 0 &&
               attribute.ConstructorArguments[0].Value is bool enabled &&
               enabled;

        private static bool IsIntegral(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol named &&
                named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
                named.TypeArguments.Length == 1)
            {
                type = named.TypeArguments[0];
            }

            switch (type.SpecialType)
            {
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                    return true;
                default:
                    return false;
            }
        }

        private static void Report(
            SyntaxNodeAnalysisContext context,
            DiagnosticDescriptor descriptor,
            PropertyDeclarationSyntax declaration,
            params object[] arguments)
            => context.ReportDiagnostic(Diagnostic.Create(descriptor, declaration.Identifier.GetLocation(), arguments));

        private static DiagnosticDescriptor Create(string id)
            => new DiagnosticDescriptor(
                id,
                AnalyzerResources.Get(id + "_Title"),
                AnalyzerResources.Get(id + "_MessageFormat"),
                "ModelSync",
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: AnalyzerResources.Get(id + "_Description"));
    }
}
