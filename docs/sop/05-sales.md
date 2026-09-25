# 5. Sales

[← SOP index](README.md)

Menu group **Sales**: Orders, Customers, Price lists, GST rates.

**A product can be sold online only when all four are true:**
1. its SKU is active,
2. it has a **price** in the customer's price list (or Retail),
3. its **HSN code has a GST rate**,
4. a warehouse serving the customer's PIN code has **Available** stock.

## 5.1 Price lists

**Menu:** Sales › Price lists. Pricing and GST View / Manage.

**Kinds of list**

| "Applies to" | Who buys from it |
| --- | --- |
| **Retail** | Everyone, including visitors. Exactly one, always active. |
| **Tier** | Customers you put on that tier (e.g. CONTRACTORS). |
| **Contract** | One named customer, for agreed prices. |

**Which price a customer gets:** their **contract** list if it has a price for that SKU,
else their **tier** list, else **Retail**. A SKU with no price in any of these **cannot be
bought** by that customer (it shows "Price on request"); it is never sold at ₹0.

**Create a list:** "New Price list" → "Name", "Code" (e.g. `CONTRACTORS`), "Applies to",
"Customer" (contract only), "Notes" → "Create".

### Setting a price

1. Click **"Open prices →"** on the list. Products appear with their SKUs; search by brand,
   size or SKU code. "Not priced" marks SKUs without a price; **"No GST rate — set one"**
   marks products that cannot be sold yet.
2. Click **"Set price"** / **"Change"** on the SKU.
3. **"Price per"** — the unit you price in (e.g. BAG or TONNE).
4. **Slabs** — first row "Any quantity" is the normal price, **excluding GST**. Add rows for
   bulk rates, e.g. "From quantity" 100 → ₹380.
   **The whole order takes the rate of the highest slab reached**: 120 bags at slabs
   0 → ₹400 / 100 → ₹380 = 120 × ₹380.
5. **"When"** — leave **"Starts"** empty for *now*, or pick a future date and time (India)
   to announce a change; the current price stays until then. Add a **"Reason"** (e.g.
   "Supplier rate revision").
6. **"Save price"**.

Prices are never overwritten: **"Price history"** shows each one as In force, Upcoming or
Replaced. Orders keep the price they were placed at. A price cannot start in the past.

## 5.2 GST rates

**Menu:** Sales › GST rates. Pricing and GST View / Manage.

- The card **"Products that cannot be sold yet"** lists HSN codes that have products but no
  rate — **clear it before launch**. Click **"Set rate"**.
- **"Add GST rate"** → "HSN code" (4, 6 or 8 digits), "GST rate (%)" (total, e.g. 18 or
  28), "Cess (%)" (0 if none), "Starts" (empty = now), "Source" (notification number) →
  **"Save rate"**.
- **Longest match wins:** product HSN `25232910` uses a rate for `25232910`, else
  `252329`, else `2523`. Set a 4-digit rate to cover a whole chapter (e.g. `2523` for all
  cement) and longer codes only for exceptions.
- Rates are dated like prices: a change starts a new rate; "History →" shows all.
- GST is split automatically: **CGST + SGST** within your registered state, **IGST** to
  other states (see [Business settings](02-administration.md#25-business-settings)).

## 5.3 Customers

**Menu:** Sales › Customers. Customers View / Manage.

Customers create their own accounts on the store — staff cannot create them.
**Search by the start of the email.** "Open the store" opens `/shop`.

**Edit a customer:**

- **Details** — "Full name", "Mobile" (country code), "Company", "GSTIN" (for B2B invoices).
  The email is their sign-in and cannot be changed.
- **Prices** — **"Price tier"**: "Retail (no tier)" or an active tier list. A contract list
  for this customer, if any, always comes first.
- **Status** — untick **"Can sign in and order"** to **block**: they are signed out at once
  and cannot sign in or order; their orders stay. Tick again to restore.
- **"Latest orders"** — the last 5; "All orders →" for the rest.

## 5.4 Orders

**Menu:** Sales › Orders. Orders View; Orders Manage to confirm or cancel.

Orders come from the online store. Numbers are `ORD-2627-000001` (financial year
2026-27, April–March).

| Status (staff) | Customer sees | Meaning |
| --- | --- | --- |
| **Awaiting confirmation** | Order placed | Stock is held for 48 hours |
| **Confirmed** | Confirmed | Agreed with the customer by phone; stock stays held, with no expiry, until dispatch |
| **Cancelled** | Cancelled | Cancelled by staff (placed or confirmed) or by the customer (placed only); stock freed |
| **Expired** | Closed | Not confirmed within **48 hours**; stock freed automatically |

Tabs: "Awaiting confirmation" (default from the menu), "Confirmed", "All", "Cancelled", "Expired".
Search by order number.

**Order details:** placed time; GST type ("CGST + SGST (same state)" or "IGST (other
state)"); "Stock is held for this order until …"; customer, phone, buyer GSTIN, delivery
window and address; items with HSN, quantity, unit price, taxable value, GST, total; and
**"Stock held"** — which warehouse holds what (**internal only; never tell the customer
the warehouse**).

### Confirming an order

1. Open **Orders → "Awaiting confirmation"**, oldest first.
2. **Call the customer** on the order's phone number: agree the items, the delivery day
   and that payment is **cash on delivery** (the only payment method for now).
3. On the order, **"Confirm this order"** → **"Confirm order"** → confirm. The order shows
   **Confirmed**; its stock stays held (no more 48-hour expiry); the customer sees
   "Confirmed" and can no longer cancel online — they call you instead.
4. Customer does not answer? Try again later; the hold runs for 48 hours. After it runs
   out the order expires and cannot be confirmed ("The stock hold … has run out") — ask the
   customer to order again.
5. A confirmed order can still be cancelled by staff until it is dispatched.

### Daily order routine (until the rest of Phase 4)

Dispatch, invoicing and cash are not in the system yet:

1. Confirm the day's orders as above.
2. **Confirmed** tab: these are the orders to deliver.
3. Arrange dispatch from the warehouse shown under "Stock held" (always one — orders are
   never split), and raise the invoice in
   your current billing system.
4. If the customer cancels, or you cannot supply: **"Cancel this order"** → reason →
   **"Cancel order"**. Stock is freed at once.
5. The system has no dispatch step yet, so goods delivered against a confirmed order are
   still counted as on hand and held. **Interim practice (agree it with your accountant):**
   once the goods leave, cancel the order with the reason "Dispatched manually on …", then
   post an **Adjustment › Count correction** of −quantity with "Reason in words"
   "Dispatched against ORD-…". (The adjustment cannot take stock the order still holds,
   which is why the order is closed first.) Phase 4's dispatch step replaces this.

The reason typed when cancelling is kept internally; the customer's order page shows only
"Cancelled".
