---
paths:
  - "src/Platform.Domain/Inventory/**/*.cs"
  - "src/Platform.Domain/Wallet/**/*.cs"
  - "src/Platform.Infrastructure/Inventory/**/*.cs"
  - "src/Platform.Infrastructure/Wallet/**/*.cs"
---

# Ledgers — stock and money (P2, P3, P4, P9)

Both ledgers are append-only. `stock_balances` and `wallets` are materialised
caches of them, never independent truth.

## Rules

- **No UPDATE or DELETE on a ledger table, ever.** A correction is a new
  reversing entry carrying `reverses_entry_id` and a reason code.
- **Never write a balance without the ledger entry that justifies it.** Both go
  in one transaction. A balance write with no matching entry is a defect.
- **Every entry names its source document** — `ref_type` plus `ref_id`. An entry
  with no source cannot be defended in a dispute six months later.
- **Rebuild must always be possible.** `RebuildBalancesFromLedger` has to stay
  correct. If a change makes the balance underivable from the ledger, the change
  is wrong.
- Reason codes come from the enum, never a free-text string.

## Stock reason codes

`GRN_RECEIPT` · `TRANSFER_OUT` · `TRANSFER_IN` · `SALE_DISPATCH` ·
`SALE_RETURN` · `DAMAGE` · `EXPIRY_WRITE_OFF` · `AUDIT_CORRECTION` ·
`SITE_ISSUE` · `OPENING_BALANCE`

`OPENING_BALANCE` is migration-only and is locked after go-live.

## Wallet entry types

`EARNING` · `OVERTIME` · `BONUS` · `ADVANCE` · `ADVANCE_RECOVERY` ·
`DEDUCTION` · `PAYOUT` · `REVERSAL`

## Reservation vs dispatch (P4)

A hold reduces **available**, not **on hand**. It writes no ledger entry,
because a hold is not a movement. Only dispatch writes `SALE_DISPATCH`.

Getting this backwards makes the system's count disagree with the physical
count, which breaks the availability promise the whole storefront rests on.

## Concurrency

Reserve against the specific stock lot inside a transaction, not against a
SKU-level counter. Lot-level locking gives independent contention domains and
prevents two customers buying the same last pallet.

## Payout runs

Freeze attendance for the period on calculation. The reconciliation check is a
hard block, not a warning — a run that does not balance to the rupee cannot be
executed:

```
SUM(earned - advances_recovered - deductions) == SUM(payout_lines.net_payable)
```

A correction after a run flows into the **next** run. The closed run is
immutable (P9).
