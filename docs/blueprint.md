# Blueprint — condensed reference

Working reference for the build. The full narrative blueprint (personas, risk
register, effort estimate, client summary) lives in the Claude doc; this file
carries what a coding session needs and is the version of record for schema.

## Bounded contexts

| Context | Owns | Publishes | Consumes |
| --- | --- | --- | --- |
| Identity | users, roles, permissions, scopes | user events | — |
| Catalog | brands, categories, products, skus, attributes, uoms | sku changed | — |
| Inventory | warehouses, bins, batches, stock ledger, reservations | stock changed, low stock | order allocated |
| Pricing | price lists, slabs, tax rates, freight rules | price changed | sku changed |
| Orders | orders, allocations, shipments, invoices, returns | order placed, order shipped | stock changed, price changed |
| Projects | sites, wbs, tasks, work orders | task changed | — |
| Workforce | teams, members, attendance, wage structures | attendance approved | task changed |
| Wallet | wallet ledger, advances, payout runs | payout completed | attendance approved |
| Site materials | indents, site GRN, site issues | indent raised | order shipped |

## The three chains

```
Sell    brand -> sku -> price -> cart -> order -> allocation -> shipment -> invoice
Stock   warehouse -> stock ledger -> allocation -> shipment
Pay     site -> team -> member -> attendance -> wallet -> payout
Bridge  site -> indent -> order -> shipment -> site GRN -> issue -> task
```

The bridge is the only path crossing the seam between the halves.

## Schema

### Identity
`organisations` · `users` · `roles` · `permissions` · `role_permissions` ·
`user_roles` · `user_scopes` · `customers` · `customer_addresses` · `sessions` ·
`audit_log` · `otp_requests`

Scope types: `global` `warehouse` `warehouse_group` `site` `project` `customer`

### Catalog
`brands(id, name, slug, logo_url, manufacturer_name, is_active, display_order)`
`categories(id, parent_id, name, slug, display_order)`
`products(id, brand_id, category_id, name, slug, hsn_code, is_active)`
`skus(id, product_id, code, variant_label, base_uom, weight_kg, volume_cft)`
`sku_attributes` · `attributes` · `uoms` · `uom_conversions` · `sku_media` ·
`sku_packs` · `substitutes`

Brand is a first-class entity, not a column. Brand-wise display and brand-wise
stocking both fall out of that.

### Inventory
`warehouses(id, org_id, code, name, type, address, lat, lng, owner_user_id)`
`warehouse_zones` · `bins`
`batches(id, sku_id, batch_code, manufactured_on, expires_on, shade_code, supplier_lot)`
`stock_ledger_entries(id, warehouse_id, sku_id, batch_id, bin_id, quantity, uom, direction, reason_code, ref_type, ref_id, at, by)`
`stock_balances(warehouse_id, sku_id, batch_id, on_hand, reserved, available, updated_at)`
`reservations(id, lot_id, sku_id, quantity, uom, cart_id, order_id, status, expires_at)`
`serviceability(id, warehouse_id, pincode, lead_time_days, is_active)`
`stock_transfers` · `stock_adjustments` · `stock_audits` · `reorder_rules` ·
`suppliers` · `purchase_orders` · `goods_receipts`

### Pricing
`price_lists(id, name, currency, tier_id, valid_from, valid_to, is_active)`
`price_list_lines(id, price_list_id, sku_id, uom, unit_price, min_qty, max_qty)`
`price_tiers` · `tax_rates(hsn_code, rate_percent, cess_percent, valid_from, valid_to)`
`freight_rules` · `freight_zones` · `charges` · `promotions`

Resolution order, first match wins: customer contract price -> tier price list
honouring the quantity slab -> default retail list -> line is not sellable and
is blocked at the cart. Never default to zero.

### Orders
`carts` · `cart_lines` · `orders` · `order_lines` · `allocations` · `shipments` ·
`shipment_lines` · `invoices` · `payments` · `credit_notes` · `returns` ·
`delivery_proofs` · `vehicles`

Order status is derived where it can be. Do not invent a stored
`partially_dispatched` state — derive it from shipment statuses.

### Projects and workforce
`projects` · `sites(id, project_id, code, name, lat, lng, geofence_radius_m, manager_user_id)`
`wbs_nodes` · `tasks` · `work_types` · `task_assignments` · `progress_entries` ·
`measurements` · `site_documents` · `site_issues`
`teams` · `members` · `team_members` · `wage_structures` · `wage_bands`
`attendance(id, member_id, site_id, team_id, task_id, date, status, in_time, out_time, overtime_hours, lat, lng, device_id, client_id, marked_by, approved_by, sync_state)`
`attendance_adjustments` · `leave_records`
`wallets` · `wallet_entries` · `advances` · `payout_runs` · `payout_lines`

`attendance` is the fastest-growing table. Partition by month from the start.

## Key calculations

Available to promise, for a SKU and a customer pincode. Orders are never split
across warehouses (decision #2), so it is the best single place, not a sum:

```
ATP = MAX over serviceable warehouses of (on_hand - reserved - blocked)
```

minus a configurable safety buffer per SKU. A whole order ships from the one
serviceable warehouse that holds every line — the fastest, then the fullest —
and its delivery date is that warehouse's lead time.

Site progress, weighted by task budget:

```
Progress(node) = SUM(completed_qty * weight) / SUM(planned_qty * weight)
```

## Units of measure

One conversion service owns every conversion. Pricing, stock, freight,
invoicing and reporting call it; none of them does its own arithmetic.

| Material | Base unit | Also sold as | Conversion |
| --- | --- | --- | --- |
| Cement | bag (50 kg) | tonne, truck load | global, 20 bags/tonne |
| TMT steel | kilogram | 12 m piece, bundle, tonne | SKU-specific by diameter |
| Bricks | piece | thousand, truck load | global |
| Sand, aggregate | cubic foot | tonne, trolley, tipper | SKU-specific by density |
| Tiles | box | sqft, piece | SKU-specific by size |
| Pipes | running metre | piece, bundle | SKU-specific |
| Paint | litre | 20 L bucket, 4 L can | global per pack |

## State machines

**Order** Draft -> Placed -> (PaymentPending) -> Confirmed -> Allocated ->
Picking -> ReadyToDispatch -> Dispatched -> Delivered -> Closed.
Cancel from Confirmed or Allocated. Return from Delivered within the window.

**Reservation** Held -> Confirmed -> Allocated -> Consumed.
Held -> Expired or Released. Allocated -> Released on cancellation.

**Task** Planned -> Assigned -> InProgress -> MeasurementPending -> Completed.
OnHold from InProgress. Measurement rejection returns to InProgress.

**Payout run** Draft -> Calculated -> PendingApproval -> Approved -> Executed ->
Closed. PendingApproval can return to Draft with remarks.

## Wage bases

| Basis | Trigger | Formula |
| --- | --- | --- |
| Day rate | attendance approved | days present x rate, plus OT hours x OT rate |
| Monthly | month-end cycle | monthly rate less unpaid leave x (rate / working days) |
| Piece rate | measurement approved | measured qty x rate, split across team weighted by days present |
