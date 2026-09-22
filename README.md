# Construction Materials Supply & Site Workforce Platform

Two halves on one platform: materials supply (warehouse-wise stock, brand-wise
catalogue, customer storefront) and construction site operations (level-wise
logins, teams, tasks, attendance, wages, wallets).

## Using this repo with Claude Code

`CLAUDE.md` loads automatically into every Claude Code session in this folder.
The files in `.claude/rules/` load only when Claude touches matching paths, so
the storefront rules arrive when it edits a storefront file and stay out of
context otherwise.

```bash
cd construction-platform
git init && git add -A && git commit -m "Project foundation"
claude
```

Good opening prompts:

```
Read docs/blueprint.md and docs/roadmap.md, then scaffold the Phase 1
solution structure: Platform.Api, Platform.Domain, Platform.Infrastructure,
and a test project. Don't write features yet.

Design the EF Core entities and migration for the Identity context from
docs/blueprint.md. Capability plus scope, per P6.

Write IUomConversionService with tests covering every material family in
the units table in docs/blueprint.md.
```

Use `/plan` before anything that spans modules — it drafts an approach for
approval before writing code. Ask Claude to add to `CLAUDE.md` whenever a
convention gets settled in conversation, so the next session inherits it.

## Layout

```
CLAUDE.md                    loads every session — the ten rules and conventions
.claude/rules/
  storefront.md              warehouse opacity (P1)       — storefront paths
  ledgers.md                 stock and money (P2,P3,P4,P9) — inventory, wallet
  mobile-offline.md          offline sync (P5)            — MAUI, sync endpoints
  permissions.md             capability + scope (P6)      — controllers, identity
docs/
  blueprint.md               schema, contexts, calculations, state machines
  roadmap.md                 nine phases and their gates
  decisions-pending.md       open questions — close before Phase 0 ends
  adr/0001-database.md       why PostgreSQL and not Firestore
```

The rules files are written to be enforceable. If a change breaks one, the
change is wrong even if it compiles.

## Planned solution structure

```
src/
  Platform.Api/                 .NET 8 Web API
  Platform.Domain/              entities, rules, no infrastructure
  Platform.Infrastructure/      EF Core, Redis, S3, external services
  Platform.Shared/              validation shared with MAUI
  Platform.Storefront/          MVC — customer
  Platform.Admin/               MVC — admin
  Platform.Warehouse/           MVC — warehouse
  Platform.Mobile.Supervisor/   MAUI
  Platform.Mobile.Driver/       MAUI
tests/
  Platform.Tests.Unit/
  Platform.Tests.Integration/
  Platform.Tests.Opacity/       asserts P1 on every storefront response
```
