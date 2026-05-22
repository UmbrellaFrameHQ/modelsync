param(
    [string]$Configuration = "Release",
    [switch]$Integration
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$filter = if ($Integration) { "Category=Integration" } else { "Category!=Integration" }

$projects = @(
    "UmbrellaFrame.ModelSync.CoreTest\UmbrellaFrame.ModelSync.CoreTest.csproj",
    "UmbrellaFrame.ModelSync.MySqlTest\UmbrellaFrame.ModelSync.MySqlTest.csproj",
    "UmbrellaFrame.ModelSync.SqlServerTest\UmbrellaFrame.ModelSync.SqlServerTest.csproj",
    "UmbrellaFrame.ModelSync.PostgreSQLTest\UmbrellaFrame.ModelSync.PostgreSQLTest.csproj",
    "UmbrellaFrame.ModelSync.SQLiteTest\UmbrellaFrame.ModelSync.SQLiteTest.csproj",
    "UmbrellaFrame.ModelSync.NotesExtensionTest\UmbrellaFrame.ModelSync.NotesExtensionTest.csproj"
)

foreach ($project in $projects) {
    dotnet test (Join-Path $root $project) --configuration $Configuration --filter $filter
}
