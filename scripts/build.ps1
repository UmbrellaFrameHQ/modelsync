param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

dotnet restore "$root\ModelSync.sln"
dotnet build "$root\ModelSync.sln" --configuration $Configuration --no-restore
