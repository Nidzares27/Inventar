# Production Deployment Guide

This document covers the production deployment path for:

- `Inventar` admin app: `Tepih-Inventory-main/Inventar.csproj`
- `Inventar.Storefront` shop app: `Inventar.Storefront/Inventar.Storefront.csproj`

## What is now prepared

- SQL Server connections use retry-on-failure for transient database issues.
- Both apps expose health endpoints:
  - `/health/live`
  - `/health/ready`
- Both apps support reverse-proxy forwarding through the `ReverseProxy` config section.
- Both apps enable HTTPS response compression.
- Both apps add production cache headers for static files.
- Both apps persist Data Protection keys to a configurable directory.
- `Inventar.Storefront` now writes application logs to `Logs/log-*.txt`.
- `Inventar` SendGrid sender settings are now configuration-driven instead of hardcoded.
- Production config templates were added with safe placeholders:
  - `Tepih-Inventory-main/appsettings.Production.json`
  - `Inventar.Storefront/appsettings.Production.json`

## Required production configuration

Do not store real production secrets in committed `appsettings*.json` files.

Recommended approach:

1. Keep committed `appsettings.Production.json` as a template.
2. Override secrets using environment variables or deployment-time transforms.
3. Keep machine-specific overrides in an untracked file such as:
   - `appsettings.Production.local.json`

### Inventar admin

Required keys:

- `ConnectionStrings__Inventar`
- `CloudinarySettings__CloudName`
- `CloudinarySettings__ApiKey`
- `CloudinarySettings__ApiSecret`

Optional but recommended:

- `SendGrid__ApiKey`
- `SendGrid__SenderEmail`
- `SendGrid__SenderDisplayName`
- `HostRedirect__Enabled`
- `HostRedirect__DestinationHost`
- `HostRedirect__SourceHosts__0`
- `HostRedirect__SourceHosts__1`
- `DataProtection__KeysPath`
- `AllowedHosts`
- `ReverseProxy__Enabled`

### Inventar.Storefront

Required keys:

- `ConnectionStrings__Inventar`
- `StorefrontEmail__SenderEmail`
- `StorefrontEmail__SenderDisplayName`
- `StorefrontEmail__SmtpHost`
- `StorefrontEmail__SmtpPort`
- `StorefrontEmail__SmtpUsername`
- `StorefrontEmail__SmtpPassword`

Optional but recommended:

- `StorefrontGoogleAuth__ClientId`
- `StorefrontGoogleAuth__ClientSecret`
- `StorefrontGoogleAuth__CallbackPath`
- `DataProtection__KeysPath`
- `AllowedHosts`
- `ReverseProxy__Enabled`

## Reverse proxy configuration

If the apps will run behind IIS, Nginx, Apache, or another reverse proxy:

1. Set `ReverseProxy:Enabled` to `true`.
2. Fill at least one of:
   - `ReverseProxy:KnownProxies`
   - `ReverseProxy:KnownNetworks`
3. Make sure the proxy forwards:
   - `X-Forwarded-For`
   - `X-Forwarded-Proto`
   - `X-Forwarded-Host`

Example:

```json
"ReverseProxy": {
  "Enabled": true,
  "KnownProxies": [ "127.0.0.1", "::1" ],
  "KnownNetworks": [ "10.0.0.0/24" ]
}
```

## Data Protection keys

Do not rely on ephemeral keys in production.

Set:

- `DataProtection__KeysPath`

Use a directory that:

- survives app restarts
- is backed up
- is shared between instances of the same app if you deploy multiple instances

Recommended:

- separate key directories for admin and storefront
- filesystem permissions restricted to the app identity

## Publish commands

Admin:

```powershell
dotnet publish "C:\Users\PC\OneDrive\Desktop\Inventar-Stabilna Verzija\Tepih-Inventory-main\Inventar.csproj" -c Release -o "C:\deploy\inventar-admin"
```

Storefront:

```powershell
dotnet publish "C:\Users\PC\OneDrive\Desktop\Inventar-Stabilna Verzija\Inventar.Storefront\Inventar.Storefront.csproj" -c Release -o "C:\deploy\inventar-storefront"
```

If you need framework-dependent output only, the commands above are enough.

## Database migrations

Before first production start, run database migration against the production database.

Admin project:

```powershell
dotnet ef database update --project "C:\Users\PC\OneDrive\Desktop\Inventar-Stabilna Verzija\Tepih-Inventory-main\Inventar.csproj" --startup-project "C:\Users\PC\OneDrive\Desktop\Inventar-Stabilna Verzija\Tepih-Inventory-main\Inventar.csproj"
```

Storefront currently uses the same SQL Server connection string, so make sure the shared production database is the intended target before applying migrations.

If you are replacing an already published legacy Inventar installation while keeping its current SQL database, use this cutover guide:

- [SmarterASP Existing DB Cutover](/C:/Users/PC/OneDrive/Desktop/Inventar-Stabilna%20Verzija/docs/smarterasp-existing-db-cutover.md)

## Health endpoints

After deployment, verify:

- admin live: `https://your-admin-host/health/live`
- admin ready: `https://your-admin-host/health/ready`
- storefront live: `https://your-shop-host/health/live`
- storefront ready: `https://your-shop-host/health/ready`

Expected behavior:

- `/health/live` should report that the app process is running
- `/health/ready` should confirm database connectivity

## Post-deployment checklist

- Confirm both apps start under `ASPNETCORE_ENVIRONMENT=Production`.
- Confirm `AllowedHosts` is not `*`.
- Confirm HTTPS is active end-to-end.
- Confirm Data Protection keys are written to the intended directory.
- Confirm storefront email sending works with the production SMTP account.
- Confirm Cloudinary uploads work from admin product create/edit flows.
- Confirm Google login redirect URI matches the deployed storefront URL.
- Confirm the health endpoints return healthy.
- Confirm logs are being written to `Logs/`.

## Notes

- `Inventar.Storefront` requires SMTP password in production and now validates that on startup.
- `Inventar` requires full Cloudinary configuration in production and now validates that on startup.
- If you want different cache durations, hostnames, or log retention, those can be tuned later without restructuring the apps again.
