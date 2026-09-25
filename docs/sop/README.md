# Standard Operating Procedures — Construction Platform

How to run the business on the platform, screen by screen and scenario by scenario.
Written for the people who use it: owners, admins, store keepers, sales staff and
customers. Every label in quotes is exactly what appears on screen.

> **Living document.** The SOP is updated in the same change as the feature it
> describes (see "Keeping this SOP current" below). If a screen and this SOP
> disagree, the screen is right and the SOP has a bug — report it.

| # | Guide | For |
| --- | --- | --- |
| 1 | [Getting started](01-getting-started.md) — signing in, the screen layout, lists, forms, messages | Everyone |
| 2 | [Administration](02-administration.md) — users, roles, feature access, audit log, business settings | Owner, admin |
| 3 | [Catalogue](03-catalogue.md) — products, Excel import, brands, units | Catalogue manager |
| 4 | [Inventory](04-inventory.md) — warehouses, stock, receipts, transfers, adjustments, opening stock, ledger check, delivery areas | Store keepers, inventory manager |
| 5 | [Sales](05-sales.md) — price lists, GST rates, customers, orders | Sales, accounts |
| 6 | [Online store](06-online-store.md) — the customer's side, at `/shop` | Customers, support staff |
| 7 | [Scenarios](07-scenarios.md) — step-by-step playbooks for real situations | Everyone |
| 8 | [Troubleshooting](08-troubleshooting.md) — message on screen → cause → what to do | Everyone |
| 9 | [Known limits](09-known-limits.md) — what is not built yet, and behaviours to watch | Owner, admin |

## Which guide do I need?

| I am… | Read |
| --- | --- |
| Setting the business up for the first time | 1, 2, then [Scenario A](07-scenarios.md#a-first-time-setup-go-live-checklist) |
| A store keeper at one warehouse | 1, 4, Scenarios C–F |
| Managing prices and GST | 1, 5, Scenarios G–H |
| Handling store orders and customers | 1, 5, 6, Scenarios I–K |
| Helping a customer on the phone | 6, 8 |

## What the platform covers today

Phases 1–3 are live: identity and access, catalogue, warehouse stock on a ledger,
prices and GST, and an online store where customers place orders that **hold**
stock. Confirming, dispatching, invoicing and payment arrive in Phase 4 — until
then those steps happen by phone and paper (see [Known limits](09-known-limits.md)).

## Five rules everyone should know

1. **Stock is never typed in.** It changes only through a document: receipt,
   transfer, adjustment or opening stock. Every change is in the ledger for ever.
2. **Nothing is deleted.** Records are deactivated; documents are reversed by a
   new document. The audit log keeps who did what, when and from where.
3. **Prices and GST are dated.** A change starts a new price from now or a later
   date; orders already placed keep their price.
4. **Customers never see warehouses.** They see one available quantity, a price
   and a delivery date.
5. **Access is feature + place.** A person can do only what their roles and
   feature access allow, and only in the warehouses they are given.

## Keeping this SOP current (for the development team)

- Any change that adds or changes something a user sees — a screen, a button,
  a field, a message, a rule — updates the matching guide **in the same commit**.
- A new screen gets a section in its guide; a new end-to-end flow gets a
  scenario in [07-scenarios.md](07-scenarios.md); a new error message people may
  hit gets a row in [08-troubleshooting.md](08-troubleshooting.md).
- When a limit in [09-known-limits.md](09-known-limits.md) is removed, delete it
  there and describe the new behaviour where it belongs.
- Add a line to the change log below.

## Change log

| Date | Change |
| --- | --- |
| 2026-09-25 | Phase 4 step 1: "Confirm order" (Confirmed status and tab; confirmed stock never expires; customers call to change a confirmed order). |
| 2026-09-25 | Decision #2 = No: an order ships from one warehouse; the store offers the best single warehouse's stock and one delivery date; new message "These items cannot all be delivered together…". |
| 2026-09-25 | Role hierarchy ("Reports to", Role hierarchy chart, greyed-out roles), protected Administrator role, and immediate effect on signed-in users ("Your session has ended", Sign out everywhere). Scenarios B3–B4 added. |
| 2026-09-25 | First edition, covering Phases 1–3 (access, catalogue, inventory, pricing and GST, customers, store orders), the phone country-code field and masked password fields. |
