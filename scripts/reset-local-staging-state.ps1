param()

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupRoot = Join-Path $repoRoot "artifacts\local-staging\key-backups\$timestamp"
$paths = @(
    (Join-Path $repoRoot "Tepih-Inventory-main\App_Data\DataProtection-Keys"),
    (Join-Path $repoRoot "Inventar.Storefront\App_Data\DataProtection-Keys")
)

New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null

foreach ($path in $paths) {
    if (-not (Test-Path -LiteralPath $path)) {
        Write-Host "Skipping missing path: $path" -ForegroundColor Yellow
        continue
    }

    $leaf = Split-Path -Path $path -Leaf
    $parentLeaf = Split-Path -Path (Split-Path -Path $path -Parent) -Leaf
    $backupPath = Join-Path $backupRoot "$parentLeaf-$leaf"
    Copy-Item -LiteralPath $path -Destination $backupPath -Recurse -Force
    Remove-Item -LiteralPath (Join-Path $path "*") -Recurse -Force
    Write-Host "Cleared DataProtection keys under: $path" -ForegroundColor Green
}

Write-Host ""
Write-Host "Next recommended steps:" -ForegroundColor Cyan
Write-Host "1. Clear localhost cookies for both admin and storefront in your browser."
Write-Host "2. Start LocalStaging again."
Write-Host "3. Sign in fresh and retest."
Write-Host ""
Write-Host "Backup of the old key files was saved to: $backupRoot"
