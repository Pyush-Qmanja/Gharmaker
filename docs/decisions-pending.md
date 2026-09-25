# Open decisions

Close every row before Phase 0 ends. Do not code around an open decision —
raise it instead.

| # | Question | Recommendation | Status |
| --- | --- | --- | --- |
| 4 | Supervisors set wages freely, or within a band? | Within a band. Main protection against wage drift, costs nothing to build | open |
| 5 | Can a supervisor approve attendance they marked themselves? | Yes for daily labour, never their own record, never a payout | open |
| 6 | Multi-tenant — will outside contractors use the system? | Decide now. Retrofitting tenancy is a rewrite | open |
| 8 | Is the supervisor app Android-only? | If yes, MAUI needs no Mac build agent | open |
| P4a | Invoice numbering format | One series per financial year, e.g. `INV/2627/000001` | open — owner decides later; must close before the first invoice is issued |
| P4b | Is e-invoicing (IRN + QR from the government portal) required? | Depends on turnover; build without it, add the portal integration if required | open — assumed not required for the beta |

## Closed

| # | Question | Decision | Where |
| --- | --- | --- | --- |
| DB | Relational or document store | Firestore only (supersedes PostgreSQL) | `adr/0002-firestore.md` |
| S1 | May stock go negative? | No. A posting that would take any balance below zero is refused (422), checked inside the transaction | `StockPoster` |
| S2 | Batches and bins in Phase 2? | Not yet: the ledger is keyed by warehouse and SKU. `batch_id` / `bin_id` are added when batch-tracked materials (cement dates, tile shades) are needed | `ledgers.md` |
| S3 | Transfers in one step or two? | Two: stock leaves on send (in transit), arrives when the destination receives it | `StockService` |
| 1 | Invoice from one registered address? | Yes: legal name, GSTIN and registered address live on the organisation (Business settings); its state decides CGST + SGST or IGST. Warehouses never appear on a tax document | `BusinessSettingsService`, `GstCalculator` |
| 3 | Slab pricing: whole quantity or only above the boundary? | Whole quantity takes the rate of the highest slab it reaches | `SlabPricing` |
| 7 | Storefront in Razor or React? | Razor, as an MVC area (`/shop`) in `Platform.Web`, with its own customer cookie | `Areas/Shop` |
| P3a | Products with no stock on the store? | Listed, marked "Out of stock" (or "Not delivered to your PIN code"), Add to cart disabled | `ShopCatalogService` |
| P3b | How long does checkout hold stock before confirmation? | 48 hours (`Storefront:HoldMinutes`); then the order expires and the stock is freed. Confirmation, payment and allocation arrive in Phase 4 | `HoldExpiryWorker` |
| 2 | Can one order ship from two warehouses when no single one has enough? | **No** (kept simple): one order ships from one warehouse, as one shipment with one invoice. The store offers per item what the best-stocked single serving warehouse holds, and refuses a cart that no single warehouse can supply together ("These items cannot all be delivered together…"); the customer orders the rest separately | `AllocationPlanner.PlanOrder` |
| P4c | How is payment taken in Phase 4? | Cash only, recorded by staff (on delivery); no online gateway yet. Credit and a gateway may come later | Phase 4 plan |
| P3c | GST shown before the address is known? | Within the seller's state (or the customer's saved state for the same PIN code); checkout recomputes from the delivery address | `ShopCartService` |
