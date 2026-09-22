# Roadmap

Nine phases. Each ends at a gate that proves something works, not at a date.
Current position: **Phase 0**.

| Phase | Name | Weeks | Gate proves |
| --- | --- | --- | --- |
| 0 | Discovery, foundation, environments | 3 | A signed-in hello world deploys to staging |
| 1 | Identity, access control, master data | 4 | A scoped role sees exactly what it should |
| 2 | Warehouse, inventory, admin portal | 6 | Stock moves and the ledger always agrees |
| 3 | Storefront, cart, pricing, tax | 6 | A customer buys, correct tax, no warehouse leak |
| 4 | Orders, fulfilment, delivery | 5 | A split order lands with two invoices |
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
