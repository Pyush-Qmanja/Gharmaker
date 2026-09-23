# Open decisions

Close every row before Phase 0 ends. Do not code around an open decision —
raise it instead.

| # | Question | Recommendation | Status |
| --- | --- | --- | --- |
| 1 | Invoice from one registered address, treating warehouses as internal stock points? | Yes — it is the only thing standing between P1 and a legal disclosure | open |
| 2 | Can one order ship from two warehouses when no single one has enough? | Yes, with two invoices and a clear customer message | open |
| 3 | Slab pricing — better rate on the whole quantity, or only above the boundary? | Whole quantity; it is what the trade expects and it explains on an invoice | open |
| 4 | Supervisors set wages freely, or within a band? | Within a band. Main protection against wage drift, costs nothing to build | open |
| 5 | Can a supervisor approve attendance they marked themselves? | Yes for daily labour, never their own record, never a payout | open |
| 6 | Multi-tenant — will outside contractors use the system? | Decide now. Retrofitting tenancy is a rewrite | open |
| 7 | Storefront in Razor or React? | Razor unless faceted filtering across thousands of SKUs is needed. Deferrable to start of Phase 3 | open |
| 8 | Is the supervisor app Android-only? | If yes, MAUI needs no Mac build agent | open |

## Closed

| # | Question | Decision | Where |
| --- | --- | --- | --- |
| DB | Relational or document store | Firestore only (supersedes PostgreSQL) | `adr/0002-firestore.md` |
| S1 | May stock go negative? | No. A posting that would take any balance below zero is refused (422), checked inside the transaction | `StockPoster` |
| S2 | Batches and bins in Phase 2? | Not yet: the ledger is keyed by warehouse and SKU. `batch_id` / `bin_id` are added when batch-tracked materials (cement dates, tile shades) are needed | `ledgers.md` |
| S3 | Transfers in one step or two? | Two: stock leaves on send (in transit), arrives when the destination receives it | `StockService` |
