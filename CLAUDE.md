# Construction Materials Supply & Site Workforce Platform

Two halves on one platform: a materials supply business (warehouse-wise stock,
brand-wise catalogue, customer storefront) and a construction site operations
system (level-wise logins, teams, tasks, attendance, wages, wallets).

Full blueprint: `docs/blueprint.md`. Read it before designing anything new.

## Stack

| Layer | Choice |
| --- | --- |
| Core API | .NET 8 Web API (C#) |
| Customer web, admin, warehouse portals | ASP.NET Core MVC, Razor, HTML/CSS |
| Supervisor and driver apps | .NET MAUI + SQLite offline store |
| Worker view | Page in the MVC app, plus SMS |
| Database | PostgreSQL (see `docs/adr/0001-database.md`) |
| Cache, holds, locks | Redis |
| Push notifications | Firebase Cloud Messaging |
| Files and images | S3 |
| Hosting | AWS ap-south-1 (Mumbai) |

## The ten rules

These are not style preferences. A change that breaks one of these is wrong even
if it compiles and the tests pass.

**P1 — Warehouse opacity is enforced at the API, not the UI.**
No customer-facing endpoint returns a warehouse id, name, code, address, lat or
lng. Availability is one aggregated number. Delivery is a date range. Adding a
customer-facing endpoint means adding it to the opacity test suite in the same
commit. See `.claude/rules/storefront.md`.

**P2 — Stock has one source of truth, and it is a ledger.**
Quantity on hand is never an editable column. It is the sum of the append-only
`stock_ledger_entries`. `stock_balances` is a materialised cache, rebuildable.
If a balance and its ledger disagree, the ledger is right. Never write a balance
without writing the ledger entry that justifies it.

**P3 — Money is an append-only ledger too.**
Wallets, customer credit, payments. Nothing is edited. Corrections are reversing
entries that reference the original.

**P4 — Availability is promised, then allocated.**
Checkout reserves against a soft hold with an expiry. Warehouse, bin and batch
are assigned after payment or credit approval — never before.

**P5 — Field capture is offline-first.**
The MAUI apps write locally and sync when they can. Every field-captured record
carries a client-generated UUID and a device timestamp. Server stores both device
time and server time. See `.claude/rules/mobile.md`.

**P6 — Permissions are capability plus scope, never role name alone.**
`if (role == "Supervisor")` anywhere in the codebase is a review failure. Every
check is two-part: does this role hold this capability, and is this object in
this user's scope. Out-of-scope returns 404, not 403.

**P7 — Quantities carry their unit everywhere.**
No quantity is ever a bare number, in a signature, a DTO, a column or a variable.
Every quantity is a value plus a UOM code. Conversion happens only in
`IUomConversionService`. No local arithmetic on quantities anywhere else.

**P8 — Prices are dated, never overwritten.**
A price change inserts a new row with `effective_from`. Same for tax rates and
freight rules. Historical orders reprice to what was true when placed.

**P9 — Every document is immutable once issued.**
Invoices, delivery challans, GRNs, payout statements. Changes are made by credit
note, debit note, or a new document referencing the original.

**P10 — Everything that matters is auditable.**
Audit is written by middleware, not remembered per-controller. Actor, entity,
action, before, after, IP, device, timestamp.

## Conventions

- Money: `decimal(18,4)` plus a currency code. Never `float` or `double`.
- Quantity: `decimal(18,4)` plus a UOM code. Never a bare number (P7).
- Timestamps: stored UTC, rendered IST. Field records carry device and server time.
- Primary keys: UUID v7. Never expose a sequential integer to a user.
- Human references: `ORD-2627-004512` — prefix, financial year, sequence.
- Tables: plural snake_case. Every business table carries `org_id`.
- Audit columns on every table: `created_at`, `created_by`, `updated_at`, `updated_by`.
- Masters soft-delete with `is_active`. Transactions are never deleted.
- All list endpoints paginate. All writes are idempotent by a client-supplied key.

## Module boundaries

Supply modules never read site tables directly. Site modules never read inventory
tables directly. They exchange the indent and the order through a defined
contract. Keep this seam clean — it is what lets the two halves be tested and
shipped independently.

```
Platform core   Identity · Audit · Notification · Documents
Supply half     Catalog · Inventory · Pricing · Orders · Storefront
Site half       Projects · Workforce · Wallet · Site materials
Bridge          Site materials --indent--> Orders   (the only crossing)
```

## Current phase

Phase 0 — discovery and foundation. See `docs/roadmap.md`.
Open decisions are in `docs/decisions-pending.md`; do not code around one, ask.

## Working agreements

- Read `docs/blueprint.md` for the relevant section before designing a new module.
- When a change touches stock, money or permissions, say which of P1–P10 apply
  before writing code.
- Prefer a failing test that proves the rule over a comment describing it.
- If a requirement conflicts with P1–P10, stop and raise it. Do not quietly pick.
