# SmarterASP Cutover Plan For Existing Inventar Database

This plan keeps the existing production SQL database and upgrades its schema in place.
That avoids manual copying of `Tepisi`, `Prodaje`, users, roles, and claims.

## Target production layout

- `kasmirhome.me` -> `Inventar.Storefront`
- `system.kasmirhome.me` -> new admin `Inventar`
- both applications -> same upgraded SQL database
- `commerce.*` tables -> created by migrations and initially empty

## What the database upgrade should preserve

- keep all existing rows from:
  - `dbo.Tepisi`
  - `dbo.Prodaje`
  - `dbo.AspNetUsers`
  - `dbo.AspNetRoles`
  - `dbo.AspNetUserRoles`
  - `dbo.AspNetUserClaims`
- add missing storefront columns and tables
- backfill legacy defaults for existing products:
  - `OnlinePrice = Price` when null
  - `BroaderCategory = 'Tepih'` when empty or `default`
  - `NarrowerCategory = 'Tepih'` when empty or `default`
  - `IsPublished = 0`
  - `ReservedQuantity = 0`
  - `PoMjeri = 0`
  - `UnID = NULL`
- keep `commerce.*` tables empty until the new storefront starts using them

## Files prepared in this repository

- Safety data migration:
  - [20260609190000_BackfillLegacyProductionDefaults.cs](/C:/Users/PC/OneDrive/Desktop/Inventar-Stabilna%20Verzija/Tepih-Inventory-main/Migrations/20260609190000_BackfillLegacyProductionDefaults.cs)
- Artifact generator:
  - [generate-live-upgrade-artifacts.ps1](/C:/Users/PC/OneDrive/Desktop/Inventar-Stabilna%20Verzija/scripts/generate-live-upgrade-artifacts.ps1)
- SQL checks:
  - [live-db-preflight.sql](/C:/Users/PC/OneDrive/Desktop/Inventar-Stabilna%20Verzija/docs/sql/live-db-preflight.sql)
  - [post-cutover-smoke-test.sql](/C:/Users/PC/OneDrive/Desktop/Inventar-Stabilna%20Verzija/docs/sql/post-cutover-smoke-test.sql)

## Recommended rollout

### 1. Prepare staging from the current live database

1. In SmarterASP, back up the current live SQL database.
2. Restore that backup into a separate staging database.
3. Either publish both new apps to temporary SmarterASP sites, or run them locally if the hosting account can not create extra staging sites because of quota limits.
4. Point both new apps to the staging database.

If you need the local fallback path, use:

- [Local Staging Against SmarterASP Staging Database](/C:/Users/PC/OneDrive/Desktop/Inventar-Stabilna%20Verzija/docs/local-staging-smarterasp.md)

### 2. Generate and apply the upgrade SQL

1. Run:

```powershell
.\scripts\generate-live-upgrade-artifacts.ps1
```

2. Run `live-db-preflight.sql` against the staging database and save the result.
3. Run `inventar-live-upgrade-complete.sql` against the staging database.
4. Run `post-cutover-smoke-test.sql` against the staging database.

### 3. Validate the staging environment

Check at minimum:

- admin login works
- existing inventory rows are visible
- existing sales rows are visible
- user roles still authorize the right screens
- storefront starts correctly
- storefront catalog opens
- `commerce.WebOrders` is empty before the first online order

### 4. Cut over production

1. Put the old site in a short maintenance window.
2. Create one more fresh backup of the live database.
3. Run the same upgrade SQL against the live database.
4. Publish the two new apps to their production SmarterASP sites.
5. Map:
   - `kasmirhome.me` to the storefront site
   - `system.kasmirhome.me` to the new admin site
6. Update GoDaddy DNS records.
7. Run the smoke test again on production.

## SmarterASP and GoDaddy mapping

### Storefront site

- create a new SmarterASP site for `Inventar.Storefront`
- map `kasmirhome.me`
- optionally map `www.kasmirhome.me`

### Admin site

- create another SmarterASP site for the new admin app
- map `system.kasmirhome.me`

### DNS

Keep GoDaddy as registrar and DNS provider unless you explicitly want a later registrar transfer.

- root domain `@` -> A record to the storefront site IP
- `www` -> CNAME to root domain or mapped per your preferred setup
- `system` -> A record to the admin site IP

## Temporary admin-only release option

If you publish only the new admin app first:

- map `system.kasmirhome.me` to the new admin site
- temporarily map `kasmirhome.me` and `www.kasmirhome.me` to that same admin site as well
- keep the admin app `HostRedirect` section enabled so requests for `kasmirhome.me` and `www.kasmirhome.me` are redirected to `https://system.kasmirhome.me`
- when `Inventar.Storefront` goes live later, remove `kasmirhome.me` and `www.kasmirhome.me` from the admin site mapping and bind them to the storefront site instead

## Production configuration minimums

Before production startup succeeds, these values must exist:

### Admin

- `ConnectionStrings:Inventar`
- `CloudinarySettings:CloudName`
- `CloudinarySettings:ApiKey`
- `CloudinarySettings:ApiSecret`

### Storefront

- `ConnectionStrings:Inventar`
- `StorefrontEmail:SenderEmail`
- `StorefrontEmail:SenderDisplayName`
- `StorefrontEmail:SmtpHost`
- `StorefrontEmail:SmtpPort`
- `StorefrontEmail:SmtpUsername`
- `StorefrontEmail:SmtpPassword`

## Important rule

Do not run the first database upgrade attempt directly against the only live database.
Always test the exact script against a restored staging copy first.
