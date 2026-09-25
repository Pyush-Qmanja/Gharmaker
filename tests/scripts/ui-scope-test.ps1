$Web = "https://localhost:7331"
function Token($html) { if ($html -match 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"') { $Matches[1] } }
function Check($label, $ok) { "{0}  {1}" -f ($(if ($ok) { "PASS" } else { "FAIL" })), $label }
function Get-Page($session, $path) {
  try { $r = Invoke-WebRequest "$Web$path" -WebSession $session -UseBasicParsing -MaximumRedirection 5; return @{ s = [int]$r.StatusCode; c = $r.Content } }
  catch { $resp = $_.Exception.Response; $c = if ($resp) { (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } else { "" }; return @{ s = [int]$resp.StatusCode; c = $c } }
}
function Post-Form($session, $path, $pagePath, $fields) {
  $token = Token (Get-Page $session $pagePath).c
  $body = ($fields + @(,@("__RequestVerificationToken", $token)) | ForEach-Object { [uri]::EscapeDataString($_[0]) + "=" + [uri]::EscapeDataString([string]$_[1]) }) -join "&"
  try { $r = Invoke-WebRequest "$Web$path" -Method Post -Body $body -ContentType "application/x-www-form-urlencoded" -WebSession $session -UseBasicParsing -MaximumRedirection 5; return @{ s = [int]$r.StatusCode; c = $r.Content } }
  catch { $resp = $_.Exception.Response; $c = if ($resp) { (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } else { "" }; return @{ s = [int]$resp.StatusCode; c = $c } }
}
function Sign-In($email, $password) {
  $s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
  $r = Post-Form $s "/Account/Login" "/Account/Login" @(@("Email", $email), @("Password", $password))
  return @{ session = $s; page = $r }
}
function Wh-Fields($code, $name, $pin) { @(@("Code", $code), @("Name", $name), @("Type", "ThirdParty"), @("OwnerUserId", ""), @("Address.Line1", "Gat 45, Chakan"), @("Address.City", "Pune"), @("Address.State", "Maharashtra"), @("Address.Pincode", $pin), @("Lat", "18.7606"), @("Lng", "73.8636")) }

$admin = Sign-In "admin@platform.local" "Emulator-Admin@2026"
Check "admin sees Warehouses tile" ($admin.page.c -match 'module-card__title">Warehouses')

$form = (Get-Page $admin.session "/Warehouses/Create").c
Check "warehouse form: Type is a drop-down with 'Third Party'" (($form -match '<select[^>]*name="Type"') -and ($form -match '>Third Party<'))
Check "warehouse form: responsible person drop-down with None + Administrator" (($form -match 'name="OwnerUserId"') -and ($form -match '>None<') -and ($form -match '>Administrator<'))
Check "warehouse form: nested address fields + decimal step" (($form -match 'name="Address.Pincode"') -and ($form -match 'step="any"'))

$bad = Post-Form $admin.session "/Warehouses/Create" "/Warehouses/Create" (Wh-Fields "WH-BAD" "Bad" "01234")
Check "bad PIN shows error on the PIN field" ($bad.c -match "six-digit PIN code")

$sfx = (Get-Random -Maximum 99999)
$a = Post-Form $admin.session "/Warehouses/Create" "/Warehouses/Create" (Wh-Fields "wh-chakan-$sfx" "Chakan Hub" "410501")
Check "create warehouse via form" ($a.c -match "Warehouse created")
Check "list shows upper-cased code and 'Third Party'" (($a.c -match "WH-CHAKAN-$sfx") -and ($a.c -match "Third Party"))
$b = Post-Form $admin.session "/Warehouses/Create" "/Warehouses/Create" (Wh-Fields "WH-NASHIK-$sfx" "Nashik Depot" "422001")
$ids = [regex]::Matches($b.c, "/Warehouses/Edit/([0-9a-f-]{36})") | ForEach-Object { $_.Groups[1].Value }
$chakanRow = [regex]::Match($b.c, "WH-CHAKAN-$sfx[\s\S]*?/Warehouses/Edit/([0-9a-f-]{36})").Groups[1].Value
$nashikRow = [regex]::Match($b.c, "WH-NASHIK-$sfx[\s\S]*?/Warehouses/Edit/([0-9a-f-]{36})").Groups[1].Value
Check "both warehouses listed for admin" ($chakanRow -and $nashikRow)

$role = Post-Form $admin.session "/Roles/Create" "/Roles/Create" @(@("Name", "Hub Viewer $sfx"), @("Capabilities", "warehouses.view"))
$userForm = (Get-Page $admin.session "/Users/Create").c
Check "user form: scope picker has Everywhere + the new warehouses" (($userForm -match 'value="Global"') -and ($userForm -match "value=`"Warehouse:$chakanRow`"") -and ($userForm -match "WH-CHAKAN-$sfx"))
$roleId = [regex]::Match($userForm, "value=`"([0-9a-f-]{36})`"[^>]*/?>\s*<label[^>]*>[^<]*?Hub Viewer $sfx").Groups[1].Value
$email = "hub$sfx@platform.local"
$u = Post-Form $admin.session "/Users/Create" "/Users/Create" @(@("Name", "Chakan Keeper"), @("Email", $email), @("Password", "Hub@123456"), @("RoleIds", $roleId), @("Scopes", "Warehouse:$chakanRow"))
Check "create user scoped to Chakan via form" ($u.c -match "User created")
Check "users list shows the warehouse scope chip" ($u.c -match "class=`"chip`">WH-CHAKAN-$sfx")

$keeper = Sign-In $email "Hub@123456"
Check "keeper sees only the Warehouses tile" (($keeper.page.c -match 'module-card__title">Warehouses') -and ($keeper.page.c -notmatch 'module-card__title">Users'))
$list = (Get-Page $keeper.session "/Warehouses").c
Check "keeper's list: Chakan only, no Nashik" (($list -match "WH-CHAKAN-$sfx") -and ($list -notmatch "WH-NASHIK-$sfx"))
Check "keeper opens Nashik edit page -> 404" ((Get-Page $keeper.session "/Warehouses/Edit/$nashikRow").s -eq 404)
