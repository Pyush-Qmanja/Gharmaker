$Web = "https://localhost:7331"; $Api = "http://localhost:5310/api"
function Token($html) { if ($html -match 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"') { $Matches[1] } }
function Decode($html) { [System.Net.WebUtility]::HtmlDecode($html) }
function Check($label, $ok) { "{0}  {1}" -f ($(if ($ok) { "PASS" } else { "FAIL" })), $label }
function Get-Page($session, $path) {
  try { $r = Invoke-WebRequest "$Web$path" -WebSession $session -UseBasicParsing -MaximumRedirection 5; return @{ s = [int]$r.StatusCode; c = $r.Content } }
  catch { $resp = $_.Exception.Response; $c = if ($resp) { (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } else { "" }; return @{ s = [int]$resp.StatusCode; c = $c } }
}
function Post-Form($session, $path, $pagePath, $fields) {
  $token = Token (Get-Page $session $pagePath).c
  $body = ($fields + @(,@("__RequestVerificationToken", $token)) | ForEach-Object { [uri]::EscapeDataString($_[0]) + "=" + [uri]::EscapeDataString([string]$_[1]) }) -join "&"
  try { $r = Invoke-WebRequest "$Web$path" -Method Post -Body $body -ContentType "application/x-www-form-urlencoded" -WebSession $session -UseBasicParsing -MaximumRedirection 5; return @{ s = [int]$r.StatusCode; c = $r.Content; u = $r.BaseResponse.ResponseUri.AbsolutePath } }
  catch { $resp = $_.Exception.Response; $c = if ($resp) { (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } else { "" }; return @{ s = [int]$resp.StatusCode; c = $c } }
}
function Sign-In($email, $password) {
  $s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
  $null = Post-Form $s "/Account/Login" "/Account/Login" @(@("Email", $email), @("Password", $password))
  return $s
}
# Grid rows in feature order: 0 catalog, 1 brands, 2 warehouses, 3 users, 4 roles.
function Grid($levels, $whScopes) {
  $codes = "catalog", "brands", "warehouses", "users", "roles"
  $f = @()
  for ($i = 0; $i -lt 5; $i++) {
    $f += ,@("Access[$i].Feature", $codes[$i]); $f += ,@("Access[$i].Level", $levels[$i])
  }
  foreach ($sc in $whScopes) { $f += ,@("Access[2].Scopes", $sc) }
  return $f
}

$t = (Invoke-RestMethod -Method Post "$Api/auth/login" -ContentType application/json -Body '{"email":"admin@platform.local","password":"Emulator-Admin@2026"}').accessToken
$h = @{ Authorization = "Bearer $t" }
$sfx = Get-Random -Maximum 99999
$wh = Invoke-RestMethod -Method Post "$Api/warehouses" -Headers $h -ContentType application/json -Body (@{ code = "UI-$sfx"; name = "UI Depot"; type = "Owned"; address = @{ line1 = "1"; city = "Pune"; state = "MH"; pincode = "411001" } } | ConvertTo-Json -Depth 4)
$whScope = "Warehouse:$($wh.id)"

$admin = Sign-In "admin@platform.local" "Emulator-Admin@2026"
$form = (Get-Page $admin "/Users/Create").c
Check "user form has the access grid with levels and warehouse places" (($form -match 'name="Access\[2\]\.Level" value="Manage"') -and ($form -match "name=`"Access\[2\]\.Scopes`" value=`"$whScope`"") -and ($form -match 'Whole organisation'))
Check "user form has 'What this user can do' summary" ($form -match 'cannot open anything yet')

"--- create through the form"
$email = "grid$sfx@platform.local"
$base = @(@("Name", "Grid User"), @("Email", $email), @("Password", "Grid@123456"))
$r = Post-Form $admin "/Users/Create" "/Users/Create" ($base + (Grid @("View", "None", "Manage", "None", "None") @($whScope)))
Check "posted form redirects to the list" ($r.u -eq "/Users")
$u = (Invoke-RestMethod "$Api/users?search=grid$sfx" -Headers $h).items[0]
Check "stored access: catalog View, warehouses Manage @UI depot" ((($u.access | ForEach-Object { "$($_.feature):$($_.level):$($_.scopes.Count)" }) -join ",") -eq "catalog:View:0,warehouses:Manage:1")
$edit = (Get-Page $admin "/Users/Edit/$($u.id)").c
Check "edit form shows Manage selected and the place ticked" (($edit -match 'value="Manage"\s+data-level="Manage" checked') -and ($edit -match "value=`"$whScope`"\s+checked"))
Check "edit form summary lists Warehouses Manage (Direct)" (($edit -match 'level-badge--manage">Manage') -and ($edit -match 'chip--primary">Direct'))

"--- validation on the form"
$bad = Post-Form $admin "/Users/Create" "/Users/Create" (@(@("Name", "No Place"), @("Email", "np$sfx@platform.local"), @("Password", "Grid@123456")) + (Grid @("None", "None", "View", "None", "None") @()))
Check "scoped feature without a place: form shown again with the message" (($bad.u -eq "/Users/Create") -and ((Decode $bad.c) -match 'Choose where warehouses access applies'))

"--- role through the grid"
$rr = Post-Form $admin "/Roles/Create" "/Roles/Create" @(@("Name", "Grid Role $sfx"), @("Capabilities[0]", "catalog.manage"), @("Capabilities[1]", ""), @("Capabilities[2]", "warehouses.view"), @("Capabilities[3]", ""), @("Capabilities[4]", ""))
Check "role form posts and redirects" ($rr.u -eq "/Roles")
$role = (Invoke-RestMethod "$Api/roles?pageSize=100" -Headers $h).items | Where-Object name -eq "Grid Role $sfx"
Check "role stored: Manage implies View, blanks dropped" (($role.capabilities -join ",") -eq "catalog.view,catalog.manage,warehouses.view")
Check "roles list describes access per feature" ((Get-Page $admin "/Roles?search=").c -match 'Catalogue: Manage')

"--- refusal shown on the form, not as the 'not allowed' page"
$mgrEmail = "uimgr$sfx@platform.local"
$null = Invoke-RestMethod -Method Post "$Api/users" -Headers $h -ContentType application/json -Body (@{ name = "UI Manager"; email = $mgrEmail; password = "Manager@12345"; roleIds = @(); scopes = @(); access = @(@{ feature = "users"; level = "Manage"; scopes = @() }, @{ feature = "roles"; level = "View"; scopes = @() }) } | ConvertTo-Json -Depth 5)
$mgr = Sign-In $mgrEmail "Manager@12345"
$deny = Post-Form $mgr "/Users/Create" "/Users/Create" (@(@("Name", "Too Much"), @("Email", "tm$sfx@platform.local"), @("Password", "Grid@123456")) + (Grid @("Manage", "None", "None", "None", "None") @()))
Check "manager giving catalogue Manage (not held): message on the form" (($deny.u -eq "/Users/Create") -and ((Decode $deny.c) -match 'You can only give or remove access you hold yourself') -and ($deny.c -match 'form-panel'))
