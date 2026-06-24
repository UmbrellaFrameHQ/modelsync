$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

docker compose -f (Join-Path $root "docker-compose.modelsync-test.yml") up -d

Write-Host ""
Write-Host "ModelSync test databases are starting."
Write-Host ""
Write-Host "SQL Server:  Server=localhost,14333;Database=modelsync_sp;User Id=sa;Password=ModelSync_Pass123;Encrypt=False;TrustServerCertificate=True;"
Write-Host "MySQL:       Server=localhost;Port=3307;Database=modelsync_sp;User ID=root;Password=ModelSync_Pass123;"
Write-Host "PostgreSQL: Host=localhost;Port=5433;Database=modelsync_sp;Username=postgres;Password=ModelSync_Pass123;"
