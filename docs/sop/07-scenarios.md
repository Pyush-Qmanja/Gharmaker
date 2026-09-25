# 7. Scenarios — how to use the portal in real situations

[← SOP index](README.md)

Each scenario says **who** does it, **what they need**, the **steps**, and **how to check**
it worked. The examples use one business throughout:

> **Demo Build Supply Pvt Ltd**, registered in **Maharashtra** (GSTIN `27AAPFU0939F1ZV`),
> with warehouses **WH-PUNE-01** (Pune) and **WH-NSK-01** (Nashik), selling UltraTech
> cement (`CEM-ULT-OPC53-50KG`, counted in BAG), Tata Tiscon TMT bars and Kajaria tiles.

| # | Scenario | Who |
| --- | --- | --- |
| A | [First-time setup (go-live checklist)](#a-first-time-setup-go-live-checklist) | Owner / admin |
| B | [Onboard a new employee](#b-onboard-a-new-employee) | Admin |
| B2 | [An employee leaves](#b2-an-employee-leaves) | Admin |
| B3 | [Set up the role hierarchy and delegate user management](#b3-set-up-the-role-hierarchy-and-delegate-user-management) | Administrator |
| B4 | [Change someone's access while they are signed in](#b4-change-someones-access-while-they-are-signed-in) | Admin / manager |
| C | [A supplier truck arrives](#c-a-supplier-truck-arrives) | Store keeper |
| C2 | [A receipt was entered wrongly](#c2-a-receipt-was-entered-wrongly) | Store keeper |
| D | [Move stock to another warehouse](#d-move-stock-to-another-warehouse) | Store keepers at both ends |
| E | [Damaged goods and the monthly stock count](#e-damaged-goods-and-the-monthly-stock-count) | Store keeper |
| F | [Close a warehouse](#f-close-a-warehouse) | Inventory manager |
| G | [Launch a new product on the store](#g-launch-a-new-product-on-the-store) | Catalogue + pricing |
| H | [A price or GST change from a future date](#h-a-price-or-gst-change-from-a-future-date) | Pricing manager |
| I | [Special rates for contractors or one big customer](#i-special-rates-for-contractors-or-one-big-customer) | Sales / pricing |
| J | [A customer order, start to finish](#j-a-customer-order-start-to-finish) | Customer + sales |
| K | [“The store says it isn't available”](#k-the-store-says-it-isnt-available) | Support |
| L | [Start delivering to a new area](#l-start-delivering-to-a-new-area) | Inventory manager |
| M | [Block a customer](#m-block-a-customer) | Sales manager |
| N | [Weekly controls](#n-weekly-controls) | Owner / inventory manager |
| O | [Find out who changed something](#o-find-out-who-changed-something) | Admin / auditor |

---

## A. First-time setup (go-live checklist)

**Who:** the owner or first administrator, signed in with the Administrator account.
Do the steps in this order — later steps depend on earlier ones.

| Step | Where | Do | Check |
| --- | --- | --- | --- |
| 1 | Administration › Business settings | Legal name, GSTIN, registered address → "Save business details" | Green banner "The store can take orders…" |
| 2 | Administration › Roles | Create the roles you need (see [suggested roles](02-administration.md#23-roles)) | Each role lists its features |
| 3 | Administration › Users | Create a second administrator and your staff ([Scenario B](#b-onboard-a-new-employee)) | Each user's "What this user can do" is right |
| 4 | Catalogue › Units | Check the built-in units; add any you use (e.g. `COIL`) | — |
| 5 | Catalogue › Products › Import from Excel | Import the full catalogue ([3.2](03-catalogue.md#32-excel-import)) | "Import saved…"; products appear by category |
| 6 | Inventory › Warehouses | Create every warehouse with its responsible person | All listed, Active |
| 7 | Inventory › Stock › Opening stock | Physically count, then upload opening stock **once per warehouse** | Stock screen totals match the count sheets |
| 8 | Inventory › Stock › Ledger check | Run it | "Every balance agrees with its ledger." |
| 9 | Sales › GST rates | Add a rate for every HSN; empty the card "Products that cannot be sold yet" | Card gone |
| 10 | Sales › Price lists › Retail › Open prices | Price every SKU you sell online | No "Not priced" on sellable items |
| 11 | Inventory › Delivery areas | Add the PIN codes each warehouse serves, with delivery days | PINs listed |
| 12 | "View store" | Enter a served PIN; open a product; add to cart; check price, GST and delivery date | Everything shows "In stock" and a price |
| 13 | Store | Place one real test order, then cancel it in Sales › Orders | Stock back to Available |

---

## B. Onboard a new employee

**Example:** Ramesh joins as store keeper at the **Pune** warehouse only.
**Who:** an admin with Users Manage (and holding the access being given).

1. Administration › Users › **"+ New user"**.
2. Profile: "Ramesh Patil", phone **+91** `98765 43210`, email `ramesh@demobuild.in`,
   initial password (8+ characters).
3. Roles: tick **"Store keeper"**; **"Where the roles apply"** → `WH-PUNE-01 — Pune`
   (not "Everywhere").
4. Optional exception — he should also *see* Nashik stock: in **Feature access** set
   **Stock → View**, and tick `WH-NSK-01`.
5. **"Create"**. Reopen him and read **"What this user can do"**: Stock Manage in
   WH-PUNE-01, Stock View in WH-NSK-01, and so on.
6. Give him the email and password privately. Ask him to sign in and confirm he sees only
   Pune in the warehouse filters.

**If it goes wrong:** "Choose where … access applies" → you set View/Manage without
ticking a place. "You can only give…" → you are giving more than you hold yourself.

## B2. An employee leaves

1. Administration › Users → search `ramesh@` → ban icon **"Deactivate"** → confirm.
2. He can no longer sign in, and if he is signed in anywhere his next click shows
   "Your session has ended".
3. If he was **"Responsible person"** of a warehouse, edit the warehouse and choose a new
   one.
4. His name stays on everything he posted — nothing is lost.

## B3. Set up the role hierarchy and delegate user management

**Goal:** the Pune branch manager adds and removes their own store keepers, but cannot
touch the sales team, other managers or administrators.
**Who:** an administrator.

1. Administration › Roles › **"+ New role"**: "Branch manager", **Reports to**
   "Administrator", access: Users **Manage**, Roles **Manage**, Warehouses View, Stock
   Manage, Goods receipts Manage, Transfers Manage.
2. **"+ New role"**: "Store keeper", **Reports to** "Branch manager", access: Stock Manage,
   Goods receipts Manage, Transfers Manage, Warehouses View.
3. Open **"Role hierarchy"** and check the chart:
   Administrator › Branch manager › Store keeper.
4. Give Priya (branch manager) the role "Branch manager", **where the roles apply**
   `WH-PUNE-01`.

**What Priya can now do:** create users with the "Store keeper" role at Pune, deactivate
them, sign them out everywhere, create a "Helper" role under Store keeper.
**What she cannot do** (refused with a clear message): change her own role, give
"Branch manager" or "Administrator", edit the sales head's people, deactivate an
administrator, or give a feature she does not hold.

## B4. Change someone's access while they are signed in

**Example:** Ramesh is signed in at the Pune counter. He should also post adjustments.

1. Administration › Users › Ramesh → Feature access → Stock **Manage** at `WH-PUNE-01`
   → "Save changes".
2. Ramesh does **nothing** — no sign-out. His next click shows "Adjust" on the Stock screen.

**Taking access away** works the same way: it disappears on his next click, and the
server refuses it even if the old page is still open.

**He must stop at once** (theft, dismissal): **Deactivate** him — his next click shows
"Your session has ended" and he cannot sign in. Only a lost phone or shared computer?
Use **"Sign out everywhere"** on his user page instead; his password keeps working.

---

## C. A supplier truck arrives

**Example:** 200 bags of UltraTech OPC 53 arrive at Pune with challan `CH-5521`.
**Who:** store keeper with Goods receipts Manage at Pune.

1. Unload and **count** the bags. Put damaged ones aside.
2. Inventory › Goods receipts › **"New receipt"** (or Dashboard › "Receive goods").
3. "Receiving warehouse" `WH-PUNE-01`; "Supplier" `UltraTech Cement Ltd`; "Supplier
   invoice / challan no." `CH-5521`; Remarks "Truck MH12AB1234, 4 bags torn".
4. Line: SKU `CEM-ULT-OPC53-50KG`, Amount `196`, Unit `BAG`.
   (Steel arriving by weight? Enter `2.5` `TONNE`; it is converted to the base unit.)
5. **"Post receipt"** → "GRN-2026-000031 posted."
6. **Check:** Stock → the SKU shows +196 at Pune; the store shows it for Pune's PIN codes.

Claim the 4 torn bags from the supplier. Do not add them to stock.

## C2. A receipt was entered wrongly

**Example:** GRN-2026-000031 was posted as 1960 instead of 196.

1. Open the receipt (Goods receipts list) → **"Reverse this document"** → reason "Typed
   1960 instead of 196" → **"Reverse"** → confirm.
2. A `REV-` document undoes it; the receipt shows **Reversed**.
3. Post a **new** correct receipt (Scenario C).

If the reversal is refused with "Not enough stock", some of that stock has already been
sold or moved. Instead, post an **Adjustment › Count correction** for the difference
(−1764) with the reason, and tell your inventory manager.

---

## D. Move stock to another warehouse

**Example:** Nashik is running low; send 100 bags from Pune.

**At Pune** (Transfers Manage at Pune):

1. Inventory › Transfers › **"New transfer"**. "From" `WH-PUNE-01`, "To" `WH-NSK-01`,
   Remarks "Tempo MH15XY9876, driver Sunil".
2. Line `CEM-ULT-OPC53-50KG` `100` `BAG` → **"Send transfer"** → `TRF-…` **In transit**.
3. Print or note the TRF number for the driver.

**At Nashik** (Transfers Manage at Nashik), when the vehicle arrives:

4. Count the goods. Open the transfer → **"Receive into WH-NSK-01"** → confirm.
5. If 3 bags are damaged, still receive it, then post **Adjustment › Damage** of 3 BAG at
   Nashik with "3 bags torn in transit on TRF-…".

**Check:** Pune −100, Nashik +100 (−3), both visible under Stock › Movements.

**Sent to the wrong warehouse?** Before it is received, reverse the transfer; stock returns
to Pune. Then send a new one.

---

## E. Damaged goods and the monthly stock count

**Damage** (e.g. 12 bags wet after rain):

1. Inventory › Adjustments › **"New adjustment"**. Warehouse `WH-PUNE-01`, Reason
   **Damage**, "Reason in words" "12 bags wet after rain on 3 Sep".
2. Line `CEM-ULT-OPC53-50KG` `12` `BAG` (positive) → **"Post adjustment"**.

**Monthly count:**

1. Stock → filter your warehouse → note On hand per SKU (or export by copying).
2. Count physically. For each difference, post **Count correction**: `+5` if you found more,
   `−5` if fewer, with the reason "Monthly count 30 Sep".
3. If a correction is refused with "held for orders", stock is reserved for store orders;
   check Sales › Orders before correcting.
4. Your inventory manager runs **Ledger check** after the count ([Scenario N](#n-weekly-controls)).

---

## F. Close a warehouse

**Who:** inventory manager. The system does not stop you deactivating a warehouse that
still has stock, so follow this order:

1. **Stop new orders from it:** Inventory › Delivery areas → filter the warehouse → remove
   its PIN codes (or re-add them under another warehouse first — [Scenario L](#l-start-delivering-to-a-new-area)).
2. Wait until Sales › Orders shows no "Awaiting confirmation" order holding its stock
   (check "Stock held" on each order), or cancel them.
3. **Receive every transfer** in transit to or from it.
4. **Transfer out** all remaining stock ([Scenario D](#d-move-stock-to-another-warehouse))
   and post adjustments for what is written off. Stock for it should show nothing on hand.
5. Inventory › Warehouses → **"Deactivate"**. Move its users' places to other warehouses.

---

## G. Launch a new product on the store

**Example:** start selling ACC Gold cement.

1. **Catalogue** (Catalogue Manage): add rows to your master sheet — Category
   `Cement > OPC`, Brand `ACC`, Product `ACC Gold Water Shield`, HSN `2523`, SKU
   `CEM-ACC-GOLD-50KG`, Variant `50 kg bag`, Base Unit `BAG`, Conversions `TONNE=20` —
   and import ([3.2](03-catalogue.md#32-excel-import)).
2. **GST** (Pricing Manage): Sales › GST rates. If `2523` already has 28 %, nothing to do.
   Otherwise "Add GST rate".
3. **Price** (Pricing Manage): Sales › Price lists › Retail › "Open prices" → search `acc`
   → "Set price" → Any quantity ₹410, from 100 → ₹395 → "Save price". Also set it on any
   tier lists that should get a better rate.
4. **Stock** (store keeper): receive the first delivery (Scenario C).
5. **Check on the store** ("View store"): enter a served PIN, search "acc gold": price
   "₹410.00 per BAG · excl. 28% GST", bulk rate, **In stock**, delivery date.

If it shows "Price on request", step 2 or 3 is missing. If "Not delivered to your PIN
code", see [Scenario K](#k-the-store-says-it-isnt-available).

---

## H. A price or GST change from a future date

**Supplier raises cement by ₹15 from 1 October:**

1. Sales › Price lists › Retail › "Open prices" → SKU → **"Change"**.
2. Enter the new slabs (₹415 / 100+ → ₹400). **"Starts"** `01-10-2026 00:00`. Reason
   "UltraTech revision letter 22 Sep".
3. **"Save price"** → "Price saved. It applies from 1 Oct…". The grid shows the current
   price and **"Next change: From 1 Oct"**.
4. Repeat for tier and contract lists as agreed.

Orders placed before 1 October keep the old price. Made a mistake in an upcoming price?
Save a corrected change that starts **one minute later** (e.g. 00:01); the later start wins.
Do not use the identical start time twice.

**GST rate change notified by government:** Sales › GST rates → "Change rate" on the HSN
→ new rate, "Starts" = effective date, "Source" = notification number → "Save rate".

---

## I. Special rates for contractors or one big customer

**A group of contractors (tier):**

1. Sales › Price lists › "New Price list": Name "Contractors", Code `CONTRACTORS`,
   Applies to **Tier** → "Create".
2. "Open prices" → set rates for the SKUs they buy. SKUs you leave unpriced fall back to
   Retail.
3. Sales › Customers → open each contractor → **"Price tier"** `Contractors` → "Save
   changes". Their next page load shows the tier prices.

**One customer with agreed rates (contract):**

1. "New Price list": Applies to **Contract**, choose the customer, Code `CON-SHAH-2026`.
2. Set only the agreed SKUs. For everything else they get their tier, else Retail.

To end a contract, deactivate the list; the customer falls back to tier or Retail.

---

## J. A customer order, start to finish

**The customer (on the store):**

1. Enters site PIN `411001`, finds UltraTech OPC 53, sees **In stock**, "Delivery by Sat 26
   Sep".
2. Adds 120 BAG → cart shows ₹380 per BAG (bulk rate), CGST 14 % + SGST 14 % (Maharashtra
   delivery), "Total payable".
3. Checkout: site address in Maharashtra, mobile **+91** `98765 43210`, optional GSTIN →
   **"Place order"** → `ORD-2627-000045`. The 120 bags are **held** for 48 hours.

**Staff (Sales › Orders › "Awaiting confirmation"):**

4. Open ORD-2627-000045. Note "Stock held" (e.g. all 120 bags at WH-PUNE-01 — an order
   always ships from one warehouse) and "Stock is held for this order until …".
5. **Call** the customer: confirm quantity, delivery slot, payment (cash on delivery).
6. **Customer changes their mind** → "Cancel this order", reason "Customer cancelled on
   call" → stock freed; the customer sees "Cancelled".
7. **Agreed** → on the order, **"Confirm order"**. It moves to the **Confirmed** tab, the
   stock stays held without expiry, and the customer sees "Confirmed". Deliver and invoice
   through your current process for now, then follow the interim stock step in
   [5.4](05-sales.md#daily-order-routine-until-the-rest-of-phase-4).
8. **Nobody confirms** → after 48 hours the order becomes **Expired** (customer sees
   "Closed") and the stock is freed. Work the list daily so this does not happen by accident.

**Delivery to another state** (e.g. site in Belagavi, Karnataka): the same order shows
**IGST 28 %** instead of CGST + SGST — worked out from the delivery address state.

---

## K. “The store says it isn't available”

Ask the customer for the **PIN code** and the **product**, then check in this order:

| Customer sees | Check | Fix |
| --- | --- | --- |
| "Not delivered to your PIN code" | Inventory › Delivery areas → search the PIN | Add the PIN to a warehouse ([Scenario L](#l-start-delivering-to-a-new-area)) |
| "Out of stock" | Stock → the SKU → Available at warehouses serving that PIN | Receive or transfer stock there |
| "Only 40 BAG can be delivered…" | The best single warehouse serving that PIN has 40 available (orders are never split across warehouses) | Offer 40 now, or transfer more into one warehouse |
| "These items cannot all be delivered together…" | Each item is available, but no single warehouse has all of them | Split into two orders, or transfer stock so one warehouse has everything |
| "Price on request" | Sales › GST rates card; the SKU in the customer's price list | Add GST rate / set price |
| "This item is no longer sold" | SKU Active in the catalogue | Re-activate by import if it should sell |
| "The store is not taking orders yet" | Administration › Business settings | Complete the settings |
| "Check delivery" | Customer has not entered a PIN | Ask them to enter the site PIN |

Remember: never tell the customer which warehouse has the stock.

---

## L. Start delivering to a new area

**Example:** start serving Hinjewadi (411057) from Pune in 1 day, and Satara PINs in 2 days.

1. Inventory › Delivery areas › **"Add PIN codes"**.
2. "Delivers from" `WH-PUNE-01`; "Delivery time (days)" `1`; PIN codes `411057`
   → "Add PIN codes".
3. Repeat with `2` days and the Satara list (paste up to 500 at once).
4. **Check:** on the store, enter 411057 → products show In stock / Out of stock (not "Not
   delivered").

A new city with a new warehouse: create the warehouse (4.2), receive stock, then add its
PIN codes.

---

## M. Block a customer

**When:** fraud, repeated fake orders, unpaid dues.

1. Sales › Customers → search by email → open.
2. Untick **"Can sign in and order"** → "Save changes" → "Customer blocked…".
3. They are signed out at once and cannot sign in. Cancel any of their open orders in
   Sales › Orders so the stock is freed.
4. To restore, tick the box again.

---

## N. Weekly controls

| Check | Where | Healthy result |
| --- | --- | --- |
| Ledger check | Inventory › Stock › Ledger check | "Every balance agrees with its ledger." |
| Orders waiting | Sales › Orders › Awaiting confirmation | Nothing older than 1 day |
| Orders to deliver | Sales › Orders › Confirmed | Each has a delivery day agreed |
| Expired orders | Sales › Orders › Expired | Each one understood (not missed calls) |
| Unsellable products | Sales › GST rates card | Empty |
| Transfers in transit | Inventory › Transfers | None older than the travel time |
| Access review | Administration › Users | Leavers deactivated; no unexpected "Everywhere" |
| Sensitive changes | Administration › Audit log (Users, Roles) | Every change expected |

---

## O. Find out who changed something

**Example:** "Why did Ramesh get Manage on Nashik stock?"

1. Administration › Audit log → filter **Users** → find the entry for Ramesh.
2. Open it: **Who**, **When**, **IP address**, **Device**, and the **Before / After** of
   the changed fields (e.g. `access`, `role_ids`, `scopes`).
3. **"Full history of this record"** shows every change to Ramesh's user record.

Prices, customers and orders are under **"All records"**. For stock, Stock › Movements
shows every movement with who posted it.
