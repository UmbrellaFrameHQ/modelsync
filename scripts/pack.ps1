param(
    [string]$Configuration = "Release",
    [string]$Output = "artifacts"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $root $Output

New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
Get-ChildItem -Path $outputPath -Filter "UmbrellaFrame.ModelSync.*.nupkg" -ErrorAction SilentlyContinue | Remove-Item -Force

$packages = @(
    "UmbrellaFrame.ModelSync.Core\UmbrellaFrame.ModelSync.Core.csproj",
    "UmbrellaFrame.ModelSync.MySql\UmbrellaFrame.ModelSync.MySql.csproj",
    "UmbrellaFrame.ModelSync.SqlServer\UmbrellaFrame.ModelSync.SqlServer.csproj",
    "UmbrellaFrame.ModelSync.PostgreSQL\UmbrellaFrame.ModelSync.PostgreSQL.csproj",
    "UmbrellaFrame.ModelSync.SQLite\UmbrellaFrame.ModelSync.SQLite.csproj",
    "UmbrellaFrame.ModelSync.Analyzers\UmbrellaFrame.ModelSync.Core.Analyzers.csproj"
)

foreach ($project in $packages) {
    dotnet pack (Join-Path $root $project) --configuration $Configuration --output $outputPath
}
