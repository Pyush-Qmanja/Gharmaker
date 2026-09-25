# Phase 4 plan — orders, fulfilment, delivery

Decisions it rests on (see `decisions-pending.md`):

- **#2 — no split.** One order ships from one warehouse: one shipment, one invoice.
  Already enforced at checkout (`AllocationPlanner.PlanOrder`).
- **P4c — cash only.** Staff record cash received on delivery. No gateway, no credit yet.
- **P4a — invoice numbering: open.** Built with a configurable series; the owner sets
  the format before the first real invoice.
- **P4b — e-invoicing: assumed not required** for the beta.

**Gate:** an order is confirmed, shipped from one warehouse, invoiced with correct GST,
delivered, and its cash recorded; the stock ledger and the money ledger both agree.

## Order life

```
Placed ──confirm──▶ Confirmed ──dispatch──▶ Dispatched ──deliver──▶ Delivered ──cash in full──▶ Closed
  │                    │
  └─cancel / expire    └─cancel (before dispatch)
```

Status stays derived where it can be (blueprint): "paid" comes from the money
ledger, not a flag.

## Slices, in build order

| # | Slice | What it adds | Rules |
| --- | --- | --- | --- |
| 1 | **Confirm** ✅ done | "Confirm order" after the phone call; holds stop expiring; customer sees "Confirmed" (and must call to change it); staff may still cancel until dispatch | P4 (holds become allocations); orders.manage |
| 2 | **Dispatch** | Pick list for the warehouse; "Dispatch" with vehicle and driver; stock leaves via the ledger as *Sale dispatch* and the hold is consumed; a numbered delivery challan (`DC-`) | P2, P7, P9; new warehouse-scoped feature *Dispatch* (store keepers) |
| 3 | **Tax invoice** | Issued at dispatch, immutable; legal name, GSTIN, HSN, CGST+SGST or IGST as today; printable page; configurable series | P8, P9; blocked for go-live until P4a |
| 4 | **Delivery** | "Mark delivered" with receiver name and note | Dispatch feature |
| 5 | **Cash** | "Record cash" against the invoice; append-only money ledger with reversing entries; order closes when paid in full; daily cash summary per collector | P3, P10; new feature *Payments* |
| 6 | **Cancel / return** | Cancel before dispatch frees the stock; a return after delivery posts *Sale return* stock back and a credit note against the invoice | P2, P3, P9 |
| 7 | **Customer side** | Status timeline on "My orders", invoice download, no warehouse shown | P1 (opacity suite extended) |
| 8 | **Proof** | Test suites for each slice, the gate scenario end to end, ledger checks, SOP update | — |

The 48-hour expiry stays for **unconfirmed** orders only. The SOP's interim
"count correction after dispatch" practice is retired by slice 2.
