---
paths:
  - "src/Platform.Api/Controllers/**/*.cs"
  - "src/Platform.Api/Security/**/*.cs"
  - "src/Platform.Api/Services/Auth/**/*.cs"
  - "src/Platform.Shared/Entities/Identity/**/*.cs"
  - "src/Platform.Web/Controllers/**/*.cs"
---

# Permissions — capability plus scope (P6)

Every authorisation decision is two questions, always both:

1. Does this user's role hold this **capability**?
2. Is this object inside this user's **scope**?

## How it is built here

- Features: `Platform.Shared/Constants/Features.cs` — each feature is a view/manage
  pair of capability codes (`Capabilities.cs`) and says whether it is limited by
  scope (e.g. warehouses). The UI access grid is built from it.
- A user gets capabilities two ways, each with its own places:
  - **Roles** (`RoleIds`) apply in the user's `Scopes`.
  - **Feature access** (`Access`: `Feature` + `Level` + `Scopes`) is given straight
    to the user; a scoped feature applies only in that row's `Scopes`, an
    organisation-wide feature applies everywhere. Manage includes View.
- API guard: `[CrudCapabilities(view, manage)]` on every CRUD controller (startup
  fails without it) or `[RequiresCapability(code)]` on other endpoints.
- `IPermissionService` loads the user and their roles **per request** — nothing
  about permissions is in the JWT — so revoking a role, scope or user applies at
  once. Scope is always asked **per capability**: `CoversAsync(capability, scopeType, id)`,
  `GetScopeIdsAsync(capability, scopeType)`, `HasGlobalAsync(capability)`. Return
  404 when a read is not covered.
- Scoped data derives from `ScopedCrudService` and names its `Feature`: lists and
  reads use where the caller can **view**; anything else is 404. Create, update and
  deactivate need **manage** for that object, else 403.
- Nobody can hand out more than they hold (`UserService`): every capability-in-a-place
  a change would add — through a role, a role scope or a feature access row — must
  be held by the editor in that place. Giving the Administrator role needs every
  capability, everywhere.
- The UI hides what the user cannot use (`IUserAccess`, `Navigation.Items`), but
  that is cosmetic; the API is the enforcement point.

## Banned patterns

```csharp
if (user.Role == "Supervisor")        // review failure
if (user.IsAdmin)                     // review failure
[Authorize(Roles = "WarehouseManager")] // review failure
```

Role names never appear in business logic. A capability is a seeded data row; a
scope is a row in `user_scopes`. Widening one role must never silently widen
another — permission inheritance is deliberately not implemented.

## Required pattern

```csharp
[RequiresCapability(Capabilities.ApproveAttendance)]
public async Task<IActionResult> Approve(Guid attendanceId)
{
    var record = await _attendance.GetAsync(attendanceId);
    if (!await _scope.Covers(User, ScopeType.Site, record.SiteId))
        return NotFound();   // 404, never 403
    ...
}
```

## Scope types

`global` · `warehouse` · `warehouse_group` · `site` · `project` · `customer`

## Out of scope returns 404

Telling a supervisor that site 47 exists but they cannot see it is itself a
leak. This holds everywhere, and on the storefront it is part of how warehouse
opacity is maintained.

## Scope changes take effect immediately

Check against current scope on each request, not the scope captured in the token
at login. Revoking a scope must not wait for a token to expire.

## The controls that protect the business

These are requirements, not suggestions. Each needs a test.

- A supervisor sets a wage only within the band configured for that work type
  and site. Outside the band requires the project manager.
- A supervisor cannot approve their own attendance record.
- A supervisor cannot approve an advance to themselves.
- Advances above the configured threshold need project manager approval.
- A payout run is created by one person and approved by another.
- Attendance older than the configured window cannot be marked, only adjusted
  with a reason.

## Audit

Super admin actions are audited at higher detail, **including reads** of wage
and wallet data. Audit is middleware (P10) — never a line a controller has to
remember.
