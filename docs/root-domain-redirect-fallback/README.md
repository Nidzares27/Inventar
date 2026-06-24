# Temporary Root Redirect Fallback

This is an optional fallback for the short period where only `Inventar` admin is live.

Primary plan:

- map `system.kasmirhome.me`, `kasmirhome.me`, and `www.kasmirhome.me` to the new admin site
- keep the admin app `HostRedirect` configuration enabled

Fallback plan:

- if you prefer to keep `kasmirhome.me` on the old SmarterASP site slot for a short time, replace that site's root `web.config` with the file in this folder
- that IIS rewrite rule redirects:
  - `kasmirhome.me/*`
  - `www.kasmirhome.me/*`
  to `https://system.kasmirhome.me/*`

Notes:

- this fallback depends on IIS URL Rewrite being available on the hosting plan
- the redirect is temporary, not permanent
- once `Inventar.Storefront` goes live, remove this fallback and bind the root domain to the storefront site instead
