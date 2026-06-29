using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace UmbrellaFrame.ModelSync.CoreTest;

public class ArchitectureDependencyTests
{
    [Test]
    public void CoreProject_ShouldNotReferenceProviderProjectsOrProviderClientPackages()
    {
        var root = FindRepositoryRoot();
        var coreProject = Path.Combine(root, "UmbrellaFrame.ModelSync.Core", "UmbrellaFrame.ModelSync.Core.csproj");
        var document = XDocument.Load(coreProject);
        var references = document.Descendants()
            .Where(e => e.Name.LocalName == "PackageReference" || e.Name.LocalName == "ProjectReference")
            .Select(e => (e.Attribute("Include")?.Value ?? string.Empty) + " " + (e.Attribute("Update")?.Value ?? string.Empty))
            .ToList();

        var forbidden = new[]
        {
            "UmbrellaFrame.ModelSync.SqlServer",
            "UmbrellaFrame.ModelSync.MySql",
            "UmbrellaFrame.ModelSync.PostgreSQL",
            "UmbrellaFrame.ModelSync.SQLite",
            "Microsoft.Data.SqlClient",
            "MySqlConnector",
            "Npgsql",
            "Microsoft.Data.Sqlite"
        };

        foreach (var forbiddenReference in forbidden)
        {
            Assert.That(references.Any(r => r.Contains(forbiddenReference)), Is.False, forbiddenReference);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = TestContext.CurrentContext.TestDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "ModelSync.sln")))
                return directory;
            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate ModelSync repository root.");
    }
}
