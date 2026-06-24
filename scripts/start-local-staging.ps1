param(
    [switch]$SkipValidation,
    [switch]$RequireCloudinary,
    [switch]$RequireStorefrontEmailPassword,
    [switch]$RequireGoogleAuth,
    [switch]$OpenBrowser
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsDir = Join-Path $repoRoot "artifacts\local-staging"
$sessionPath = Join-Path $artifactsDir "session.json"
$validationScript = Join-Path $PSScriptRoot "test-local-staging-config.ps1"
$healthScript = Join-Path $PSScriptRoot "test-local-staging-health.ps1"
$adminProject = "Tepih-Inventory-main\Inventar.csproj"
$storefrontProject = "Inventar.Storefront\Inventar.Storefront.csproj"

function Normalize-ProcessPathEnvironment {
    $processEnvironment = [System.Environment]::GetEnvironmentVariables("Process")
    $pathValue = $null

    foreach ($key in $processEnvironment.Keys) {
        if ([string]::Equals([string]$key, "PATH", [System.StringComparison]::OrdinalIgnoreCase)) {
            $pathValue = [string]$processEnvironment[$key]
            break
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($pathValue)) {
        [System.Environment]::SetEnvironmentVariable("PATH", $null, "Process")
        [System.Environment]::SetEnvironmentVariable("Path", $null, "Process")
        [System.Environment]::SetEnvironmentVariable("Path", $pathValue, "Process")
    }
}

function Stop-TrackedProcess {
    param([int]$ProcessId)

    try {
        $process = Get-Process -Id $ProcessId -ErrorAction Stop
        Stop-Process -Id $process.Id -Force
    }
    catch {
    }
}

function Test-ExistingSession {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    $session = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    foreach ($name in @("Admin", "Storefront")) {
        if ($null -ne $session.$name -and $session.$name.ProcessId) {
            $process = Get-Process -Id ([int]$session.$name.ProcessId) -ErrorAction SilentlyContinue
            if ($null -ne $process) {
                return $true
            }
        }
    }

    return $false
}

function Wait-ForHealthEndpoint {
    param(
        [string]$Name,
        [string]$Url,
        [int]$TimeoutSeconds = 90
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 10
            Write-Host "[OK] $Name became reachable: $Url" -ForegroundColor Green
            return $true
        }
        catch {
            Start-Sleep -Seconds 3
        }
    }

    Write-Host "[WARN] $Name did not become reachable within $TimeoutSeconds seconds: $Url" -ForegroundColor Yellow
    return $false
}

if (-not $SkipValidation) {
    $validationArguments = @{}
    if ($RequireCloudinary) {
        $validationArguments.RequireCloudinary = $true
    }

    if ($RequireStorefrontEmailPassword) {
        $validationArguments.RequireStorefrontEmailPassword = $true
    }

    if ($RequireGoogleAuth) {
        $validationArguments.RequireGoogleAuth = $true
    }

    & $validationScript @validationArguments
    if ($LASTEXITCODE -ne 0) {
        throw "LocalStaging configuration validation failed. Fix the reported issues before starting the apps."
    }
}

New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null
Normalize-ProcessPathEnvironment

if (Test-ExistingSession -Path $sessionPath) {
    throw "A LocalStaging session appears to be already running. Use scripts\stop-local-staging.ps1 first, or remove artifacts\local-staging\session.json after confirming the old processes are gone."
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$adminStdOutPath = Join-Path $artifactsDir "admin-$timestamp.stdout.log"
$adminStdErrPath = Join-Path $artifactsDir "admin-$timestamp.stderr.log"
$storefrontStdOutPath = Join-Path $artifactsDir "storefront-$timestamp.stdout.log"
$storefrontStdErrPath = Join-Path $artifactsDir "storefront-$timestamp.stderr.log"

$adminProcess = $null
$storefrontProcess = $null

try {
    $adminProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--no-restore", "--launch-profile", "LocalStaging", "--project", $adminProject) `
        -WorkingDirectory $repoRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $adminStdOutPath `
        -RedirectStandardError $adminStdErrPath `
        -PassThru

    $storefrontProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--no-restore", "--launch-profile", "LocalStaging", "--project", $storefrontProject) `
        -WorkingDirectory $repoRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $storefrontStdOutPath `
        -RedirectStandardError $storefrontStdErrPath `
        -PassThru

    $session = [pscustomobject]@{
        StartedUtc = (Get-Date).ToUniversalTime().ToString("o")
        Admin      = [pscustomobject]@{
            ProcessId      = $adminProcess.Id
            HttpUrl        = "http://localhost:5075"
            HttpsUrl       = "https://localhost:7189"
            StdOutLogPath  = $adminStdOutPath
            StdErrLogPath  = $adminStdErrPath
        }
        Storefront = [pscustomobject]@{
            ProcessId      = $storefrontProcess.Id
            HttpUrl        = "http://localhost:5241"
            HttpsUrl       = "https://localhost:7241"
            StdOutLogPath  = $storefrontStdOutPath
            StdErrLogPath  = $storefrontStdErrPath
        }
    }

    $session | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $sessionPath -Encoding UTF8

    Write-Host "LocalStaging processes started." -ForegroundColor Green
    Write-Host "Admin:      http://localhost:5075  |  https://localhost:7189"
    Write-Host "Storefront: http://localhost:5241  |  https://localhost:7241"
    Write-Host "Session file: $sessionPath"
    Write-Host "Admin logs: $adminStdOutPath / $adminStdErrPath"
    Write-Host "Storefront logs: $storefrontStdOutPath / $storefrontStdErrPath"

    Wait-ForHealthEndpoint -Name "Admin" -Url "http://localhost:5075/health/live" | Out-Null
    Wait-ForHealthEndpoint -Name "Storefront" -Url "http://localhost:5241/health/live" | Out-Null
    & $healthScript

    if ($OpenBrowser) {
        Start-Process "https://localhost:7189"
        Start-Process "https://localhost:7241"
    }
}
catch {
    if ($adminProcess) {
        Stop-TrackedProcess -ProcessId $adminProcess.Id
    }

    if ($storefrontProcess) {
        Stop-TrackedProcess -ProcessId $storefrontProcess.Id
    }

    if (Test-Path -LiteralPath $sessionPath) {
        Remove-Item -LiteralPath $sessionPath -Force
    }

    throw
}
