$Web = "https://localhost:7331"
function Token($html) { if ($html -match 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"') { $Matches[1] } }
function Check($label, $ok) { "{0}  {1}" -f ($(if ($ok) { "PASS" } else { "FAIL" })), $label }
function Get-Page($session, $path) {
  try { $r = Invoke-WebRequest "$Web$path" -WebSession $session -UseBasicParsing -MaximumRedirection 5; return @{ s = [int]$r.StatusCode; c = $r.Content } }
  catch { $resp = $_.Exception.Response; $c = if ($resp) { (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } else { "" }; return @{ s = [int]$resp.StatusCode; c = $c } }
}
function Post-Form($session, $path, $pagePath, $fields) {
  $token = Token (Get-Page $session $pagePath).c
  $body = ($fields + @(,@("__RequestVerificationToken", $token)) | ForEach-Object { [uri]::EscapeDataString($_[0]) + "=" + [uri]::EscapeDataString($_[1]) }) -join "&"
  try { $r = Invoke-WebRequest "$Web$path" -Method Post -Body $body -ContentType "application/x-www-form-urlencoded" -WebSession $session -UseBasicParsing -MaximumRedirection 5; return @{ s = [int]$r.StatusCode; c = $r.Content } }
  catch { $resp = $_.Exception.Response; return @{ s = [int]$resp.StatusCode; c = "" } }
}
function Sign-In($email, $password) {
  $s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
  $r = Post-Form $s "/Account/Login" "/Account/Login" @(@("Email", $email), @("Password", $password))
  return @{ session = $s; page = $r }
}

$admin = Sign-In "admin@platform.local" "Emulator-Admin@2026"
Check "admin signs in, lands on dashboard" ($admin.page.c -match "Good (morning|afternoon|evening), Administrator")
Check "admin sees Users and Roles tiles" (($admin.page.c -match 'module-card__title">Users') -and ($admin.page.c -match 'module-card__title">Roles'))

$roleForm = (Get-Page $admin.session "/Roles/Create").c
Check "role form shows the grouped access grid" (($roleForm -match 'access-grid__group"><th colspan="2" scope="colgroup">Catalogue') -and ($roleForm -match 'value="brands.view"'))

$bad = Post-Form $admin.session "/Roles/Create" "/Roles/Create" @(@("Name", "Empty Role"))
Check "role without capabilities shows field error" ($bad.c -match "Choose at least one capability")

$roleName = "Catalog Viewer $(Get-Random)"
$made = Post-Form $admin.session "/Roles/Create" "/Roles/Create" @(@("Name", $roleName), @("Capabilities", "brands.view"))
Check "create role via form" ($made.c -match "Role created")
Check "roles list describes access" ($made.c -match 'class="chip">Brands: View')

$userForm = (Get-Page $admin.session "/Users/Create").c
$roleId = if ($userForm -match 'value="([0-9a-f-]{36})"[^>]*>\s*<label[^>]*>[^<]*?' + [regex]::Escape($roleName)) { $Matches[1] } else { ([regex]::Matches($userForm, 'name="RoleIds" value="([0-9a-f-]{36})"') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Last 1) }
Check "user form offers the new role" ($userForm -match [regex]::Escape($roleName))
Check "user form has email + password + global scope" (($userForm -match 'name="Email"') -and ($userForm -match 'type="password"') -and ($userForm -match 'value="Global"'))

$suffix = Get-Random
$email = "clerk$suffix@platform.local"
$created = Post-Form $admin.session "/Users/Create" "/Users/Create" @(@("Name", "Store Clerk"), @("Email", $email), @("Password", "Clerk@12345"), @("RoleIds", $roleId), @("Scopes[0].ScopeType", "Global"))
Check "create user via form" ($created.c -match "User created")
Check "users list shows role chip + Everywhere" (($created.c -match ('class="chip">' + [regex]::Escape($roleName))) -and ($created.c -match "Everywhere"))

$dup = Post-Form $admin.session "/Users/Create" "/Users/Create" @(@("Name", "Dup"), @("Email", $email), @("Password", "Clerk@12345"))
Check "duplicate email error shown on the field" ($dup.c -match "This email already has an account")

$clerk = Sign-In $email "Clerk@12345"
Check "clerk signs in" ($clerk.page.c -match "Good (morning|afternoon|evening), Store")
Check "clerk sees only the Brands tile" (($clerk.page.c -match 'module-card__title">Brands') -and ($clerk.page.c -notmatch 'module-card__title">Users'))
Check "clerk menu hides Users/Roles" ($clerk.page.c -notmatch 'asp-controller|href="/Users"')
$forbid = Get-Page $clerk.session "/Users"
Check "clerk opening /Users gets 'not allowed' page (403)" (($forbid.s -eq 403) -and ($forbid.c -match "have access to this"))
Check "clerk can open /Brands" ((Get-Page $clerk.session "/Brands").s -eq 200)

