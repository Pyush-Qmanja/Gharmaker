# Construction Materials Supply & Site Workforce Platform

Two halves on one platform: materials supply (warehouse-wise stock, brand-wise
catalogue, customer storefront) and construction site operations (level-wise
logins, teams, tasks, attendance, wages, wallets).

## Run it locally

Prerequisites: .NET 8 SDK, PostgreSQL 16 (or Docker).

```powershell
# 1. Database — either Docker…
docker compose up -d
#    …or a local PostgreSQL with database/user/password: platform / platform / platform_dev

# 2. Local settings (git-ignored) — then set a signing key and admin password
copy src\Platform.Api\appsettings.Development.example.json src\Platform.Api\appsettings.Development.json

# 3. Tools
dotnet tool restore

# 4. API — migrates and seeds the first organisation + admin on start (Development only)
dotnet run --project src\Platform.Api --launch-profile https      # https://localhost:7261/swagger

# 5. UI, in a second terminal
dotnet run --project src\Platform.Web --launch-profile https      # https://localhost:7031
```

Sign in with the `Seed:AdminEmail` / `Seed:AdminPassword` from step 2.

New migration after changing an entity:

```powershell
dotnet ef migrations add <Name> --project src\Platform.Api --output-dir Data\Migrations
```

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
    Repositories/            IRepository<> (generic), IUnitOfWork
    Mapping/                 IEntityMapper<> + per-entity mappers
    Data/                    AppDbContext (audit + tenancy), configurations, migrations
    Security/                JWT issuing
  Platform.Web/              MVC UI
    Controllers/             CrudController<> + thin per-resource controllers
    Services/Api/            IApiClient, ICrudApiClient<>, BearerTokenHandler
    TagHelpers/              <form-field> — label + input + error in one tag
    Views/Shared/            CrudIndex, CrudForm and shared partials
    Views/<Resource>/        only _Table.cshtml and _Form.cshtml
    wwwroot/css/             variables.css (design tokens), site.css (components)
    wwwroot/js/              site.js (behaviour via data- attributes)
```

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
  adr/0001-database.md       why PostgreSQL and not Firestore
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
