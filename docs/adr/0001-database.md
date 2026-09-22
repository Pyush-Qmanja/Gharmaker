# ADR 0001 — Primary database

**Status:** superseded by ADR 0002 (Firestore) · **Date:** 2026-09-23

## Decision

PostgreSQL 16 as the primary store. Redis for cache, holds and locks. Firebase
Cloud Messaging for push. S3 for files. Firestore is not used.

## Context

The platform is two ledgers wearing an e-commerce coat. Stock and money both
require multi-table atomic writes, uniqueness constraints, and ad-hoc grouping
for reporting that nobody has specified yet.

## Why not Firestore

Firestore was considered seriously — it does have multi-document ACID
transactions (capped at 500 writes), server-side `sum`/`count` aggregations,
collection group queries and composite indexes, so several common objections do
not apply. The ones that do:

- **No joins.** `order -> allocation -> warehouse -> batch -> bin` becomes N
  sequential reads or heavy denormalisation. Document references do not
  dereference server-side — a reference is a foreign key you still resolve with
  a second read.
- **No ad-hoc grouping.** "Brand-wise stock, sold this month, revenue, margin"
  cannot be expressed as a query. It becomes pre-aggregation maintained by
  Cloud Functions, or a BigQuery export — a second system.
- **Every new report needs an index and often a backfill.** In SQL someone
  writes a `GROUP BY`. Here it is a deploy.
- **Write hotspots.** A SKU-level availability counter exceeds the sustained
  per-document write limit during a busy checkout hour. The workaround is
  sharded counters, which makes reconciliation harder.
- **Ecosystem.** No EF Core, no LINQ against the store, no migrations, no
  reporting tool that speaks to it.

## Consequences

- Offline sync for the MAUI apps is built rather than inherited. This is the
  real cost of the decision — roughly four to six weeks, and it is why the MAUI
  offline spike is a Phase 0 gate.
- Reporting stays cheap: a read replica and a materialised reporting schema.
- Firebase still earns its place for push, and optionally for phone OTP.

## Revisit if

The site half is ever split into a separate product with its own store. A
PostgreSQL supply half plus a Firestore site half is a defensible split, since
they meet only at the indent — but it is two databases to operate and is not
worth it inside one product.
