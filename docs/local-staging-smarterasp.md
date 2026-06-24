# Local Staging Against SmarterASP Staging Database

Use this path when:

- the live SmarterASP site must stay untouched
- the staging database already exists on SmarterASP
- the hosting account cannot create extra staging sites because of quota limits

This setup runs both new apps locally on your machine while both point to the restored SmarterASP staging database.

If the staging database already had one or more failed or partial upgrade attempts, first restore a fresh backup of the live database into staging again, then rerun:

- `live-db-preflight.sql`
- `inventar-live-upgrade-complete.sql`
- `post-cutover-smoke-test.sql`

## Goal

- old live site stays online
- staging database is upgraded and tested safely
- new admin app runs locally against staging DB
- new storefront app runs locally against staging DB

## Files prepared in this repository

- admin local staging profile:
  - [launchSettings.json](/C:/Users/PC/OneDrive/Desktop/Inventar-Stabilna%20Verzija/Tepih-Inventory-main/Properties/launchSettings.json)
- storefront local staging profile:
  - [launchSettings.json](/C:/Users/PC/OneDrive/Desktop/Inventar-Stabilna%20Verzija/Inventar.Storefront/Properties/launchSettings.json)
- admin staging config template:
  - [appsettings.Staging.local.example.json](/C:/Users/PC/OneDrive/Desktop/Inventar-Stabilna%20Verzija/Tepih-Inventory-main/appsettings.Staging.local.example.json)
- storefront staging config template:
  - [appsettings.Staging.local.example.json](/C:/Users/PC/OneDrive/Desktop/Inventar-Stabilna%20Verzija/Inventar.Storefront/appsettings.Staging.local.example.json)
- LocalStaging config validator:
  - [test-local-staging-config.ps1](/C:/Users/PC/OneDrive/Desktop/Inventar-Stabilna%20Verzija/scripts/test-local-staging-config.ps1)
- LocalStaging starter:
  - [start-local-staging.ps1](/C:/Users/PC/OneDrive/Desktop/Inventar-Stabilna%20Verzija/scripts/start-local-staging.ps1)
- LocalStaging stopper:
  - [stop-local-staging.ps1](/C:/Users/PC/OneDrive/Desktop/Inventar-Stabilna%20Verzija/scripts/stop-local-staging.ps1)
- LocalStaging health check:
  - [test-local-staging-health.ps1](/C:/Users/PC/OneDrive/Desktop/Inventar-Stabilna%20Verzija/scripts/test-local-staging-health.ps1)
- LocalStaging state reset:
  - [reset-local-staging-state.ps1](/C:/Users/PC/OneDrive/Desktop/Inventar-Stabilna%20Verzija/scripts/reset-local-staging-state.ps1)
- detailed smoke checklist:
  - [local-staging-smoke-checklist.md](/C:/Users/PC/OneDrive/Desktop/Inventar-Stabilna%20Verzija/docs/local-staging-smoke-checklist.md)

## 1a. Quick helper commands

If PowerShell blocks local scripts on your machine, run them with `-ExecutionPolicy Bypass`.

Validate only the core LocalStaging setup:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\PC\OneDrive\Desktop\Inventar-Stabilna Verzija\scripts\test-local-staging-config.ps1"
```

Validate a full run that must include uploads and email flows:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\PC\OneDrive\Desktop\Inventar-Stabilna Verzija\scripts\test-local-staging-config.ps1" -RequireCloudinary -RequireStorefrontEmailPassword
```

Start both apps in LocalStaging:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\PC\OneDrive\Desktop\Inventar-Stabilna Verzija\scripts\start-local-staging.ps1"
```

Check health endpoints again later:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\PC\OneDrive\Desktop\Inventar-Stabilna Verzija\scripts\test-local-staging-health.ps1"
```

Stop both apps when done:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\PC\OneDrive\Desktop\Inventar-Stabilna Verzija\scripts\stop-local-staging.ps1"
```

Reset LocalStaging cookies/key state if login or session behavior looks wrong:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\PC\OneDrive\Desktop\Inventar-Stabilna Verzija\scripts\reset-local-staging-state.ps1"
```

## 1. Prepare the local staging config files

Create these two files by copying the `.example` files and removing `.example` from the filename:

- `Tepih-Inventory-main/appsettings.Staging.local.json`
- `Inventar.Storefront/appsettings.Staging.local.json`

These files are already ignored by git through:

- `appsettings.Local.json`
- `appsettings.*.local.json`

## 2. Fill the staging database connection string

In both files, set `ConnectionStrings:Inventar` to the SmarterASP staging database.

Expected format:

```json
"ConnectionStrings": {
  "Inventar": "Data Source=SQLxxxx.site4now.net;Initial Catalog=your_staging_database;User Id=your_staging_user;Password=your_staging_password;Encrypt=True;TrustServerCertificate=True;"
}
```

You can get these values in SmarterASP from the staging database details page:

- SQL server name
- database name
- SQL username
- SQL password

## 3. Fill only the extras you actually want to test

### Admin

If you want to test image or video upload from admin, fill:

- `CloudinarySettings:CloudName`
- `CloudinarySettings:ApiKey`
- `CloudinarySettings:ApiSecret`

If you only want to test inventory, reports, sales, and order management, Cloudinary is not required for startup in `Staging`.

### Storefront

To start storefront cleanly in `Staging`, fill:

- `StorefrontEmail:SenderEmail`
- `StorefrontEmail:SenderDisplayName`
- `StorefrontEmail:SmtpHost`
- `StorefrontEmail:SmtpPort`
- `StorefrontEmail:SmtpUsername`

If you also want to test:

- email login
- checkout verification emails
- order confirmation emails

then also fill:

- `StorefrontEmail:SmtpPassword`

Google login is optional for local staging. Only fill it if you want to test that flow too.

## 4. Run the admin app locally

### From Visual Studio

Select profile:

- `LocalStaging`

Project:

- `Tepih-Inventory-main/Inventar.csproj`

Local URLs:

- `https://localhost:7189`
- `http://localhost:5075`

### From terminal

```powershell
dotnet run --launch-profile LocalStaging --project "C:\Users\PC\OneDrive\Desktop\Inventar-Stabilna Verzija\Tepih-Inventory-main\Inventar.csproj"
```

## 5. Run the storefront app locally

### From Visual Studio

Select profile:

- `LocalStaging`

Project:

- `Inventar.Storefront/Inventar.Storefront.csproj`

Local URLs:

- `https://localhost:7241`
- `http://localhost:5241`

### From terminal

```powershell
dotnet run --launch-profile LocalStaging --project "C:\Users\PC\OneDrive\Desktop\Inventar-Stabilna Verzija\Inventar.Storefront\Inventar.Storefront.csproj"
```

## 6. Confirm both apps are really using staging DB

Check:

- inventory rows are visible in admin
- sales rows are visible in admin
- storefront catalog loads expected products
- new test changes affect only staging DB data
- live site remains unchanged

Recommended health endpoints:

- admin:
  - `https://localhost:7189/health/live`
  - `https://localhost:7189/health/ready`
- storefront:
  - `https://localhost:7241/health/live`
  - `https://localhost:7241/health/ready`

## 7. Local staging smoke checklist

### Admin

- login works
- inventory index opens
- QR scanning view opens
- sales screens open
- storefront orders index opens
- storefront catalog admin screens open

### Storefront

- home page opens
- catalog opens
- product details open
- cart works
- checkout page opens
- account login page opens

### Database

- `commerce.WebOrders` stays empty until you intentionally create a test web order
- no unexpected changes appear in the live production database

For the full pre-cutover checklist that includes the recent risky flows like direct-sale placeholder products, poMjeri replacement, and sales edit confirmation, use:

- [local-staging-smoke-checklist.md](/C:/Users/PC/OneDrive/Desktop/Inventar-Stabilna%20Verzija/docs/local-staging-smoke-checklist.md)

## Important note

Do not point local staging to the live production database.
Only use the restored staging copy on SmarterASP until final cutover day.

## Troubleshooting

If admin login appears to succeed but the app immediately behaves strangely afterward, check two things first:

1. Schema drift on staging DB.
If admin logs mention missing columns such as `CreatedForDirectSale` or `DirectSaleOriginalTotal`, the staging database is behind the current code and needs the newest migration applied before LocalStaging testing can continue.

2. Local DataProtection key drift.
If logs mention that the app can not decrypt a key element, clear the local DataProtection keys with:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\PC\OneDrive\Desktop\Inventar-Stabilna Verzija\scripts\reset-local-staging-state.ps1"
```

After that, clear localhost cookies in the browser and sign in again.
