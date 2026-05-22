using System.Linq;
using NUnit.Framework;
using UmbrellaFrame.ModelSync.NotesExtension.Services;

namespace UmbrellaFrame.ModelSync.NotesExtensionTest
{
    [TestFixture]
    public class CSharpModelNoteSyntaxParserTests
    {
        [Test]
        public void Parse_ShouldFindModelClassSingleLineMultiLineAndExpressionBodiedProperties()
        {
            const string source = @"namespace Demo.Models
{
    public class ProductModel
    {
        public int Id { get; set; }

        public string Name
        {
            get;
            set;
        }

        public string DisplayName => Name;
    }

    public class ProductService
    {
        public string Name { get; set; }
    }
}";

            var contexts = CSharpModelNoteSyntaxParser.Parse(
                source,
                "Models/ProductModel.cs",
                className => className.EndsWith("Model", System.StringComparison.Ordinal));

            var displayNames = contexts.Values.Select(context => context.DisplayName).ToArray();

            Assert.That(displayNames, Does.Contain("ProductModel"));
            Assert.That(displayNames, Does.Contain("ProductModel.Id"));
            Assert.That(displayNames, Does.Contain("ProductModel.Name"));
            Assert.That(displayNames, Does.Contain("ProductModel.DisplayName"));
            Assert.That(displayNames, Does.Not.Contain("ProductService.Name"));
            Assert.That(
                contexts.Values.Single(context => context.DisplayName == "ProductModel.Name").NoteKey,
                Is.EqualTo("file:Models/ProductModel.cs::Demo.Models.ProductModel.Name"));
        }
    }
}
