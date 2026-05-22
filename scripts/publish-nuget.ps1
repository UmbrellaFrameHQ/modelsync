param(
    [string]$Configuration = "Release",
    [string]$Output = "artifacts",
    [string]$Source = "https://api.nuget.org/v3/index.json"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $root $Output
$apiKey = $env:NUGET_API_KEY

if ([string]::IsNullOrWhiteSpace($apiKey)) {
    throw "Set NUGET_API_KEY before publishing."
}

& (Join-Path $PSScriptRoot "pack.ps1") -Configuration $Configuration -Output $Output

Get-ChildItem -Path $outputPath -Filter "*.nupkg" | ForEach-Object {
    dotnet nuget push $_.FullName --api-key $apiKey --source $Source --skip-duplicate
}
