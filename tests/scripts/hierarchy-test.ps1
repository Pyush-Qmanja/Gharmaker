param([string]$Base = "http://localhost:5310/api", [string]$Web = "https://localhost:7331")
# Role hierarchy + live session suite. TEST environment only (emulators, :5310 / :7331).
$ErrorActionPreference = "Continue"
function Call($m, $u, $body, $tok) {
  try {
    $h = @{}; if ($tok) { $h.Authorization = "Bearer $tok" }
    $a = @{ Method = $m; Uri = "$Base/$u"; Headers = $h; UseBasicParsing = $true }
    if ($null -ne $body) { $a.ContentType = "application/json"; $a.Body = ($body | ConvertTo-Json -Depth 6) }
    $r = Invoke-WebRequest @a
    return @{ s = [int]$r.StatusCode; d = ($(if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null })); t = $r.Content }
  } catch {
    $code = $_.Exception.Response.StatusCode.value__
    $c = if ($_.Exception.Response) { (New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd() } else { $_.Exception.Message }
    return @{ s = $code; d = $null; t = $c }
  }
}
function Check($label, $ok, $detail = "") { "{0}  {1}{2}" -f $(if ($ok) { "PASS" } else { "FAIL" }), $label, $(if (-not $ok -and $detail) { "   [$detail]" } else { "" }) }
function Login($email, $pw) { (Call POST "auth/login" @{ email = $email; password = $pw }).d.accessToken }
$pw = "Test@12345"
$sfx = Get-Random -Maximum 99999
$admin = Login "admin@platform.local" "Emulator-Admin@2026"
$global = @(@{ scopeType = "Global" })

"--- setup: Administrator > Branch manager > Store keeper, and Administrator > Sales head"
$roles = (Call GET "roles?pageSize=100" $null $admin).d.items
$adminRole = $roles | Where-Object { $_.isSystem } | Select-Object -First 1
Check "Administrator role is the built-in top role" ($null -ne $adminRole -and $adminRole.name -eq "Administrator")
$bm = (Call POST "roles" @{ name = "Branch manager $sfx"; capabilities = @("users.view", "users.manage", "roles.view", "roles.manage", "stock.view", "brands.view") } $admin).d
$sk = (Call POST "roles" @{ name = "Store keeper $sfx"; parentId = $bm.id; capabilities = @("stock.view") } $admin).d
$sh = (Call POST "roles" @{ name = "Sales head $sfx"; capabilities = @("users.view", "users.manage", "roles.view", "roles.manage") } $admin).d
Check "roles created with their parents" ($bm.id -and $sk.parentId -eq $bm.id -and $sh.id)
function NewUser($name, $roleId) { (Call POST "users" @{ name = $name; email = "$($name.ToLower())$sfx@platform.local"; password = $pw; roleIds = @($roleId); scopes = $global } $admin).d }
$uBm = NewUser "bm" $bm.id; $uSk = NewUser "sk" $sk.id; $uSh = NewUser "sh" $sh.id
$tBm = Login "bm$sfx@platform.local" $pw
Check "branch manager signs in" ([bool]$tBm)

"--- roles: only below your own"
Check "BM changes Store keeper (below)" ((Call PUT "roles/$($sk.id)" @{ name = $sk.name; parentId = $bm.id; capabilities = @("stock.view", "brands.view"); isActive = $true } $tBm).s -eq 200)
$r = Call PUT "roles/$($bm.id)" @{ name = $bm.name; capabilities = @("users.view", "users.manage", "roles.view", "roles.manage", "stock.view", "brands.view", "pricing.manage"); isActive = $true } $tBm
Check "BM cannot widen own role" ($r.s -eq 403 -and $r.t -match "below your own") $r.t
$r = Call PUT "roles/$($adminRole.id)" @{ name = "Administrator"; capabilities = @("brands.view"); isActive = $true } $admin
Check "nobody, not even an admin, changes the Administrator role" ($r.s -eq 403 -and $r.t -match "cannot be changed") $r.t
Check "Administrator role cannot be deactivated" ((Call DELETE "roles/$($adminRole.id)" $null $admin).s -eq 403)
Check "BM cannot change a peer branch role" ((Call PUT "roles/$($sh.id)" @{ name = $sh.name; capabilities = @("users.view"); isActive = $true } $tBm).s -eq 403)
$r = Call POST "roles" @{ name = "Top $sfx"; capabilities = @("stock.view") } $tBm
Check "BM cannot create a role at the top" ($r.s -eq 403) $r.t
$jr = Call POST "roles" @{ name = "Helper $sfx"; parentId = $sk.id; capabilities = @("stock.view") } $tBm
Check "BM creates a role under Store keeper" ($jr.s -eq 201)
$r = Call POST "roles" @{ name = "Pricer $sfx"; parentId = $bm.id; capabilities = @("pricing.view", "pricing.manage") } $tBm
Check "BM cannot give a feature they do not hold" ($r.s -eq 403 -and $r.t -match "features you hold") $r.t
$r = Call PUT "roles/$($bm.id)" @{ name = $bm.name; parentId = $jr.d.id; capabilities = $bm.capabilities; isActive = $true } $admin
Check "admin cannot make a role report to its own junior (loop)" ($r.s -eq 400) $r.t

"--- users: only people below you"
$new = Call POST "users" @{ name = "Junior"; email = "jr$sfx@platform.local"; password = $pw; roleIds = @($sk.id); scopes = $global } $tBm
Check "BM gives Store keeper to a new user" ($new.s -eq 201)
$r = Call POST "users" @{ name = "Peer"; email = "peer$sfx@platform.local"; password = $pw; roleIds = @($bm.id); scopes = $global } $tBm
Check "BM cannot give own role (peer)" ($r.s -eq 403) $r.t
$r = Call POST "users" @{ name = "Boss"; email = "boss$sfx@platform.local"; password = $pw; roleIds = @($adminRole.id); scopes = $global } $tBm
Check "BM cannot make an administrator" ($r.s -eq 403) $r.t
Check "BM cannot deactivate the other branch's user" ((Call DELETE "users/$($uSh.id)" $null $tBm).s -eq 403)
$adminUser = ((Call GET "users?search=admin@platform.local" $null $admin).d.items | Select-Object -First 1)
Check "BM cannot deactivate an administrator" ((Call DELETE "users/$($adminUser.id)" $null $tBm).s -eq 403)
$r = Call PUT "users/$($uBm.id)" @{ name = "bm"; roleIds = @($bm.id, $sh.id); scopes = $global; access = @(); isActive = $true } $tBm
Check "BM cannot change own roles" ($r.s -eq 403 -and $r.t -match "your own roles") $r.t
$r = Call PUT "users/$($uBm.id)" @{ name = "Branch Manager Renamed"; phone = "+919876543210"; roleIds = @($bm.id); scopes = $global; access = @(); isActive = $true } $tBm
Check "BM can still edit own profile" ($r.s -eq 200) $r.t
Check "BM deactivates a store keeper (below)" ((Call DELETE "users/$($uSk.id)" $null $tBm).s -eq 204)

"--- administrators"
$r = Call DELETE "users/$($adminUser.id)" $null $admin
Check "admin cannot deactivate self" ($r.s -eq 422 -or $r.s -eq 400) "$($r.s) $($r.t)"
$admins = (Call GET "users?pageSize=100" $null $admin).d.items | Where-Object { $_.isActive -and ($_.roleIds -contains $adminRole.id) }
if (@($admins).Count -eq 1) {
  $r = Call PUT "users/$($adminUser.id)" @{ name = $adminUser.name; roleIds = @(); scopes = $global; access = @(); isActive = $true } $admin
  Check "the last administrator cannot drop the Administrator role" ($r.t -match "at least one active administrator") "$($r.s) $($r.t)"
} else { "SKIP  last-administrator check (more than one administrator exists)" }

"--- signed-in users are affected at once"
$tJr = Login "jr$sfx@platform.local" $pw
Check "store keeper sees stock" ((Call GET "stock/balances" $null $tJr).s -eq 200)
Call PUT "roles/$($sk.id)" @{ name = $sk.name; parentId = $bm.id; capabilities = @("brands.view"); isActive = $true } $admin | Out-Null
Check "role narrowed: SAME token loses stock at once" ((Call GET "stock/balances" $null $tJr).s -eq 403)
Call DELETE "users/$($new.d.id)" $null $admin | Out-Null
$r = Call GET "auth/me" $null $tJr
Check "user deactivated: SAME token refused at once" ($r.s -eq 401 -and $r.t -match "session has ended") "$($r.s) $($r.t)"
Check "deactivated user cannot sign in" (-not (Login "jr$sfx@platform.local" $pw))
$u = (Call GET "users/$($new.d.id)" $null $admin).d
Call PUT "users/$($new.d.id)" @{ name = $u.name; roleIds = $u.roleIds; scopes = $global; access = @(); isActive = $true } $admin | Out-Null
Check "restored user: the OLD token stays dead" ((Call GET "auth/me" $null $tJr).s -eq 401)
Start-Sleep -Seconds 2
Check "restored user signs in again" ([bool](Login "jr$sfx@platform.local" $pw))

$t1 = Login "sh$sfx@platform.local" $pw; $t2 = Login "sh$sfx@platform.local" $pw
Check "two devices signed in" ((Call GET "auth/me" $null $t1).s -eq 200 -and (Call GET "auth/me" $null $t2).s -eq 200)
Check "admin signs them out everywhere" ((Call POST "users/$($uSh.id)/sessions/end" @{} $admin).s -eq 204)
Check "both devices refused" ((Call GET "auth/me" $null $t1).s -eq 401 -and (Call GET "auth/me" $null $t2).s -eq 401)
Start-Sleep -Seconds 2
$t3 = Login "sh$sfx@platform.local" $pw
Check "they can sign in again" ((Call GET "auth/me" $null $t3).s -eq 200)
Check "BM cannot sign out the other branch's user" ((Call POST "users/$($uSh.id)/sessions/end" @{} $tBm).s -eq 403)
Check "self: sign out everywhere" ((Call POST "auth/sessions/end" @{} $t3).s -eq 204 -and (Call GET "auth/me" $null $t3).s -eq 401)

"--- web: a signed-in browser is signed out on its next click"
$s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$lp = Invoke-WebRequest "$Web/Account/Login" -WebSession $s -UseBasicParsing
$tok = [regex]::Match($lp.Content, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
Start-Sleep -Seconds 2
$body = "Email=" + [uri]::EscapeDataString("sh$sfx@platform.local") + "&Password=" + [uri]::EscapeDataString($pw) + "&__RequestVerificationToken=" + [uri]::EscapeDataString($tok)
$home1 = Invoke-WebRequest "$Web/Account/Login" -Method Post -Body $body -ContentType "application/x-www-form-urlencoded" -WebSession $s -UseBasicParsing
Check "web sign-in lands on the dashboard" ($home1.Content -match "Your modules|No access yet")
$roles1 = Invoke-WebRequest "$Web/Roles/Hierarchy" -WebSession $s -UseBasicParsing
Check "role hierarchy page shows the chart" ($roles1.Content -match "Role hierarchy" -and $roles1.Content -match "role-tree__item--depth-1")
Call POST "users/$($uSh.id)/sessions/end" @{} $admin | Out-Null
$after = Invoke-WebRequest "$Web/Roles" -WebSession $s -UseBasicParsing
Check "next click goes to sign-in with 'session has ended'" ($after.BaseResponse.ResponseUri.AbsolutePath -eq "/Account/Login" -and $after.Content -match "Your session has ended")
$again = Invoke-WebRequest "$Web/" -WebSession $s -UseBasicParsing
Check "the dead cookie is gone (still at sign-in)" ($again.BaseResponse.ResponseUri.AbsolutePath -eq "/Account/Login")
