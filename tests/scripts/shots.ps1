# Screenshots pages of the test Web app with headless Edge into scratchpad\shots.
param([string[]]$Pages = @("Account/Login", "_preview/home.html", "_preview/users.html", "_preview/users-create.html", "_preview/roles.html", "_preview/roles-create.html", "_preview/warehouses.html", "_preview/catalog.html", "_preview/uoms.html", "_preview/catalog-import.html"),
      [int]$Width = 1440, [int]$Height = 1800)
$edge = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
$dir = (Join-Path $PSScriptRoot ".work\shots")
$profile = (Join-Path $PSScriptRoot ".work\edge-profile")
New-Item -ItemType Directory -Force $dir | Out-Null
foreach ($p in $Pages) {
  $name = ($p -replace '^_preview/', '' -replace '\.html$', '' -replace '[/?=&]', '-').ToLower() + "-$Width.png"
  $file = Join-Path $dir $name
  & $edge --headless=new --disable-gpu --hide-scrollbars --user-data-dir="$profile" --window-size="$Width,$Height" --virtual-time-budget=4000 --screenshot="$file" "https://localhost:7331/$p" 2>$null | Out-Null
  Start-Sleep -Milliseconds 300
  "{0} {1}" -f $name, (Test-Path $file)
}
