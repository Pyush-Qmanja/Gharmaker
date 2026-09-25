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
- **Role hierarchy** (`AccessHierarchy`, `RoleTree`): every role has a `ParentId`
  (null = directly under the top); the built-in Administrator role (`IsSystem`, never
  identified by name) is the root, always holds every capability, and cannot be
  edited, renamed or deactivated. Administrators manage everything. Anyone else may
  create, change or deactivate only roles **strictly below** a role they hold, attach
  new roles only at or below their own, and add or remove only capabilities they hold.
  For users: only people whose every role is below theirs **and** whose direct access
  they hold themselves; never their own roles or access (profile only); roles given or
  taken must be below theirs. At least one active administrator must always remain.
  These rules sit on top of "only give what you hold", never instead of it.
- Stock features are warehouse-scoped: `stock` (view / adjust + opening + ledger check),
  `receipts`, `transfers`. Sending a transfer needs manage at the source, receiving it
  manage at the destination; the ledger check needs stock-adjust everywhere.
- Phase 3 features: `delivery` (warehouse-scoped PIN codes), `pricing` (price lists,
  prices, GST rates), `customers`, `orders` (manage = confirm or cancel) and `settings` (business details).
- Two kinds of caller. Staff tokens carry `actor=staff`; storefront tokens carry
  `actor=customer`. The API's default policy (`AuthPolicies.StaffDefault`) refuses a
  customer token on every staff endpoint, and `PermissionService` gives a customer no
  capability; storefront endpoints that need a customer say `[CustomerOnly]`. In the web
  app the store has its own cookie (path `/shop`) and every `/shop` request runs as the
  customer or as nobody (`ShopAuth.UseShopIdentity`), never as a staff user.
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

Signed-in users are never "stuck" with old access:

- Access changes (role, feature access, places, role deactivated) apply on the
  caller's next request — `IPermissionService` reads the user and roles per request
  through the request-scoped `ICallerAccount`. Never cache capabilities across
  requests; if a cache is ever added (e.g. Redis) it must be invalidated on every
  user or role write.
- Ending a session is immediate: `SessionGuard` (API, after authentication) answers
  401 "Your session has ended" when the user is inactive or the token was issued at
  or before `User.SessionsEndedAt`. That field is set on deactivation and by "sign out
  everywhere" (`POST api/users/{id}/sessions/end`, or `api/auth/sessions/end` for
  oneself). A restored user's old tokens stay dead.
- The web app checks `/api/auth/me` once per staff page (`StaffSession`): an ended
  session signs the browser out and sends it to sign-in with a notice; a renamed user's
  cookie is re-issued with the new name.
- Customers: a blocked customer is refused by the storefront customer context and the
  store signs them out ("Your session has ended").

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

## Abuse limits and browser security

- Rate limits (`Api/Security/RateLimiting.cs`, section `RateLimiting`): every caller
  (signed-in user, else client IP) gets separate read and write budgets per minute, so
  no client can run up Firestore reads or writes. Sign-in (staff and store) is 10 a minute
  per IP, store registration 5 per 15 minutes per IP, checkout 10 a minute per customer.
  Refused calls get 429 with `Retry-After`. Put `[EnableRateLimiting(RateLimitPolicies.X)]`
  on any new endpoint that signs in, creates accounts or costs money.
- The web app forwards each visitor's IP to the API (`ForwardedForHandler`). Both apps
  trust `X-Forwarded-For` only from loopback and the proxies listed in
  `RateLimiting:TrustedProxies` (API) and `Security:TrustedProxies` (web). **In production
  list the web servers and load balancer there**, or every visitor shares one budget.
- Registration never says whether an email is in use (customer or staff): one generic
  422 message for every taken email.
- Security headers on every response: a strict Content-Security-Policy (only this site;
  possible because the project allows no CDN, inline script or inline style), no
  framing, nosniff, referrer and permissions policies, no Server banner; API responses
  are `no-store`. HSTS outside Development.

## Audit

Super admin actions are audited at higher detail, **including reads** of wage
and wallet data. Audit is middleware (P10) — never a line a controller has to
remember.
