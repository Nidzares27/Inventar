param(
    [switch]$RequireCloudinary,
    [switch]$RequireStorefrontEmailPassword,
    [switch]$RequireGoogleAuth
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$results = New-Object System.Collections.Generic.List[object]

function Add-Result {
    param(
        [string]$Scope,
        [string]$Check,
        [ValidateSet("OK", "WARN", "ERROR")]
        [string]$Status,
        [string]$Message
    )

    $results.Add([pscustomobject]@{
            Scope   = $Scope
            Check   = $Check
            Status  = $Status
            Message = $Message
        })
}

function ConvertTo-Hashtable {
    param([object]$InputObject)

    if ($null -eq $InputObject) {
        return $null
    }

    if ($InputObject -is [System.Collections.IDictionary]) {
        $table = @{}
        foreach ($key in $InputObject.Keys) {
            $table[$key] = ConvertTo-Hashtable $InputObject[$key]
        }

        return $table
    }

    if ($InputObject -is [System.Management.Automation.PSCustomObject]) {
        $table = @{}
        foreach ($property in $InputObject.PSObject.Properties) {
            $table[$property.Name] = ConvertTo-Hashtable $property.Value
        }

        return $table
    }

    if ($InputObject -is [System.Collections.IEnumerable] -and -not ($InputObject -is [string])) {
        $items = New-Object System.Collections.Generic.List[object]
        foreach ($item in $InputObject) {
            $items.Add((ConvertTo-Hashtable $item))
        }

        return ,$items.ToArray()
    }

    return $InputObject
}

function Merge-Hashtable {
    param(
        [hashtable]$Base,
        [hashtable]$Override
    )

    $merged = @{}

    foreach ($key in $Base.Keys) {
        $merged[$key] = $Base[$key]
    }

    foreach ($key in $Override.Keys) {
        if ($merged.ContainsKey($key) -and
            $merged[$key] -is [hashtable] -and
            $Override[$key] -is [hashtable]) {
            $merged[$key] = Merge-Hashtable -Base $merged[$key] -Override $Override[$key]
        }
        else {
            $merged[$key] = $Override[$key]
        }
    }

    return $merged
}

function Read-JsonAsHashtable {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing configuration file: $Path"
    }

    $raw = Get-Content -LiteralPath $Path -Raw
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return @{}
    }

    return (ConvertTo-Hashtable (ConvertFrom-Json -InputObject $raw))
}

function Get-ConfigValue {
    param(
        [hashtable]$Config,
        [string[]]$PathSegments
    )

    $current = $Config
    foreach ($segment in $PathSegments) {
        if ($current -isnot [hashtable] -or -not $current.ContainsKey($segment)) {
            return $null
        }

        $current = $current[$segment]
    }

    return $current
}

function Is-PlaceholderValue {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $true
    }

    $placeholderFragments = @(
        "SQLxxxx.site4now.net",
        "your_staging_database",
        "your_staging_user",
        "your_staging_password",
        "example.com",
        "smtp.example.com",
        "<required",
        "<optional",
        "your_"
    )

    foreach ($fragment in $placeholderFragments) {
        if ($Value.IndexOf($fragment, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }

    return $false
}

function Get-ConnectionStringSummary {
    param([string]$ConnectionString)

    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        return "not configured"
    }

    $parts = @{}
    foreach ($segment in $ConnectionString.Split(';', [System.StringSplitOptions]::RemoveEmptyEntries)) {
        $pair = $segment.Split('=', 2)
        if ($pair.Count -eq 2) {
            $parts[$pair[0].Trim()] = $pair[1].Trim()
        }
    }

    $server = $parts["Data Source"]
    if ([string]::IsNullOrWhiteSpace($server)) {
        $server = $parts["Server"]
    }

    $database = $parts["Initial Catalog"]
    if ([string]::IsNullOrWhiteSpace($database)) {
        $database = $parts["Database"]
    }

    return "server='$server', database='$database'"
}

function Write-Results {
    param([System.Collections.Generic.List[object]]$Items)

    foreach ($item in $Items) {
        $color = switch ($item.Status) {
            "OK" { "Green" }
            "WARN" { "Yellow" }
            "ERROR" { "Red" }
        }

        Write-Host ("[{0}] {1} - {2}: {3}" -f $item.Status, $item.Scope, $item.Check, $item.Message) -ForegroundColor $color
    }
}

$adminBasePath = Join-Path $repoRoot "Tepih-Inventory-main\appsettings.json"
$adminLocalPath = Join-Path $repoRoot "Tepih-Inventory-main\appsettings.Staging.local.json"
$storefrontBasePath = Join-Path $repoRoot "Inventar.Storefront\appsettings.json"
$storefrontLocalPath = Join-Path $repoRoot "Inventar.Storefront\appsettings.Staging.local.json"

try {
    $adminBaseConfig = Read-JsonAsHashtable -Path $adminBasePath
    Add-Result -Scope "Admin" -Check "Base config" -Status "OK" -Message "Loaded appsettings.json."
}
catch {
    Add-Result -Scope "Admin" -Check "Base config" -Status "ERROR" -Message $_.Exception.Message
}

try {
    $adminLocalConfig = Read-JsonAsHashtable -Path $adminLocalPath
    Add-Result -Scope "Admin" -Check "Staging local config" -Status "OK" -Message "Loaded appsettings.Staging.local.json."
}
catch {
    Add-Result -Scope "Admin" -Check "Staging local config" -Status "ERROR" -Message $_.Exception.Message
}

try {
    $storefrontBaseConfig = Read-JsonAsHashtable -Path $storefrontBasePath
    Add-Result -Scope "Storefront" -Check "Base config" -Status "OK" -Message "Loaded appsettings.json."
}
catch {
    Add-Result -Scope "Storefront" -Check "Base config" -Status "ERROR" -Message $_.Exception.Message
}

try {
    $storefrontLocalConfig = Read-JsonAsHashtable -Path $storefrontLocalPath
    Add-Result -Scope "Storefront" -Check "Staging local config" -Status "OK" -Message "Loaded appsettings.Staging.local.json."
}
catch {
    Add-Result -Scope "Storefront" -Check "Staging local config" -Status "ERROR" -Message $_.Exception.Message
}

if ($results.Status -contains "ERROR") {
    Write-Results -Items $results
    exit 1
}

$adminConfig = Merge-Hashtable -Base $adminBaseConfig -Override $adminLocalConfig
$storefrontConfig = Merge-Hashtable -Base $storefrontBaseConfig -Override $storefrontLocalConfig

$adminConnectionString = [string](Get-ConfigValue -Config $adminConfig -PathSegments @("ConnectionStrings", "Inventar"))
$storefrontConnectionString = [string](Get-ConfigValue -Config $storefrontConfig -PathSegments @("ConnectionStrings", "Inventar"))

if (Is-PlaceholderValue $adminConnectionString) {
    Add-Result -Scope "Admin" -Check "Connection string" -Status "ERROR" -Message "Inventar staging connection string is missing or still contains placeholder values."
}
else {
    Add-Result -Scope "Admin" -Check "Connection string" -Status "OK" -Message ("Configured for {0}." -f (Get-ConnectionStringSummary $adminConnectionString))
}

if (Is-PlaceholderValue $storefrontConnectionString) {
    Add-Result -Scope "Storefront" -Check "Connection string" -Status "ERROR" -Message "Inventar staging connection string is missing or still contains placeholder values."
}
else {
    Add-Result -Scope "Storefront" -Check "Connection string" -Status "OK" -Message ("Configured for {0}." -f (Get-ConnectionStringSummary $storefrontConnectionString))
}

if (-not (Is-PlaceholderValue $adminConnectionString) -and -not (Is-PlaceholderValue $storefrontConnectionString)) {
    if ($adminConnectionString -eq $storefrontConnectionString) {
        Add-Result -Scope "Shared" -Check "Database alignment" -Status "OK" -Message "Admin and storefront point to the same staging database."
    }
    else {
        Add-Result -Scope "Shared" -Check "Database alignment" -Status "WARN" -Message "Admin and storefront do not appear to use the same connection string. Double-check that both target the same staging DB."
    }
}

$cloudName = [string](Get-ConfigValue -Config $adminConfig -PathSegments @("CloudinarySettings", "CloudName"))
$cloudApiKey = [string](Get-ConfigValue -Config $adminConfig -PathSegments @("CloudinarySettings", "ApiKey"))
$cloudApiSecret = [string](Get-ConfigValue -Config $adminConfig -PathSegments @("CloudinarySettings", "ApiSecret"))
$hasCloudinary = -not (Is-PlaceholderValue $cloudName) -and -not (Is-PlaceholderValue $cloudApiKey) -and -not (Is-PlaceholderValue $cloudApiSecret)

if ($RequireCloudinary) {
    if ($hasCloudinary) {
        Add-Result -Scope "Admin" -Check "Cloudinary" -Status "OK" -Message "Cloudinary is configured for upload testing."
    }
    else {
        Add-Result -Scope "Admin" -Check "Cloudinary" -Status "ERROR" -Message "Cloudinary is required for this test run but is not fully configured."
    }
}
elseif ($hasCloudinary) {
    Add-Result -Scope "Admin" -Check "Cloudinary" -Status "OK" -Message "Cloudinary is configured, so image/video upload can be tested."
}
else {
    Add-Result -Scope "Admin" -Check "Cloudinary" -Status "WARN" -Message "Cloudinary is not fully configured. Skip image/video upload tests in this LocalStaging run."
}

$senderEmail = [string](Get-ConfigValue -Config $storefrontConfig -PathSegments @("StorefrontEmail", "SenderEmail"))
$senderDisplayName = [string](Get-ConfigValue -Config $storefrontConfig -PathSegments @("StorefrontEmail", "SenderDisplayName"))
$smtpHost = [string](Get-ConfigValue -Config $storefrontConfig -PathSegments @("StorefrontEmail", "SmtpHost"))
$smtpPort = Get-ConfigValue -Config $storefrontConfig -PathSegments @("StorefrontEmail", "SmtpPort")
$smtpUsername = [string](Get-ConfigValue -Config $storefrontConfig -PathSegments @("StorefrontEmail", "SmtpUsername"))
$smtpPassword = [string](Get-ConfigValue -Config $storefrontConfig -PathSegments @("StorefrontEmail", "SmtpPassword"))

$requiredStorefrontEmailFields = @(
    @{ Name = "SenderEmail"; Value = $senderEmail },
    @{ Name = "SenderDisplayName"; Value = $senderDisplayName },
    @{ Name = "SmtpHost"; Value = $smtpHost },
    @{ Name = "SmtpUsername"; Value = $smtpUsername }
)

$missingStorefrontEmailFields = $requiredStorefrontEmailFields | Where-Object {
    [string]::IsNullOrWhiteSpace([string]$_.Value) -or (Is-PlaceholderValue ([string]$_.Value))
}

if ($missingStorefrontEmailFields.Count -gt 0 -or [int]$smtpPort -le 0) {
    $missingNames = ($missingStorefrontEmailFields.Name + @($(if ([int]$smtpPort -le 0) { "SmtpPort" }))).Where({ $_ }) -join ", "
    Add-Result -Scope "Storefront" -Check "SMTP core settings" -Status "ERROR" -Message "Missing or placeholder values detected for: $missingNames."
}
else {
    Add-Result -Scope "Storefront" -Check "SMTP core settings" -Status "OK" -Message "Storefront can start in Staging with the configured email settings."
}

if ($RequireStorefrontEmailPassword) {
    if ([string]::IsNullOrWhiteSpace($smtpPassword) -or (Is-PlaceholderValue $smtpPassword)) {
        Add-Result -Scope "Storefront" -Check "SMTP password" -Status "ERROR" -Message "SMTP password is required for login and checkout email tests, but it is missing."
    }
    else {
        Add-Result -Scope "Storefront" -Check "SMTP password" -Status "OK" -Message "SMTP password is present, so email flows can be tested."
    }
}
elseif ([string]::IsNullOrWhiteSpace($smtpPassword) -or (Is-PlaceholderValue $smtpPassword)) {
    Add-Result -Scope "Storefront" -Check "SMTP password" -Status "WARN" -Message "SMTP password is missing. The storefront can start, but email login and checkout email flows should be skipped."
}
else {
    Add-Result -Scope "Storefront" -Check "SMTP password" -Status "OK" -Message "SMTP password is present, so email flows can be tested."
}

$googleClientId = [string](Get-ConfigValue -Config $storefrontConfig -PathSegments @("StorefrontGoogleAuth", "ClientId"))
$googleClientSecret = [string](Get-ConfigValue -Config $storefrontConfig -PathSegments @("StorefrontGoogleAuth", "ClientSecret"))
$googleCallbackPath = [string](Get-ConfigValue -Config $storefrontConfig -PathSegments @("StorefrontGoogleAuth", "CallbackPath"))
$hasGoogleAuth = -not (Is-PlaceholderValue $googleClientId) -and -not (Is-PlaceholderValue $googleClientSecret)

if ($RequireGoogleAuth) {
    if ($hasGoogleAuth -and $googleCallbackPath.StartsWith("/")) {
        Add-Result -Scope "Storefront" -Check "Google login" -Status "OK" -Message "Google login is configured for LocalStaging."
    }
    else {
        Add-Result -Scope "Storefront" -Check "Google login" -Status "ERROR" -Message "Google login was requested for this test run, but the client credentials or callback path are not ready."
    }
}
elseif ($hasGoogleAuth) {
    Add-Result -Scope "Storefront" -Check "Google login" -Status "OK" -Message "Google login is configured for LocalStaging."
}
else {
    Add-Result -Scope "Storefront" -Check "Google login" -Status "WARN" -Message "Google login is not configured. Skip that flow unless you add staging credentials."
}

Write-Results -Items $results

if ($results.Status -contains "ERROR") {
    exit 1
}

exit 0
