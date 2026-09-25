param([string]$Base = "http://localhost:5310/api")
function Call($m, $u, $body, $tok) {
  try {
    $h = @{}; if ($tok) { $h.Authorization = "Bearer $tok" }
    $a = @{ Method = $m; Uri = "$Base/$u"; Headers = $h; UseBasicParsing = $true }
    if ($null -ne $body) { $a.ContentType = "application/json"; $a.Body = ($body | ConvertTo-Json -Depth 6) }
    $r = Invoke-WebRequest @a
    return @{ s = [int]$r.StatusCode; d = ($(if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null })) }
  } catch {
    $resp = $_.Exception.Response
    $c = if ($resp) { (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } else { $_.Exception.Message }
    return @{ s = [int]$resp.StatusCode; d = $c }
  }
}
function Check($label, $actual, $expected) { "{0}  {1,-66} got {2}, want {3}" -f ($(if ("$actual" -eq "$expected") { "PASS" } else { "FAIL" })), $label, $actual, $expected }
function Wh($code, $name, $pin) { @{ code = $code; name = $name; type = "Owned"; address = @{ line1 = "Plot 12, MIDC"; city = "Pune"; state = "Maharashtra"; pincode = $pin }; lat = 18.52; lng = 73.85 } }

$admin = (Call POST "auth/login" @{ email = "admin@platform.local"; password = "Emulator-Admin@2026" }).d.accessToken
"--- admin sets up warehouses"
$w1 = Call POST "warehouses" (Wh "wh-pune-01" "Pune Central" "411001") $admin
Check "create WH-PUNE-01 (code upper-cased)" "$($w1.s) $($w1.d.code)" "201 WH-PUNE-01"
$w2 = Call POST "warehouses" (Wh "WH-MUM-01" "Mumbai Yard" "400001") $admin
Check "create WH-MUM-01" $w2.s 201
Check "duplicate code" (Call POST "warehouses" (Wh "WH-MUM-01" "Again" "400001") $admin).s 409
Check "bad PIN code" (Call POST "warehouses" (Wh "WH-X" "X" "012345") $admin).s 400
$half = Wh "WH-Y" "Y" "411002"; $half.Remove("lng")
Check "latitude without longitude" (Call POST "warehouses" $half $admin).s 400
Check "admin lists both" (Call GET "warehouses" $null $admin).d.totalCount 2

$role = Call POST "roles" @{ name = "Warehouse Lead"; capabilities = @("warehouses.view", "warehouses.manage", "users.view", "users.manage", "roles.view") } $admin
$sfx = Get-Random
$leadEmail = "lead$sfx@platform.local"
$lead = Call POST "users" @{ name = "Pune Lead"; email = $leadEmail; password = "Lead@123456"; roleIds = @($role.d.id); scopes = @(@{ scopeType = "Warehouse"; scopeId = $w1.d.id }) } $admin
Check "create lead scoped to WH-PUNE-01" $lead.s 201
Check "grant scope to unknown warehouse" (Call POST "users" @{ name = "X"; email = "x$sfx@platform.local"; password = "Xx@1234567"; roleIds = @(); scopes = @(@{ scopeType = "Warehouse"; scopeId = [guid]::NewGuid() }) } $admin).s 400
Check "grant a Site scope (not available yet)" (Call POST "users" @{ name = "X"; email = "y$sfx@platform.local"; password = "Xx@1234567"; roleIds = @(); scopes = @(@{ scopeType = "Site"; scopeId = [guid]::NewGuid() }) } $admin).s 400

"--- lead (scoped to WH-PUNE-01) - the Phase 1 gate"
$t = (Call POST "auth/login" @{ email = $leadEmail; password = "Lead@123456" }).d.accessToken
$list = Call GET "warehouses" $null $t
Check "lead list shows only their warehouse" "$($list.d.totalCount) $($list.d.items.code -join ',')" "1 WH-PUNE-01"
Check "lead GET own warehouse" (Call GET "warehouses/$($w1.d.id)" $null $t).s 200
Check "lead GET other warehouse -> 404 (not 403)" (Call GET "warehouses/$($w2.d.id)" $null $t).s 404
$upd = Wh "WH-MUM-01" "Hijack" "400001"; $upd.isActive = $true
Check "lead PUT other warehouse -> 404" (Call PUT "warehouses/$($w2.d.id)" $upd $t).s 404
Check "lead DELETE other warehouse -> 404" (Call DELETE "warehouses/$($w2.d.id)" $null $t).s 404
$own = Wh "WH-PUNE-01" "Pune Central Store" "411001"; $own.isActive = $true
Check "lead edits own warehouse" (Call PUT "warehouses/$($w1.d.id)" $own $t).s 200
Check "lead creates a warehouse (needs global) -> 403" (Call POST "warehouses" (Wh "WH-NEW" "New" "411003") $t).s 403
Check "lead search 'mum' finds nothing" (Call GET "warehouses?search=mum" $null $t).d.totalCount 0
Check "lead search 'central' finds own (contains match)" (Call GET "warehouses?search=central" $null $t).d.totalCount 1

"--- escalation guard"
Check "lead grants Global to a new user -> 403" (Call POST "users" @{ name = "G"; email = "g$sfx@platform.local"; password = "Gg@1234567"; roleIds = @(); scopes = @(@{ scopeType = "Global" }) } $t).s 403
Check "lead grants WH-MUM-01 -> 403" (Call POST "users" @{ name = "M"; email = "m$sfx@platform.local"; password = "Mm@1234567"; roleIds = @(); scopes = @(@{ scopeType = "Warehouse"; scopeId = $w2.d.id }) } $t).s 403
Check "lead grants own WH-PUNE-01 -> 201" (Call POST "users" @{ name = "Picker"; email = "p$sfx@platform.local"; password = "Pp@1234567"; roleIds = @(); scopes = @(@{ scopeType = "Warehouse"; scopeId = $w1.d.id }) } $t).s 201
$adminId = (Call GET "auth/me" $null $admin).d.userId
$adminDto = (Call GET "users/$adminId" $null $admin).d
Check "lead strips admin's Global scope -> 403" (Call PUT "users/$adminId" @{ name = $adminDto.name; phone = $null; roleIds = $adminDto.roleIds; scopes = @(); isActive = $true } $t).s 403

"--- revocation is immediate"
$leadDto = (Call GET "users/$($lead.d.id)" $null $admin).d
Check "admin removes lead's warehouse scope" (Call PUT "users/$($lead.d.id)" @{ name = $leadDto.name; phone = $null; roleIds = $leadDto.roleIds; scopes = @(); isActive = $true } $admin).s 200
Check "lead GET own warehouse, SAME token -> 404" (Call GET "warehouses/$($w1.d.id)" $null $t).s 404
Check "lead list now empty" (Call GET "warehouses" $null $t).d.totalCount 0
