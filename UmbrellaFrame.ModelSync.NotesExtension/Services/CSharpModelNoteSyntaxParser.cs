using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UmbrellaFrame.ModelSync.NotesExtension.Models;

namespace UmbrellaFrame.ModelSync.NotesExtension.Services
{
    public static class CSharpModelNoteSyntaxParser
    {
        public static IReadOnlyDictionary<int, ParsedModelNoteContext> Parse(
            string sourceText,
            string fileKey,
            Func<string, bool> isModelClass)
        {
            if (sourceText == null)
            {
                throw new ArgumentNullException(nameof(sourceText));
            }

            if (isModelClass == null)
            {
                throw new ArgumentNullException(nameof(isModelClass));
            }

            var contexts = new Dictionary<int, ParsedModelNoteContext>();
            var tree = CSharpSyntaxTree.ParseText(sourceText);
            var root = tree.GetRoot();
            var lineSpanText = tree.GetText();

            foreach (var classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var className = classDeclaration.Identifier.ValueText;
                if (!isModelClass(className))
                {
                    continue;
                }

                var qualifiedClassName = GetQualifiedClassName(classDeclaration);
                var classLine = lineSpanText.Lines.GetLineFromPosition(classDeclaration.Identifier.SpanStart).LineNumber;
                contexts[classLine] = CreateContext(fileKey, qualifiedClassName, className, string.Empty, classLine);

                foreach (var propertyDeclaration in classDeclaration.Members.OfType<PropertyDeclarationSyntax>())
                {
                    if (!HasGetter(propertyDeclaration))
                    {
                        continue;
                    }

                    var propertyName = propertyDeclaration.Identifier.ValueText;
                    var propertyLine = lineSpanText.Lines.GetLineFromPosition(propertyDeclaration.Identifier.SpanStart).LineNumber;
                    contexts[propertyLine] = CreateContext(fileKey, qualifiedClassName, className, propertyName, propertyLine);
                }
            }

            return contexts;
        }

        private static ParsedModelNoteContext CreateContext(
            string fileKey,
            string qualifiedClassName,
            string className,
            string propertyName,
            int lineNumber)
        {
            var memberKey = string.IsNullOrWhiteSpace(propertyName)
                ? qualifiedClassName
                : qualifiedClassName + "." + propertyName;
            var noteKey = string.IsNullOrWhiteSpace(fileKey)
                ? memberKey
                : "file:" + fileKey + "::" + memberKey;
            var displayName = string.IsNullOrWhiteSpace(propertyName)
                ? className
                : className + "." + propertyName;
            var legacyNoteKey = string.IsNullOrWhiteSpace(propertyName)
                ? className
                : className + "." + propertyName;

            return new ParsedModelNoteContext(lineNumber, className, propertyName, noteKey, displayName, legacyNoteKey);
        }

        private static string GetQualifiedClassName(ClassDeclarationSyntax classDeclaration)
        {
            var names = new Stack<string>();
            names.Push(classDeclaration.Identifier.ValueText);

            for (SyntaxNode? node = classDeclaration.Parent; node != null; node = node.Parent)
            {
                switch (node)
                {
                    case ClassDeclarationSyntax parentClass:
                        names.Push(parentClass.Identifier.ValueText);
                        break;
                    case BaseNamespaceDeclarationSyntax namespaceDeclaration:
                        names.Push(namespaceDeclaration.Name.ToString());
                        break;
                }
            }

            return string.Join(".", names);
        }

        private static bool HasGetter(PropertyDeclarationSyntax propertyDeclaration)
        {
            if (propertyDeclaration.ExpressionBody != null)
            {
                return true;
            }

            return propertyDeclaration.AccessorList?.Accessors.Any(accessor =>
                accessor.IsKind(SyntaxKind.GetAccessorDeclaration)) == true;
        }
    }
}
