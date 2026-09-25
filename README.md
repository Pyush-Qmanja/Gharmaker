# Construction Materials Supply & Site Workforce Platform

Two halves on one platform: materials supply (warehouse-wise stock, brand-wise
catalogue, customer storefront) and construction site operations (level-wise
logins, teams, tasks, attendance, wages, wallets).

**How to use it:** the SOP in [`docs/sop/`](docs/sop/README.md) covers every screen,
step by step, with real-life scenarios.

## Run it locally

Prerequisites: .NET 8 SDK and a Firebase project with a Firestore database and
Authentication enabled (Email/Password provider).
Database choice: `docs/adr/0002-firestore.md`.

```powershell
# 1. Local settings (git-ignored). Set Firebase:ProjectId, Firebase:CredentialsPath
#    (service-account JSON kept OUTSIDE the repo), Firebase:WebApiKey, Jwt:SigningKey
#    and the Seed admin. The seed admin is created in Firebase Authentication too.
copy src\Platform.Api\appsettings.Development.example.json src\Platform.Api\appsettings.Development.json

# 2. API — seeds the first organisation + admin on first start (Development only)
dotnet run --project src\Platform.Api --launch-profile https      # https://localhost:7261/swagger

# 3. UI, in a second terminal
dotnet run --project src\Platform.Web --launch-profile https      # https://localhost:7031
```

Sign in with the `Seed:AdminEmail` / `Seed:AdminPassword` from step 1.

Deploy security rules and indexes after changing `firestore.rules` or
`firestore.indexes.json`:

```powershell
firebase deploy --only firestore:rules,firestore:indexes --project <project-id>
```

### Optional: offline emulator (needs Java)

The Firebase CLI's Firestore emulator lets you work without touching a real
project. It is the only part of the setup that needs a Java runtime.

```powershell
firebase emulators:start --only firestore,auth --project demo-platform
# then run the API with: Firebase__ProjectId=demo-platform
#   Firebase__FirestoreEmulatorHost=127.0.0.1:8080  Firebase__AuthEmulatorHost=127.0.0.1:9099
```

The emulator does not enforce composite indexes; a missing index only fails
against the real project.

## Solution

```
Platform.sln
Directory.Build.props        shared build settings; missing doc comment = build error
src/
  Platform.Shared/           class library
    Entities/                BaseEntity (Id + audit fields), IOrgScoped, ISoftDeletable, per-module entities
    Dtos/                    EntityDto, PagedRequest/PagedResult, per-module DTOs
    Validation/              FluentValidation rules, reusable RuleBuilderExtensions
    Constants/               FieldLengths, ApiRoutes, ClaimNames, Paging
  Platform.Api/              Web API
    Controllers/             CrudControllerBase<> + thin per-resource controllers
    Services/                CrudService<> + per-module services, AuthService
    Repositories/            IRepository<> (generic, org-scoped), UnitOfWork (batch or transaction, audit fields + audit log)
    Mapping/                 IEntityMapper<> + per-entity mappers
    Firestore/               FirestoreContext (one client), naming, DocumentConverter, seeder
    Security/                JWT issuing
  Platform.Web/              MVC UI
    Controllers/             CrudController<> + thin per-resource controllers
    Services/Api/            IApiClient, ICrudApiClient<>, BearerTokenHandler
    TagHelpers/              <form-field>, <checkbox-list>, <form-panel>, <icon>
    Views/Shared/            CrudIndex, CrudForm and shared partials
    Views/<Resource>/        only _Table.cshtml and _Form.cshtml
    wwwroot/css/             variables.css (tokens), base, layout, components, pages
    wwwroot/img/icons.svg    icon sprite used by <icon>
    wwwroot/js/              site.js (behaviour via data- attributes)
```

## What works today

- Sign-in (Firebase Authentication), users, roles, per-feature access with its own places,
  and an audit log of every change.
- Catalogue: brands, units with conversions, categories and products, Excel import.
- Warehouses and stock: goods receipts, two-step transfers, adjustments, opening stock
  import, reversals, movement history, and a ledger check that proves every balance.
- Stock where people look for it: on each product page (per variant and per warehouse)
  and from each warehouse ("View stock").
- Selling: price lists (retail, tier, contract) with quantity slabs and dated prices,
  GST rates by HSN code, delivery areas (PIN codes per warehouse), customers and orders.
- The online store at `/shop`: browse and search, availability and delivery date for
  your PIN code, cart in any unit, GST shown as it will be invoiced, checkout that holds
  stock, my orders and cancel. Customers never see which warehouse serves them.

Tests: `dotnet test` runs the unit tests (GST, slabs, allocation, dates) and the
storefront opacity tests.

## Using this repo with Claude Code

`CLAUDE.md` loads into every session. Files in `.claude/rules/` either load
every session (no `paths:` header) or only when Claude touches matching paths.

```
.claude/rules/
  coding-standards.md        always  — structure, comments, reuse, UI, add-a-module recipe
  field-names.md             always  — one name per concept, platform-wide
  storefront.md              paths   — warehouse opacity (P1)
  ledgers.md                 paths   — stock and money (P2, P3, P4, P9)
  mobile-offline.md          paths   — offline sync (P5)
  permissions.md             paths   — capability + scope (P6)
docs/
  blueprint.md               schema, contexts, calculations, state machines
  roadmap.md                 nine phases and their gates
  decisions-pending.md       open questions — close before Phase 0 ends
  adr/0001-database.md       PostgreSQL decision (superseded)
  adr/0002-firestore.md       Firestore only — how conventions map, accepted costs
  sop/                       user manual: every screen, scenarios, troubleshooting
firebase.json, firestore.rules, firestore.indexes.json
```

Good prompts:

```
Add a Category module to Catalog, following the recipe in coding-standards.md.

Design the Identity capability + scope model from docs/blueprint.md, per P6.

Write IUomConversionService with tests covering every material family in
the units table in docs/blueprint.md.
```

Use `/plan` before anything that spans modules. Ask Claude to add to
`CLAUDE.md` or a rules file whenever a convention gets settled, so the next
session inherits it.
