param([string]$Base = "http://localhost:5310/api")
function Call($m, $u, $body, $tok) {
  try {
    $h = @{}; if ($tok) { $h.Authorization = "Bearer $tok" }
    $a = @{ Method = $m; Uri = "$Base/$u"; Headers = $h; UseBasicParsing = $true }
    if ($null -ne $body) { $a.ContentType = "application/json"; $a.Body = ($body | ConvertTo-Json -Depth 8) }
    $r = Invoke-WebRequest @a
    return @{ s = [int]$r.StatusCode; d = ($(if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null })) }
  } catch {
    $resp = $_.Exception.Response
    $c = if ($resp) { (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } else { $_.Exception.Message }
    return @{ s = [int]$resp.StatusCode; d = $c }
  }
}
function Check($label, $actual, $expected) { "{0}  {1,-70} got {2}, want {3}" -f ($(if ("$actual" -eq "$expected") { "PASS" } else { "FAIL" })), $label, $actual, $expected }
function Wh($code, $name) { @{ code = $code; name = $name; type = "Owned"; address = @{ line1 = "Plot 1"; city = "Pune"; state = "Maharashtra"; pincode = "411001" } } }
function Wscope($id) { @{ scopeType = "Warehouse"; scopeId = $id } }
function Login($email, $pw) { (Call POST "auth/login" @{ email = $email; password = $pw }).d.accessToken }

$sfx = Get-Random -Maximum 99999
$admin = Login "admin@platform.local" "Emulator-Admin@2026"
$a = (Call POST "warehouses" (Wh "AC-A-$sfx" "Alpha Store") $admin).d
$b = (Call POST "warehouses" (Wh "AC-B-$sfx" "Beta Yard") $admin).d

"--- direct feature access with its own scope (no roles)"
$keeperEmail = "keeper$sfx@platform.local"
$keeper = Call POST "users" @{ name = "Store Keeper"; email = $keeperEmail; password = "Keeper@12345"; roleIds = @(); scopes = @();
  access = @(@{ feature = "warehouses"; level = "Manage"; scopes = @(Wscope $a.id) }, @{ feature = "catalog"; level = "View"; scopes = @() }, @{ feature = "brands"; level = "None"; scopes = @() }) } $admin
Check "create keeper with feature access only" $keeper.s 201
Check "stored access rows (None dropped)" (($keeper.d.access | ForEach-Object { "$($_.feature):$($_.level)" }) -join ",") "catalog:View,warehouses:Manage"
$k = Login $keeperEmail "Keeper@12345"
Check "keeper capabilities" (((Call GET "auth/me" $null $k).d.capabilities) -join ",") "catalog.view,warehouses.manage,warehouses.view"
Check "keeper lists warehouses -> only Alpha" ((Call GET "warehouses" $null $k).d.items.code -join ",") "AC-A-$sfx"
Check "keeper GET Beta -> 404" (Call GET "warehouses/$($b.id)" $null $k).s 404
$edit = Wh "AC-A-$sfx" "Alpha Store (edited)"; $edit.isActive = $true
Check "keeper edits Alpha" (Call PUT "warehouses/$($a.id)" $edit $k).s 200
Check "keeper creates warehouse (needs global manage) -> 403" (Call POST "warehouses" (Wh "AC-N-$sfx" "New") $k).s 403
Check "keeper browses catalogue" (Call GET "catalog/products" $null $k).s 200
Check "keeper imports (no catalog.manage) -> 403" (Call GET "catalog/import/template" $null $k).s 403
Check "keeper GET brands -> 403" (Call GET "brands" $null $k).s 403

"--- validation"
Check "scoped feature without a place -> 400" (Call POST "users" @{ name = "X"; email = "v1$sfx@platform.local"; password = "Xx@1234567"; roleIds = @(); scopes = @(); access = @(@{ feature = "warehouses"; level = "View"; scopes = @() }) } $admin).s 400
Check "unknown feature -> 400" (Call POST "users" @{ name = "X"; email = "v2$sfx@platform.local"; password = "Xx@1234567"; roleIds = @(); scopes = @(); access = @(@{ feature = "rockets"; level = "View"; scopes = @() }) } $admin).s 400
$dup = Call POST "users" @{ name = "X"; email = "v3$sfx@platform.local"; password = "Xx@1234567"; roleIds = @(); scopes = @(); access = @(@{ feature = "catalog"; level = "View"; scopes = @() }, @{ feature = "catalog"; level = "Manage"; scopes = @() }) } $admin
Check "feature listed twice -> merged into one row at the higher level" "$($dup.s) $(@($dup.d.access).Count) $($dup.d.access[0].level)" "201 1 Manage"
Check "unknown warehouse in access -> 400" (Call POST "users" @{ name = "X"; email = "v4$sfx@platform.local"; password = "Xx@1234567"; roleIds = @(); scopes = @(); access = @(@{ feature = "warehouses"; level = "View"; scopes = @(Wscope ([guid]::NewGuid())) }) } $admin).s 400

"--- role in one place + direct access in another: view Beta via role, manage Alpha directly"
$viewerRole = (Call POST "roles" @{ name = "WH Viewer $sfx"; capabilities = @("warehouses.view") } $admin).d
$mixEmail = "mix$sfx@platform.local"
$mix = Call POST "users" @{ name = "Mixed"; email = $mixEmail; password = "Mixed@12345"; roleIds = @($viewerRole.id); scopes = @(Wscope $b.id);
  access = @(@{ feature = "warehouses"; level = "Manage"; scopes = @(Wscope $a.id) }) } $admin
Check "create mixed user" $mix.s 201
$m = Login $mixEmail "Mixed@12345"
Check "mixed lists Alpha and Beta" (Call GET "warehouses" $null $m).d.totalCount 2
$editB = Wh "AC-B-$sfx" "Beta hijack"; $editB.isActive = $true
Check "mixed edits Beta (view only there) -> 403" (Call PUT "warehouses/$($b.id)" $editB $m).s 403
Check "mixed deactivates Beta (view only there) -> 403" (Call DELETE "warehouses/$($b.id)" $null $m).s 403
Check "mixed edits Alpha (manage there)" (Call PUT "warehouses/$($a.id)" $edit $m).s 200

"--- escalation guard: you can only give what you hold"
$mgrEmail = "mgr$sfx@platform.local"
$mgr = Call POST "users" @{ name = "Alpha Manager"; email = $mgrEmail; password = "Manager@12345"; roleIds = @(); scopes = @();
  access = @(@{ feature = "users"; level = "Manage"; scopes = @() }, @{ feature = "roles"; level = "View"; scopes = @() }, @{ feature = "warehouses"; level = "Manage"; scopes = @(Wscope $a.id) }) } $admin
Check "create manager (users org-wide, warehouses Alpha)" $mgr.s 201
$g = Login $mgrEmail "Manager@12345"
function NewUser($n, $access, $roles, $scopes) { @{ name = $n; email = "$n$sfx@platform.local".ToLower(); password = "Aa@12345678"; roleIds = @($roles); scopes = @($scopes); access = @($access) } }
Check "manager gives warehouses View @Alpha -> 201" (Call POST "users" (NewUser "u1" @(@{ feature = "warehouses"; level = "View"; scopes = @(Wscope $a.id) }) @() @()) $g).s 201
Check "manager gives warehouses View @Beta -> 403" (Call POST "users" (NewUser "u2" @(@{ feature = "warehouses"; level = "View"; scopes = @(Wscope $b.id) }) @() @()) $g).s 403
Check "manager gives warehouses View Everywhere -> 403" (Call POST "users" (NewUser "u3" @(@{ feature = "warehouses"; level = "View"; scopes = @(@{ scopeType = "Global" }) }) @() @()) $g).s 403
Check "manager gives brands View (not held) -> 403" (Call POST "users" (NewUser "u4" @(@{ feature = "brands"; level = "View"; scopes = @() }) @() @()) $g).s 403
$adminRole = ((Call GET "roles" $null $admin).d.items | Where-Object { $_.name -eq "Administrator" }).id
Check "manager gives Administrator role -> 403 (escalation closed)" (Call POST "users" (NewUser "u5" @() @($adminRole) @(@{ scopeType = "Global" })) $g).s 403
Check "manager with no role gives WH Viewer role -> 403 (hierarchy)" (Call POST "users" (NewUser "u6" @() @($viewerRole.id) @(Wscope $a.id)) $g).s 403
Check "manager gives WH Viewer role @Beta -> 403" (Call POST "users" (NewUser "u7" @() @($viewerRole.id) @(Wscope $b.id)) $g).s 403
$keeperDto = (Call GET "users/$($keeper.d.id)" $null $admin).d
$strip = @{ name = $keeperDto.name; phone = $null; roleIds = @(); scopes = @(); access = @(@{ feature = "warehouses"; level = "Manage"; scopes = @(Wscope $a.id) }); isActive = $true }
Check "manager removes keeper's catalogue View (not held) -> 403" (Call PUT "users/$($keeper.d.id)" $strip $g).s 403
$rename = @{ name = "Store Keeper Renamed"; phone = $null; roleIds = @(); scopes = @(); access = $keeperDto.access; isActive = $true }
Check "manager cannot edit keeper holding access they lack -> 403 (not below)" (Call PUT "users/$($keeper.d.id)" $rename $g).s 403

"--- revocation is immediate"
$revoke = @{ name = $keeperDto.name; phone = $null; roleIds = @(); scopes = @(); access = @(@{ feature = "catalog"; level = "View"; scopes = @() }); isActive = $true }
Check "admin removes keeper's warehouse access" (Call PUT "users/$($keeper.d.id)" $revoke $admin).s 200
Check "keeper lists warehouses, SAME token -> 403" (Call GET "warehouses" $null $k).s 403
