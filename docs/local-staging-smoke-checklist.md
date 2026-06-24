# Local Staging Smoke Checklist

Use this checklist during the next LocalStaging pass before live cutover.

Recommended start sequence:

1. Run the config validator.
2. Start both apps in `LocalStaging`.
3. Confirm both health endpoints respond.
4. Walk through the checks below in order.
5. Save screenshots and exact repro steps for anything that fails.

Helpful commands:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\PC\OneDrive\Desktop\Inventar-Stabilna Verzija\scripts\test-local-staging-config.ps1"
powershell -ExecutionPolicy Bypass -File "C:\Users\PC\OneDrive\Desktop\Inventar-Stabilna Verzija\scripts\start-local-staging.ps1"
powershell -ExecutionPolicy Bypass -File "C:\Users\PC\OneDrive\Desktop\Inventar-Stabilna Verzija\scripts\test-local-staging-health.ps1"
powershell -ExecutionPolicy Bypass -File "C:\Users\PC\OneDrive\Desktop\Inventar-Stabilna Verzija\scripts\stop-local-staging.ps1"
```

## Admin core

- Login works with an admin account.
- Inventory index opens and data looks normal.
- QRCodeScanning view opens without console or UI errors.
- ScannedProductsToBePurchased view opens when items are added.
- Sales grouped screens, all sales, and sales details all open.
- Storefront order index and details open.
- Storefront catalog admin index and edit screens open.

## Admin high-risk flows

- In QRCodeScanning, open the add-product modal and confirm search by parameters still works.
- In QRCodeScanning, use `Create product for selling` and create an on-the-fly product.
- Confirm the new on-the-fly product is immediately added to `productTable`.
- In ScannedProductsToBePurchased, confirm the on-the-fly row keeps the expected total selling price.
- In ScannedProductsToBePurchased, confirm `Price` and `Rabat` are not editable for the on-the-fly row.
- Complete the purchase for the on-the-fly product and confirm the flow succeeds.
- In AllSales and sales details, confirm the direct-sale entry is visually highlighted.
- Open `Sales/Edit` for a normal sale, save a harmless change, and confirm the green success message appears on the same page.
- Open `Sales/Edit` for a direct-sale placeholder sale, use `Replace product`, and confirm the replacement succeeds.
- If the replacement target is `poMjeri`, confirm the modal keeps width fixed and only length is entered manually.
- After applying replacement, confirm the sale reflects the new product correctly.

## Admin poMjeri checks

- In QRCodeScanning, scan or search a `poMjeri` product and confirm the size prompt opens.
- Confirm width is fixed and not manually editable in the `poMjeri` prompt.
- Confirm invalid length values are blocked.
- Confirm allowed values calculate max available quantity correctly.
- Complete one `poMjeri` sale and confirm the remaining size changes as expected on the next lookup.

## Storefront core

- Home page opens.
- Catalog opens.
- Search opens and returns live results.
- Product details open.
- Cart opens and quantity updates work.
- Checkout page opens.
- Account login page opens.

## Storefront product checks

- For a regular product with `Dodaj u korpu`, quantity can be changed on details.
- For a sold-out product, adding to cart is blocked.
- For a `poMjeri` product, width is selected from the fixed list, not typed manually.
- For a `poMjeri` product, changing width updates the displayed price without requiring length re-entry.
- For a `poMjeri` product, length pricing still follows the current minimum-length logic.
- Product cards and details media still render acceptably after the recent image/video work.

## Storefront account and email flows

Run these only if SMTP is configured for this LocalStaging round.

- `Posalji kod za prijavu` sends successfully.
- Checkout email verification sends successfully.
- Completing checkout creates the order successfully.
- Order confirmation email is sent successfully.
- The new order appears in storefront account order history.
- The new order appears in admin storefront orders.

## Database safety checks

- Test activity appears only in the staging database.
- The live production site remains unchanged while LocalStaging testing is in progress.
- `commerce.WebOrders` changes only when you intentionally create storefront test orders.
- Newly created on-the-fly products and their sales behave consistently across admin screens.

## If something fails

Capture:

- the exact page and action
- whether the failure is admin or storefront
- the relevant log file from `artifacts\local-staging`
- whether the issue is reproducible
- whether it affects only direct-sale products, only `poMjeri` products, or all products
