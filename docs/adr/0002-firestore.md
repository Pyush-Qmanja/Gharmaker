# ADR 0002 — Firestore as the only database

**Status:** accepted · **Date:** 2026-09-23 · **Supersedes:** ADR 0001

## Decision

Cloud Firestore (native mode) is the only database. PostgreSQL, EF Core and
migrations are removed. Redis, FCM and S3 are unchanged.

The API is the only Firestore client, through the Admin SDK. `firestore.rules`
denies every direct client request, so tenancy, validation and warehouse
opacity (P1) stay enforced in one place — the API.

## How the platform conventions map onto Firestore

| Convention | Implementation |
| --- | --- |
| One data-access path | `IRepository<T>` (generic) → `IFirestoreContext` (one `FirestoreDb` per process) |
| Tables: plural snake_case | Collections named by `FirestoreNaming.Collection<T>()` — `Brand` → `brands` |
| Columns: snake_case | Fields named by `FirestoreNaming.Field(nameof(...))` — `UpdatedAt` → `updated_at` |
| Audit fields | Stamped only in `UnitOfWork.SaveChangesAsync`; insert-only fields never overwritten |
| Every business row has `org_id` | `Repository<T>` filters every query and lookup by the caller's `org_id` |
| Multi-document atomic writes | One `WriteBatch` per `SaveChangesAsync` (max 500 writes) |
| Unique constraints | None in Firestore. `CrudService.RequireUniqueAsync` checks before writing → 409 |
| Passwords | Firebase Authentication only; `users.auth_uid` links the account, the API issues its own JWT |
| Roles, user roles, user scopes | `roles` collection; `role_ids` and `scopes` embedded on each user (blueprint's `user_roles` / `user_scopes`) |
| Money and quantity `decimal(18,4)` | Stored as invariant strings — exact, never `double` |
| Pagination | `Count()` aggregation + `Offset`/`Limit` |
| Search | Prefix match on a normalised field (e.g. `slug`); no "contains" search |
| Schema changes | No migrations. New fields appear on write; composite indexes live in `firestore.indexes.json` |

## Consequences — accepted knowingly

ADR 0001's objections still hold. They are now costs we carry:

- **No joins.** Reading `order → allocation → warehouse → batch` is several
  reads, or data is denormalised onto the document that needs it.
- **No ad-hoc grouping.** Brand-wise stock, monthly revenue and similar reports
  need aggregation code (maintained counters or a BigQuery export).
- **Every new query shape needs a composite index** in `firestore.indexes.json`,
  deployed with `firebase deploy --only firestore:indexes`. The emulator does
  not enforce indexes, so a missing one only shows up against the real project.
- **Uniqueness is check-then-write.** Two simultaneous creates can both pass the
  check. Acceptable for master data. For anything that must be strictly unique,
  use a Firestore transaction or a lock document whose id is the unique value.
- **Write hotspots.** A per-SKU availability counter written at checkout speed
  exceeds the sustained per-document write rate. Use sharded counters or
  reserve against lot documents (see `ledgers.md`).
- **Ledgers (P2, P3).** Append-only entries plus a balance document, written in
  one batch or transaction. Rebuilding a balance means reading every entry.
- **Sequential ids.** Document ids are UUID v7 (time-ordered). Firestore advises
  against monotonically increasing ids at high write rates. Fine at current
  volume; revisit for the high-volume collections (`attendance`, ledgers).

## Revisit if

Reporting needs outgrow counters and exports, or ledger contention becomes a
real problem in production. PostgreSQL for supply, stock and money, with
Firestore for the site half, is still the fallback split described in ADR 0001.
