# 3. Catalogue

[← SOP index](README.md)

Menu group **Catalogue**: Products, Brands, Units.

**Words used:** a **product** is what you sell (e.g. "UltraTech OPC 53 Cement"); a **SKU**
is one variant of it with its own code (e.g. `CEM-ULT-OPC53-50KG`, "50 kg bag"); the
**base unit** is the unit a SKU's stock is counted in (e.g. BAG).

## 3.1 Products

**Menu:** Catalogue › Products. Catalogue View to browse; Catalogue Manage to import.
This is the internal product master — customers never see this page.

**Browse**

- **Categories** tree on the left ("All products" at the top). A category includes its
  sub-categories.
- **Search** (e.g. `tiscon 12`) matches every word against name, brand, category, SKU
  codes and variants. **"All brands"** narrows by brand.
- Columns: Product (with brand), Category, Variants, HSN.

**Product page** (click a product)

- **Variants** — SKU code, variant, base unit, "Its own conversions" (e.g. "1 TONNE =
  20 BAG"), "Can be expressed in", **On hand** (with Stock access), status.
- **"Where it is stocked"** — On hand, Reserved, Available per warehouse (Stock View).
- **"Unit converter"** — pick Variant, Amount, From, To → **"Convert"**.

**Products and SKUs are added or changed only by Excel import.** There is no single
"new product" form and no category screen.

## 3.2 Excel import

**Products › "Import from Excel"** (Catalogue Manage).

1. **"Download Excel template"**. It has three sheets: **Catalogue** (headers + examples),
   **Units** (every valid unit code), **How to**.
2. Fill **one row per SKU**:

   | Column | Required | Rule |
   | --- | --- | --- |
   | Category | Yes | Levels joined by `>`: `Steel > TMT Bars`. Missing ones are created. |
   | Brand | Yes | Created if new. |
   | Product | Yes | Rows with the same Brand + Product form one product. |
   | HSN | Yes | 4, 6 or 8 digits. All rows of one product must match. |
   | SKU Code | Yes | Unique; letters, digits, single hyphens. **An existing code is updated.** |
   | Variant | Yes | e.g. `12 mm`, `50 kg bag`. |
   | Base Unit | Yes | A code from the Units sheet. |
   | Conversions | No | `TONNE=20; TRUCK=400` = 1 TONNE is 20 base units. |
   | Active | No | Yes / No. **Blank means Yes.** |

3. Upload the file (.xlsx or .csv, up to 5000 rows, 10 MB) → **"Upload and check"**.
   Nothing is saved yet.
4. Read **"Check before saving"**: counts of New SKUs, SKUs updated, New products, Rows
   with errors; each row marked **New / Update / Error** with notes (e.g. "Base unit
   changes from BAG to KG.").
5. If any row has errors, **nothing can be saved** — fix those rows in the file and
   **"Upload a different file"**.
6. When clean, **"Confirm and save"** within **30 minutes** → "Import saved: …".

**Take care**

- A product is identified by **brand + product name**. Renaming a product in the sheet
  creates a *new* product; moving an existing SKU code to it fails with "SKU code … already
  belongs to another product."
- Category spelling matters: "Steel > TMT Bar" and "Steel > TMT Bars" are two categories.
  There is no rename.
- Re-importing an old sheet with **Active blank re-activates** SKUs that were switched off.
- **Do not change the base unit of a SKU that already has stock** — later stock postings
  for it will be refused. Ask the development team to correct it instead.
- Keep your master sheet; the next import is easiest as "edit the sheet, upload again".

## 3.3 Brands

**Menu:** Catalogue › Brands. View / Manage.

**"New Brand"** → "Name", **"Slug"** (lower-case, hyphens, e.g. `ultra-tech`; type it
yourself), "Manufacturer", "Logo URL", "Display order" (lower first) → "Create".
Brands created by import get a slug automatically. Deactivate hides the brand from new
use; it does not check which products carry it.

## 3.4 Units

**Menu:** Catalogue › Units. Catalogue View / Manage.

Units are shared by all products. **"New Unit"** → "Code" (e.g. `BAG`), "Name",
"Measures" (Count / Mass / Volume / Area / Length), **"Standard size"** — how many of the
reference unit one of this is (e.g. TONNE = 1000 KG). Leave it empty for packs that
depend on the product (BAG, BOX, BUNDLE).

**Built-in units**

| Measures | Reference | Fixed size | Size depends on product |
| --- | --- | --- | --- |
| Count | PCS | THOUSAND = 1000 | BAG, BOX, BUNDLE, CAN, BUCKET, TRUCK |
| Mass | KG | QUINTAL = 100, TONNE = 1000 | — |
| Volume | CFT | BRASS = 100, CUM = 35.3147, LTR = 0.0353147 | — |
| Area | SQFT | SQM = 10.7639 | — |
| Length | M | RMT = 1, FT = 0.3048 | — |

**How conversions work:** a quantity can be entered in any unit the SKU can reach —
its base unit, its own conversions (from the import), or any unit of the same kind with a
standard size (KG ↔ TONNE), even chained (KG → TONNE → BAG). Results are kept to 4
decimals.

**Take care:** deactivating a unit or changing its standard size affects **every**
product using it, at once.
