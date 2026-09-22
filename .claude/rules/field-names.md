# Field names — loads every session

**One concept, one name, everywhere.** A field that means the same thing is
spelled the same in every entity, DTO, request, view model, view, JSON body,
and database column. Never invent a synonym.

C# uses PascalCase; the Firestore field is the snake_case of the same name
(automatic, via `FirestoreNaming.Field`) and the collection is its plural
(`Brand` → `brands`). JSON is camelCase of the same name (automatic). So
choosing the C# name decides all of them.

## Canonical names

| Concept | C# property | Firestore field | Type | Never use |
| --- | --- | --- | --- | --- |
| Primary key | `Id` | document id (not a field) | `Guid` (UUID v7 via `IdGenerator`) | `BrandId` as own key, `Key`, `Code` as PK, `int` ids |
| Reference to another row | `<Entity>Id` | `<entity>_id` | `Guid` | `BrandRef`, `FkBrand`, `Brand_Id` |
| Organisation / tenant | `OrgId` | `org_id` | `Guid` | `OrganisationId`, `TenantId`, `CompanyId` |
| When created | `CreatedAt` | `created_at` | `DateTime` (UTC) | `CreatedOn`, `CreatedDate`, `DateCreated`, `InsertedAt`, `AddedOn` |
| Who created | `CreatedBy` | `created_by` | `Guid?` (user id) | `CreatedByUser`, `Creator`, `AddedBy`, `OwnerId` |
| When last changed | `UpdatedAt` | `updated_at` | `DateTime?` (UTC) | `UpdatedOn`, `ModifiedOn`, `ModifiedAt`, `LastModified`, `ChangedAt`, `EditedOn` |
| Who last changed | `UpdatedBy` | `updated_by` | `Guid?` (user id) | `ModifiedBy`, `ChangedBy`, `EditedBy`, `LastModifiedBy` |
| Active / soft-deleted | `IsActive` | `is_active` | `bool` | `IsDeleted`, `Deleted`, `Status` (for this), `Enabled`, `Active` |
| Display name | `Name` | `name` | `string` | `Title`, `FullName`, `DisplayName`, `BrandName`, `<Entity>Name` on its own entity |
| URL identifier | `Slug` | `slug` | `string` | `UrlKey`, `Handle`, `Permalink` |
| Business code | `Code` | `code` | `string` | `ShortCode`, `RefCode`, `<Entity>Code` on its own entity |
| Sort position | `DisplayOrder` | `display_order` | `int` | `SortOrder`, `Sequence`, `Position`, `Rank` |
| Email | `Email` | `email` | `string` | `EmailAddress`, `Mail`, `EmailId` |
| Phone | `Phone` | `phone` | `string` (E.164) | `Mobile`, `PhoneNumber`, `ContactNo` |
| Image / file link | `<Thing>Url` | `<thing>_url` | `string` | `<Thing>Link`, `<Thing>Path`, `<Thing>Uri` |
| Free-text notes | `Remarks` | `remarks` | `string?` | `Notes`, `Comment`, `Description` (for notes) |
| Human reference no. | `ReferenceNo` | `reference_no` | `string` | `RefNo`, `Number`, `DocNo` |
| Money amount | `<Thing>Amount` + `Currency` | `<thing>_amount`, `currency` | `decimal(18,4)` + `string` (ISO 4217) | bare number, `float`, `double`, `CurrencyCode`, `Curr` |
| Unit price | `UnitPrice` | `unit_price` | `decimal(18,4)` | `Rate`, `PricePerUnit` |
| Quantity | `Quantity` + `Uom` | `quantity`, `uom` | `decimal(18,4)` + `string` (UOM code) | `Qty`, `Count`, `UomCode`, `Unit`, bare number (P7) |
| Validity window (P8) | `ValidFrom` / `ValidTo` | `valid_from` / `valid_to` | `DateTime` (UTC) | `StartDate`/`EndDate`, `EffectiveFrom`/`EffectiveTo` |
| Other point in time | `<Event>At` | `<event>_at` | `DateTime` (UTC instant) | `<Event>On`, `<Event>Date`, `<Event>Time` |
| Calendar date (no time) | `<Event>On` | `<event>_on` | `DateOnly` | `<Event>Date`, `<Event>At` for a date-only value |
| Other boolean | `Is<State>` / `Has<Thing>` | `is_<state>` | `bool` | `<State>Flag`, `<State>` bare |
| Device time (P5) | `DeviceTime` | `device_time` | `DateTime` | `ClientTime`, `LocalTime` |
| Client idempotency id (P5) | `ClientId` | `client_id` | `Guid` | `LocalId`, `DeviceRecordId`, `Uuid` |

Rule of thumb: **instants end in `At`, calendar dates end in `On`, actors end
in `By`, booleans start with `Is`/`Has`, references end in `Id`.** So a record's
last-change time is always `UpdatedAt` — `UpdatedOn` would mean a date with no
time, which is never what an audit field is.

Blueprint names that break this table are corrected when the table is built:
`stock_ledger_entries(at, by)` become `created_at`, `created_by` (the entry is
append-only, so creation time is the movement time).

## How the audit fields get set

`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` and `OrgId` are declared
once on `BaseEntity` / `IOrgScoped` and stamped by `UnitOfWork.SaveChangesAsync` on commit.
Never declare them again on an entity, and never assign them in a service,
mapper or controller. Read DTOs inherit them from `EntityDto`.

## Adding a new concept

If a new concept is not in the table, add a row here **in the same change**
that introduces it, so the next module reuses the name instead of inventing one.
