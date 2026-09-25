param([string]$Base = "http://localhost:5310/api")
$ErrorActionPreference = "Continue"
function Call($m, $u, $body, $tok) {
  try {
    $h = @{}; if ($tok) { $h.Authorization = "Bearer $tok" }
    $args = @{ Method = $m; Uri = "$Base/$u"; Headers = $h; UseBasicParsing = $true }
    if ($null -ne $body) { $args.ContentType = "application/json"; $args.Body = ($body | ConvertTo-Json -Depth 5) }
    $r = Invoke-WebRequest @args
    return @{ s = [int]$r.StatusCode; d = ($(if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null })) }
  } catch {
    $code = $_.Exception.Response.StatusCode.value__
    $c = if ($_.Exception.Response) { (New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd() } else { $_.Exception.Message }
    return @{ s = $code; d = $c }
  }
}
function Check($label, $actual, $expected) { $ok = if ($actual -eq $expected) { "PASS" } else { "FAIL" }; "{0}  {1,-58} got {2}, want {3}" -f $ok, $label, $actual, $expected }

$admin = (Call POST "auth/login" @{ email = "admin@platform.local"; password = "Emulator-Admin@2026" }).d.accessToken
$me = Call GET "auth/me" $null $admin
Check "admin /me" $me.s 200
"      admin capabilities: $($me.d.capabilities -join ', ')"

$role = Call POST "roles" @{ name = "Brand Viewer"; capabilities = @("brands.view") } $admin
Check "create role 'Brand Viewer'" $role.s 201
Check "duplicate role name" (Call POST "roles" @{ name = "Brand Viewer"; capabilities = @("brands.view") } $admin).s 409
Check "unknown capability code" (Call POST "roles" @{ name = "Bad"; capabilities = @("brands.fly") } $admin).s 400

$suffix = Get-Random
$email = "viewer$suffix@platform.local"
$u = Call POST "users" @{ name = "Viewer"; email = $email; password = "Viewer@12345"; roleIds = @($role.d.id); scopes = @(@{ scopeType = "Global" }) } $admin
Check "create user with role" $u.s 201
Check "duplicate email" (Call POST "users" @{ name = "Dup"; email = $email; password = "Viewer@12345"; roleIds = @() ; scopes = @() } $admin).s 409
Check "unknown role id" (Call POST "users" @{ name = "X"; email = "x$suffix@platform.local"; password = "Viewer@12345"; roleIds = @([guid]::NewGuid()); scopes = @() } $admin).s 400
Check "site scope without id" (Call POST "users" @{ name = "X"; email = "y$suffix@platform.local"; password = "Viewer@12345"; roleIds = @(); scopes = @(@{ scopeType = "Site" }) } $admin).s 400

$viewer = (Call POST "auth/login" @{ email = $email; password = "Viewer@12345" }).d.accessToken
"      viewer /me capabilities: $((Call GET 'auth/me' $null $viewer).d.capabilities -join ', ')"
Check "viewer GET brands" (Call GET "brands" $null $viewer).s 200
Check "viewer POST brands (no manage)" (Call POST "brands" @{ name = "Nope"; slug = "nope-$suffix"; displayOrder = 1 } $viewer).s 403
Check "viewer GET users (no users.view)" (Call GET "users" $null $viewer).s 403

$upd = Call PUT "roles/$($role.d.id)" @{ name = "Brand Viewer"; capabilities = @("users.view"); isActive = $true } $admin
Check "admin changes role capabilities" $upd.s 200
Check "viewer GET brands, SAME token, after revoke" (Call GET "brands" $null $viewer).s 403
Check "viewer GET users, same token, after grant" (Call GET "users" $null $viewer).s 200

Check "admin deactivates viewer" (Call DELETE "users/$($u.d.id)" $null $admin).s 204
Check "viewer /me with old token" (Call GET "auth/me" $null $viewer).s 401
Check "viewer GET users with old token (session ended)" (Call GET "users" $null $viewer).s 401
Check "viewer sign-in after deactivation" (Call POST "auth/login" @{ email = $email; password = "Viewer@12345" }).s 401

$adminId = (Call GET "auth/me" $null $admin).d.userId
Check "admin deactivates self" (Call DELETE "users/$adminId" $null $admin).s 422
Check "no token GET roles" (Call GET "roles" $null $null).s 401
$list = Call GET "users?search=viewer" $null $admin
Check "search users by email prefix" $list.s 200
"      users matching 'viewer': $($list.d.items.email -join ', ')"
