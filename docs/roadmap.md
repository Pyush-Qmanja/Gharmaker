# Roadmap

Nine phases. Each ends at a gate that proves something works, not at a date.
Current position: **Phases 1, 2 and 3 built; Phase 4 next.** Phase 0's business
checklist (sign-off, open decisions, environments, spikes) is still open.

| Phase | Name | Weeks | Gate proves |
| --- | --- | --- | --- |
| 0 | Discovery, foundation, environments | 3 | A signed-in hello world deploys to staging |
| 1 | Identity, access control, master data | 4 | A scoped role sees exactly what it should |
| 2 | Warehouse, inventory, admin portal | 6 | Stock moves and the ledger always agrees |
| 3 | Storefront, cart, pricing, tax | 6 | A customer buys, correct tax, no warehouse leak |
| 4 | Orders, fulfilment, delivery | 5 | An order is confirmed, shipped from one warehouse, invoiced with correct GST, delivered, and its cash recorded — stock and money ledgers agree |
| 5 | Sites, teams, tasks, attendance | 5 | 50 workers marked offline sync clean |
| 6 | Wages, wallet, payouts, site material | 4 | A payout run balances to the rupee |
| 7 | Dashboards, analytics, integrations | 3 | Every role opens one screen that answers their question |
| 8 | Hardening, UAT, pilot, go-live | 2 | Two weeks of real users, no stop-the-line defect |

Sequential with one squad: ~38 weeks. Two squads parallel from Phase 2: ~27
weeks, but only if Phase 1's permission contract is genuinely locked first.

## Milestones

| Milestone | End of | Who starts using it |
| --- | --- | --- |
| Internal alpha | Phase 2 | Admin and warehouse team, on real stock |
| Private beta | Phase 4 | Selected customers, real paid orders |
| Site pilot | Phase 6 | One live site, full labour cycle |
| GA | Phase 8 | Everyone |

Running real stock from the end of Phase 2 is the most valuable scheduling
decision here — it earns the stock number the availability promise rests on.

## Gates passed

| Phase | How the gate is proven |
| --- | --- |
| 1 | Scope suites: a user scoped to one warehouse sees and changes only that warehouse (404 elsewhere); per-feature access with its own places; nobody can give access they do not hold. |
| 2 | Stock suite: receipts, two-step transfers, adjustments, opening stock and reversals all post through one transactional path; stock never goes negative; 12 concurrent transfers racing for the same stock never over-spend; the ledger check finds zero mismatches after every run. |
| 3 | Storefront suites: a customer registers, browses with a PIN code, fills a cart in any unit and places an order; the price comes from their contract, tier or retail list with the whole-quantity slab; GST is CGST + SGST in the seller's state and IGST across states, rounded per line; the order holds stock in one transaction, is idempotent on its checkout key, and 5 customers racing for 10 bags never over-promise; cancel and expiry give the stock back. Opacity: reflection tests prove no storefront type or endpoint can carry a warehouse, bin, batch, supplier, cost or hold, and every storefront response and page in the suites is scanned for warehouse codes, names and ids. |

## Phase 0 checklist

- [ ] Requirements signed off with the business
- [ ] Every row in `decisions-pending.md` closed
- [ ] Design system and prototypes of the three riskiest screens
      (product detail, warehouse stock, mark attendance)
- [ ] Three environments live with automated deploy
- [ ] Spike: MAUI offline sync, 500 queued records, Android 9 / 2 GB
- [ ] Spike: availability query at 50,000 SKUs across 5 warehouses
- [ ] Data migration assessment — what exists today and in what condition
- [ ] Clone-to-running in under 30 minutes, timed with a fresh developer
