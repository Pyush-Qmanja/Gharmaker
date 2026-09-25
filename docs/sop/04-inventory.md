# 4. Inventory

[← SOP index](README.md)

Menu group **Inventory**: Warehouses, Stock, Goods receipts, Transfers, Adjustments,
Delivery areas. Everything here is **per warehouse** — you see and act only in the
warehouses you are given.

## 4.1 How stock works (read this first)

- Stock is **never typed in**. It changes only by posting a document:

  | Document | Number | Effect |
  | --- | --- | --- |
  | Goods receipt | `GRN-2026-000012` | Adds stock from a supplier |
  | Transfer | `TRF-…` | Takes out of one warehouse; adds to another when received |
  | Adjustment | `ADJ-…` | Damage, expiry, count correction |
  | Opening stock | `OPN-…` | Loads stock at go-live |
  | Reversal | `REV-…` | Undoes one of the above |

  Numbers are `SERIES-YEAR-SEQUENCE` and restart each calendar year.
- Every document writes lines to the **ledger** — the permanent history of every
  movement. Documents are never edited or deleted; a mistake is **reversed**.
- You can enter quantities in any unit the SKU allows (TONNE, BAG, KG…). Stock is
  always counted in the SKU's **base unit**; documents show both "As entered" and
  "In base unit".
- **On hand** = what is physically there. **Reserved** = held for online-store orders.
  **Available** = On hand − Reserved. Transfers and adjustments may only take
  **Available** stock — you cannot move stock a customer is waiting for:
  "Not enough stock — … on hand, … held for orders, … needed."

## 4.2 Warehouses

**Menu:** Inventory › Warehouses. View / Manage (per warehouse).

**"New Warehouse"**:

1. **Warehouse** — "Code" (e.g. `WH-PUNE-01`, stored in capitals), "Name", "Type"
   (Owned / Leased / Third Party), "Responsible person" (an active user).
2. **Address** — never shown to customers. PIN is 6 digits.
3. **Location** (optional) — latitude and longitude, both or neither.
4. **"Create"**.

"View stock →" on a row opens that warehouse's stock.

**Deactivating a warehouse** removes it from every picker at once: no receipts,
transfers, adjustments or new PIN codes. The system does **not** check for stock or
transfers first — see [Scenario F](07-scenarios.md#f-close-a-warehouse).

## 4.3 Stock

**Menu:** Inventory › Stock (Stock View).

- Filters: **"SKU code starts with…"** (code, not name), warehouse ("All my
  warehouses"), **"In stock only"**.
- Columns: SKU, Warehouse, **On hand**, **Reserved**, **Available**, Last movement.
- Click a SKU for its totals, **"By warehouse"** and its **Movements**.
- **"Movements"** (top) — the ledger, newest first: When, Document, Warehouse, SKU,
  Reason, Change (+/−), Balance after, By.
- Buttons at the top, shown only with the right access: "Receive goods", "Transfer",
  "Adjust", "Opening stock", "Ledger check".

## 4.4 Goods receipts — stock arriving from a supplier

**Menu:** Inventory › Goods receipts → **"New receipt"** (Goods receipts Manage in that
warehouse).

1. **"Receiving warehouse"**.
2. **"Supplier"** (required), **"Supplier invoice / challan no."**, "Remarks".
3. **Lines** — "SKU code" (type or pick), "Amount", "Unit" (any allowed unit). Blank rows
   are ignored; **"Add a line"** for more (up to 100). One line per SKU.
4. **"Post receipt"** → "GRN-… posted." Stock is added immediately.

Count what is physically unloaded, not what the invoice says; record any shortfall in
"Remarks" and with the supplier.

## 4.5 Transfers — moving stock between warehouses

**Menu:** Inventory › Transfers → **"New transfer"**.

**Sending** (Transfers Manage at the source):

1. **"From"** (your warehouse), **"To"** (any active warehouse), "Remarks" (vehicle no.,
   driver).
2. Lines as for receipts → **"Send transfer"**.
3. Status **In transit**: stock has left the source and is in neither warehouse's
   Available until received.

**Receiving** (Transfers Manage at the destination):

1. Open the transfer (list or dashboard) → **"Receive into {warehouse}"**.
2. Confirm "Confirm that everything on TRF-… has arrived at …?".
3. Status **Received**; stock is added at the destination.

Receiving is **all lines in full** — there is no partial receipt. If something is short
or damaged on arrival, receive the transfer, then post an **adjustment** (Damage or
Count correction) at the destination with the reason.

## 4.6 Adjustments — damage, expiry, count corrections

**Menu:** Inventory › Adjustments → **"New adjustment"** (Stock Manage).

1. "Warehouse" and **"Reason"**:
   - **Damage** / **Expiry write-off** — enter the amount **lost as a positive number**;
     it is taken out.
   - **Count correction** — **+** to add, **−** to take away (after a physical count).
2. **"Reason in words"** is required, e.g. "12 bags wet after rain on 3 Sep".
3. Lines → **"Post adjustment"**.

## 4.7 Reversing a document

Open the document → panel **"Reverse this document"** → type **"Why is it being
reversed?"** → **"Reverse"** → confirm.

- A new **REV-** document undoes every line; the original shows **Reversed**.
- A document can be reversed once; a reversal cannot be reversed. To redo, post a new,
  correct document.
- Refused if the stock has already gone (e.g. sold or moved) — the same "Not enough
  stock" rule.
- A transfer in transit returns stock to the source; a received transfer is taken out of
  the destination and put back at the source (needs Manage at both).

## 4.8 Opening stock — at go-live only

**Stock › "Opening stock"** (Stock Manage).

1. **"Download Excel template"**. Columns: **Warehouse Code, SKU Code, Quantity, Unit**.
   One row per SKU per warehouse; up to 2000 rows per file.
2. Upload → **"Upload and check"** → review **Ready / Error** per row.
3. **"Confirm and post"** within 30 minutes → one `OPN-` document per warehouse.

**Business rule:** post opening stock **once per warehouse, at go-live**. The system does
not block a second upload, and posting **adds** to what is already there — it does not
replace it. After go-live, use receipts and count corrections instead.

## 4.9 Ledger check

**Stock › "Ledger check"** (Stock Manage in **every** warehouse — typically the
inventory manager).

Adds up every ledger movement and compares it with each stock balance. "The ledger is
always right."

- **"Every balance agrees with its ledger."** — nothing to do.
- Mismatches are listed (Warehouse, SKU, Balance says, Ledger adds up to) →
  **"Rebuild balances from the ledger"**. Only balances are rewritten; the ledger is never
  changed. Report any mismatch to the development team — it should not happen.

Run it weekly, and after any system incident.

## 4.10 Delivery areas — which PIN codes each warehouse serves

**Menu:** Inventory › Delivery areas. View / Manage (per warehouse).

A customer can only order what warehouses **serving their PIN code** hold. Until a PIN is
served, customers there see "Not delivered to your PIN code".

**Add PIN codes:**

1. **"Add PIN codes"** → **"Delivers from"** (warehouse).
2. **"Delivery time (days)"** — 0 = same day, 1 = next day, up to 60.
3. **"PIN codes"** — paste up to 500, separated by commas, spaces or new lines.
4. **"Add PIN codes"** → "N PIN codes added, N updated." A PIN already served just gets the
   new delivery time.

- Change a delivery time: pencil on the row. Stop serving a PIN: bin icon (or untick
  "Delivers to this PIN code").
- One PIN can be served by several warehouses. **An order is never split:** the whole
  order ships from one warehouse that has everything (the fastest, then the fullest).
  The store therefore offers, per item, what the best-stocked single warehouse holds —
  not the total. To sell more in one order, transfer stock into one warehouse.
- Delivery days are calendar days; Sundays and holidays are not skipped.
