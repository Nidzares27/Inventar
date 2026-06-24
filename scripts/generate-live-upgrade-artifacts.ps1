param(
    [string]$OutputDir = ".\artifacts\production-upgrade"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$adminProjectDir = Join-Path $repoRoot "Tepih-Inventory-main"
$adminProject = Join-Path $adminProjectDir "Inventar.csproj"
$storefrontAccountSchemaPath = Join-Path $repoRoot "Inventar.Storefront\docs\storefront-account-schema.sql"
$resolvedOutputDir = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDir))

New-Item -ItemType Directory -Force -Path $resolvedOutputDir | Out-Null

Push-Location $adminProjectDir
try
{
    dotnet tool restore

    $migrationSqlPath = Join-Path $resolvedOutputDir "inventar-live-upgrade.sql"

    dotnet ef migrations script `
        --idempotent `
        --configuration Release `
        --project $adminProject `
        --startup-project $adminProject `
        --output $migrationSqlPath

    $storefrontAccountOutputPath = Join-Path $resolvedOutputDir "storefront-account-schema.sql"
    Copy-Item `
        -LiteralPath $storefrontAccountSchemaPath `
        -Destination $storefrontAccountOutputPath `
        -Force

    $completeUpgradeSqlPath = Join-Path $resolvedOutputDir "inventar-live-upgrade-complete.sql"
    $adminUpgradeSql = Get-Content -LiteralPath $migrationSqlPath -Raw
    $storefrontAccountSql = Get-Content -LiteralPath $storefrontAccountOutputPath -Raw
    [System.IO.File]::WriteAllText(
        $completeUpgradeSqlPath,
        $adminUpgradeSql + [Environment]::NewLine + [Environment]::NewLine + $storefrontAccountSql,
        [System.Text.Encoding]::UTF8)

    Copy-Item `
        -LiteralPath (Join-Path $repoRoot "docs\sql\live-db-preflight.sql") `
        -Destination (Join-Path $resolvedOutputDir "live-db-preflight.sql") `
        -Force

    Copy-Item `
        -LiteralPath (Join-Path $repoRoot "docs\sql\post-cutover-smoke-test.sql") `
        -Destination (Join-Path $resolvedOutputDir "post-cutover-smoke-test.sql") `
        -Force

    Write-Host "Upgrade artifacts generated in: $resolvedOutputDir"
}
finally
{
    Pop-Location
}
