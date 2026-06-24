param(
    [string]$AdminUrl = "http://localhost:5075",
    [string]$StorefrontUrl = "http://localhost:5241"
)

$ErrorActionPreference = "Stop"

function Test-HealthEndpoint {
    param(
        [string]$Name,
        [string]$BaseUrl
    )

    $checks = @(
        "$BaseUrl/health/live",
        "$BaseUrl/health/ready"
    )

    foreach ($url in $checks) {
        try {
            $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 10
            Write-Host "[OK] $Name health endpoint responded: $url" -ForegroundColor Green
        }
        catch {
            Write-Host "[WARN] $Name health endpoint did not respond: $url" -ForegroundColor Yellow
            Write-Host "       $($_.Exception.Message)"
        }
    }
}

Test-HealthEndpoint -Name "Admin" -BaseUrl $AdminUrl
Test-HealthEndpoint -Name "Storefront" -BaseUrl $StorefrontUrl
