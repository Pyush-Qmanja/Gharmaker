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
