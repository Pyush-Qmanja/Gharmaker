# Signs in to the test Web app and saves signed-in pages as static snapshots under the test build's wwwroot/_preview,
# so they can be viewed in a browser from the same origin without typing credentials there.
param([string[]]$Paths = @("/", "/Users", "/Users/Create", "/Roles", "/Roles/Create", "/Warehouses", "/Catalog", "/Uoms", "/Catalog/Import"))
$Web = "https://localhost:7331"
$out = (Join-Path $PSScriptRoot "..\..\src\Platform.Web\wwwroot\_preview")
New-Item -ItemType Directory -Force $out | Out-Null
$s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$login = Invoke-WebRequest "$Web/Account/Login" -WebSession $s -UseBasicParsing
$token = [regex]::Match($login.Content, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
Invoke-WebRequest "$Web/Account/Login" -Method Post -WebSession $s -UseBasicParsing -Body @{ Email = "admin@platform.local"; Password = "Emulator-Admin@2026"; __RequestVerificationToken = $token } | Out-Null
foreach ($p in $Paths) {
  $r = Invoke-WebRequest "$Web$p" -WebSession $s -UseBasicParsing
  $name = if ($p -eq "/") { "home" } else { ($p.Trim('/') -replace '[/?=&]', '-').ToLower() }
  [IO.File]::WriteAllText("$out\$name.html", $r.Content, [Text.Encoding]::UTF8)
  "{0,-22} {1} -> _preview/{2}.html" -f $p, $r.StatusCode, $name
}

