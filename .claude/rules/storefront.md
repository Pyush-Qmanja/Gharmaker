---
paths:
  - "src/Platform.Web/Areas/Storefront/**/*.{cs,cshtml}"
  - "src/Platform.Api/Controllers/Storefront/**/*.cs"
  - "src/Platform.Shared/Dtos/Storefront/**/*.cs"
---

# Storefront — warehouse opacity (P1)

Everything under these paths is customer-facing. The customer learns the
quantity they can buy, the price, the tax and a delivery window. Nothing else.

Orders are never split (decision #2): the quantity offered is what the best-stocked
single serving warehouse holds, a cart ships from one warehouse that holds every
line, and a cart that no single warehouse can supply is refused with a message
that names no warehouse. Plan with `AllocationPlanner.PlanOrder` only.

## Never appears in a response under these paths

- `warehouseId`, `warehouseCode`, `warehouseName`, warehouse address, lat, lng
- Per-warehouse quantity splits, or any array whose length reveals the split
- `binId`, `batchId`, `batchCode`, `lotId`, `shadeCode`
- Internal lead times per location — only the resolved customer-facing window
- Allocation, pick list, or shipment-origin detail
- Cost price, margin, supplier name

This applies to error messages and validation text as well. An out-of-stock
message says the quantity is unavailable; it never says where it ran out.

## Contract rules

- Storefront DTOs live in `Platform.Shared/Dtos/Storefront/` and are separate types. Never
  return a domain entity or an internal DTO directly from a storefront endpoint.
- Availability is a single `decimal` plus a UOM code (P7), never a collection.
- Delivery is `{ earliestDate, latestDate }`, never a per-warehouse breakdown.
- Out-of-scope or not-yours returns **404, not 403** (P6). A 403 confirms the
  object exists.

## The opacity test suite

`tests/Platform.Tests.Opacity/` asserts that no storefront response contains a
forbidden field, under any role, by serialising every response and scanning it.

**Adding a storefront endpoint means adding it to that suite in the same commit.**
A new endpoint that is not covered is an incomplete change, not a follow-up.

## How it is built here

- API: `Controllers/Storefront` (base `StorefrontControllerBase`, which runs every action
  as the store's organisation), services in `Services/Storefront`, and one mapper,
  `Mapping/Storefront/ShopMapping`, the only code that builds storefront DTOs.
- Internal planning types (`PricedLine.Plan`, `WarehouseStock`, `AllocationPart`,
  `ServingWarehouse`) never cross into a DTO.
- Web: area `Shop` (`/shop`), client `IShopApiClient`; store pages never render a staff DTO.
- Tests: `tests/Platform.Tests.Opacity` walks every storefront type and endpoint by
  reflection (and pins the endpoint list); the API and UI suites scan every storefront
  response and store page for warehouse codes, names and ids.

## The one legal exception

A GST tax invoice must carry a seller address. If the business bills from one
registered address (see `docs/decisions-pending.md`), the invoice reveals
nothing. If it bills per warehouse, the invoice is the single permitted leak and
it is confined to the invoice document — never to an API field.
