# 9. Known limits

[← SOP index](README.md)

What the platform does **not** do yet, and behaviours to watch. Remove an item here when
it is built or fixed, and describe the new behaviour in its guide.

## Not built yet (Phase 4 and later)

| Missing | Do instead, for now |
| --- | --- |
| Dispatch from the system (the warehouse is already chosen at checkout) | Ship from the warehouse under "Stock held" ([5.4](05-sales.md#daily-order-routine-until-the-rest-of-phase-4)) |
| Picking, dispatch, delivery tracking, "Delivered" status | Current manual process |
| GST tax invoices, credit notes, returns | Current billing software |
| Online payment, customer credit | Cash on delivery only, for now (decision P4c) |
| Staff placing or editing an order for a customer | The customer orders on the store; or handle offline |
| Password reset (staff and customers) | Staff: an admin sets up a new account. Customers: contact support |
| Creating a single product or category by form | Excel import ([3.2](03-catalogue.md#32-excel-import)) |
| Renaming or deactivating a category, deactivating a whole product | Contact the development team |
| Partial receipt of a transfer | Receive in full, then adjust the shortfall |
| Product images on the store | The store shows brand initials |
| Site operations (projects, workforce, wallets, mobile apps) | Later phases |

## Behaviours to watch

- **Unconfirmed orders expire after 48 hours** — work the "Awaiting confirmation" list daily.
  Confirmed orders never expire.
- **Opening stock adds; it does not replace**, and the system does not stop a second upload.
  Post it once per warehouse.
- **Deactivating a warehouse** does not check its stock, open orders or transfers first —
  follow [Scenario F](07-scenarios.md#f-close-a-warehouse).
- **Changing a SKU's base unit** after it has stock blocks further postings for it.
- **Changing a unit's standard size, or deactivating a unit,** affects every product at once.
- **Delivery dates are calendar days** — Sundays and holidays are not skipped.
- **Search differs by screen:** Users and Customers by the start of the email; Stock by the
  start of the SKU code; Roles does not filter.
- The customer's order page shows "Cancelled" but **not the reason** typed by staff.
- **Sessions last a fixed 60 minutes** from sign-in; save long forms promptly. (Access
  changes and deactivation do not wait for this — they apply at once.)
- **"What this user can do"** shows access as last saved, not while you tick boxes.
