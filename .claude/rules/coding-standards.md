# Coding standards — loads every session

These apply to every file in `src/`. A change that breaks one is wrong even if
it compiles.

## The three projects

| Project | Holds | Must not hold |
| --- | --- | --- |
| `Platform.Shared` (class library) | Entities, DTOs, FluentValidation validators, constants (`FieldLengths`, `ApiRoutes`, `ClaimNames`, `Paging`), `IdGenerator` | Firestore SDK, ASP.NET, HTTP, anything with I/O |
| `Platform.Api` (Web API) | Controllers, services, mappers, repositories, Firestore data layer (`Firestore/`), JWT | Razor, UI concerns |
| `Platform.Web` (MVC UI) | Controllers, views, view models, API client, `wwwroot` | Firestore SDK, direct DB access. It talks **only** to `Platform.Api` |

Dependencies point one way: `Api → Shared`, `Web → Shared`. `Web` never
references `Api`.

## Comments on every function

- Every public/protected type, method, property and constructor has an XML doc
  comment (`/// <summary>`, plus `<param>` and `<returns>`). The build enforces
  this: **CS1591 is an error** (`Directory.Build.props`). Do not suppress it.
- Private helpers get a `/// <summary>` too — the build can't enforce that, you must.
- Views and partials start with a one-line `@* what this renders *@` comment.
- JS functions get JSDoc. CSS sections get a header comment.
- Comments say *why* and *what for*, not a restatement of the code.

## No repeated code — reuse the generic pieces

Before writing anything, check whether one of these already does it:

| Need | Use | Where |
| --- | --- | --- |
| Data access — the ONE way to read/write Firestore | `IRepository<T>` (open generic, auto-registered, org-scoped) | `Api/Repositories` |
| Commit (atomic batch, stamps audit fields) | `IUnitOfWork` | `Api/Repositories` |
| Collection / field name | `FirestoreNaming.Collection<T>()`, `FirestoreNaming.Field(nameof(X.Prop))` — never a literal string | `Api/Firestore` |
| Value in a query filter | `DocumentConverter.ToFirestoreValue(value)` | `Api/Firestore` |
| Unique value check | `RequireUniqueAsync(...)` inside `EnsureUniqueAsync` | `Api/Services/CrudService` |
| List/get/create/update/deactivate | derive from `CrudService<...>` | `Api/Services` |
| REST endpoints | derive from `CrudControllerBase<...>` | `Api/Controllers` |
| Paging a query | `Repository.GetPagedAsync(query, request)` | `Api/Repositories` |
| Audit fields on DTO | `.WithAuditFrom(entity)` | `Api/Mapping/DtoMapping` |
| Validation rules | `ValidName()`, `ValidSlug()`, `ValidEmail()`, `ValidOptionalUrl()`... | `Shared/Validation/Common/RuleBuilderExtensions` |
| String lengths | `FieldLengths.*` — never a literal number | `Shared/Constants` |
| API routes | `ApiRoutes.*` — never a literal path | `Shared/Constants` |
| Calling the API from UI | `ICrudApiClient<...>` / `IApiClient` | `Web/Services/Api` |
| CRUD screens | derive from `CrudController<...>` | `Web/Controllers` |
| Form field markup | `<form-field asp-for="X" />` | `Web/TagHelpers` |
| List/form page layout | shared `CrudIndex` / `CrudForm` views | `Web/Views/Shared` |
| Status, row actions, pager, search, empty state | `_StatusBadge`, `_RowActions`, `_Pagination`, `_SearchBar`, `_EmptyState` | `Web/Views/Shared` |
| Show a date | `.ToIstString()` | `Web/Extensions/DateTimeExtensions` |
| Errors from API | throw `NotFoundException` / `BusinessRuleException` | `Api/Common/Exceptions` |

If you find yourself copying a block, extract it into one of these (or a new
generic helper next to them) instead.

## UI: no inline style, script or CSS

- No `style="..."` attributes, no `<style>` blocks, no `<script>` blocks, no
  `onclick=` or other inline handlers in any `.cshtml`.
- No scoped `*.cshtml.css` files.
- Colours, spacing, radii, font sizes: **only** as variables in
  `wwwroot/css/variables.css`. `site.css` uses `var(--...)`, never a raw hex.
- Component classes live in `wwwroot/css/site.css`, BEM-named (`block__element--modifier`).
- Behaviour lives in `wwwroot/js/site.js` and is opted into with `data-`
  attributes (e.g. `data-confirm="..."`, `data-auto-dismiss`).
- No CDN links; libraries go under `wwwroot/lib`.

## SOLID, concretely

- **S** — a controller only translates HTTP; a service holds the rule; a mapper
  only maps; a repository only queries. No business rule in a controller or view.
- **O** — extend by deriving (`CrudService`, `CrudController`) and overriding
  the virtual hooks (`ApplySearch`, `ApplyOrder`), not by editing the base.
- **L** — a derived service/controller must honour the base contract
  (same status codes, same paging behaviour).
- **I** — small interfaces (`IBrandFields`, `IActivatableRequest`, `IOrgScoped`,
  `ISoftDeletable`) over wide ones.
- **D** — depend on interfaces (`IRepository<T>`, `IUnitOfWork`, `IApiClient`,
  `ICurrentUser`, `TimeProvider`), never on `new` of a service or on `DateTime.UtcNow`.

## Security

- Passwords live only in **Firebase Authentication**. `/api/auth/login` asks
  Firebase (via `IIdentityProvider`) to verify them, finds the platform user by
  `auth_uid`, and issues the API's own JWT. Never store a password or hash in Firestore.
- The UI keeps the JWT in its encrypted HttpOnly cookie and forwards it via `BearerTokenHandler`.
- Every API controller is `[Authorize]` by default (via `CrudControllerBase`).
  `[AllowAnonymous]` only on sign-in.
- Never check a role name (P6, `permissions.md`). Capability + scope arrives in Phase 1.
- Tenancy is automatic: `Repository<T>` filters every `IOrgScoped` query and
  lookup by the caller's `OrgId`; `UnitOfWork` stamps it on insert. Reading
  Firestore without the repository (`IFirestoreContext` directly) is allowed
  **only** in sign-in and seeding.
- Firestore is reached only by the API (Admin SDK). `firestore.rules` denies all
  client access; never loosen it.
- The service-account key file lives outside the repo; its path goes only in
  git-ignored `appsettings.Development.json` (`Firestore:CredentialsPath`).
- Secrets never go in `appsettings.json`. Local values go in the git-ignored
  `appsettings.Development.json` (copy `appsettings.Development.example.json`).

## Recipe — adding a new CRUD module (e.g. `Category` in `Catalog`)

Use the same module folder name in every layer.

1. **Shared/Entities/Catalog/Category.cs** — derive from `BaseEntity`; add
   `IOrgScoped` (business data) and `ISoftDeletable` (master data). Use only
   field names from `field-names.md`.
2. **Shared/Dtos/Catalog/** — `CategoryDto : EntityDto`; an `ICategoryFields`
   interface; `CreateCategoryRequest`, `UpdateCategoryRequest`
   (+ `IActivatableRequest` if it can be restored).
3. **Shared/Validation/Catalog/** — one internal `CategoryFieldsValidator`
   built from `RuleBuilderExtensions`; create/update validators `Include` it.
4. **Shared/Constants/ApiRoutes.cs** — add `Categories = "api/categories"`.
5. **No schema or configuration step.** The collection (`categories`) and its
   fields are derived from the class by `FirestoreNaming` / `DocumentConverter`.
6. **Api/Mapping/Catalog/CategoryMapper.cs** — implement `IEntityMapper<...>`.
7. **Api/Services/Catalog/CategoryService.cs** — derive from `CrudService<...>`;
   override `ApplySearch` / `ApplyOrder` only if needed.
8. **Api/Controllers/Catalog/CategoriesController.cs** — derive from
   `CrudControllerBase<...>`, `[Route(ApiRoutes.Categories)]`. Usually empty.
9. **Api/Extensions/ServiceCollectionExtensions.cs** — one `AddCrudModule<...>()` line.
10. **Indexes** — add a composite index to `firestore.indexes.json` for every
    ordering in `ApplyOrder` / `ApplySearch` (org_id first). Deploy with
    `firebase deploy --only firestore:indexes`. Uniqueness goes in `EnsureUniqueAsync`.
11. **Web/Controllers/CategoriesController.cs** — derive from `CrudController<...>`;
    set names, implement `ToUpdateRequest`.
12. **Web/Views/Categories/** — `_Table.cshtml` and `_Form.cshtml` only.
13. **Web/Extensions/ServiceCollectionExtensions.cs** — one `AddCrudApiClient<...>(ApiRoutes.Categories)` line.
14. Add the nav link in `_Layout.cshtml` and a tile in `Home/Index.cshtml`.
15. `dotnet build` must pass with **0 warnings**.
