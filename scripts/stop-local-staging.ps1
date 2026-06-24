param()

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$sessionPath = Join-Path $repoRoot "artifacts\local-staging\session.json"

if (-not (Test-Path -LiteralPath $sessionPath)) {
    Write-Host "No LocalStaging session file was found. Nothing to stop." -ForegroundColor Yellow
    exit 0
}

$session = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json

foreach ($name in @("Admin", "Storefront")) {
    $entry = $session.$name
    if ($null -eq $entry -or -not $entry.ProcessId) {
        continue
    }

    $process = Get-Process -Id ([int]$entry.ProcessId) -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        Write-Host "$name process is already stopped." -ForegroundColor Yellow
        continue
    }

    Stop-Process -Id $process.Id -Force
    Write-Host "$name process stopped (PID $($process.Id))." -ForegroundColor Green
}

Remove-Item -LiteralPath $sessionPath -Force
Write-Host "LocalStaging session file removed." -ForegroundColor Green
