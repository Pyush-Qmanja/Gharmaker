$Web = "https://localhost:7331"; $Api = "http://localhost:5310/api"
Add-Type -AssemblyName System.Net.Http
function Token($html) { if ($html -match 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"') { $Matches[1] } }
function Decode($html) { [System.Net.WebUtility]::HtmlDecode($html) }
function Check($label, $ok) { "{0}  {1}" -f ($(if ($ok) { "PASS" } else { "FAIL" })), $label }
function Get-Page($session, $path) {
  try { $r = Invoke-WebRequest "$Web$path" -WebSession $session -UseBasicParsing -MaximumRedirection 5; return @{ s = [int]$r.StatusCode; c = $r.Content; u = $r.BaseResponse.ResponseUri.AbsolutePath } }
  catch { $resp = $_.Exception.Response; $c = if ($resp) { (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } else { "" }; return @{ s = [int]$resp.StatusCode; c = $c } }
}
function Post-Form($session, $path, $pagePath, $fields) {
  $token = Token (Get-Page $session $pagePath).c
  $body = ($fields + @(,@("__RequestVerificationToken", $token)) | ForEach-Object { [uri]::EscapeDataString($_[0]) + "=" + [uri]::EscapeDataString([string]$_[1]) }) -join "&"
  try { $r = Invoke-WebRequest "$Web$path" -Method Post -Body $body -ContentType "application/x-www-form-urlencoded" -WebSession $session -UseBasicParsing -MaximumRedirection 5; return @{ s = [int]$r.StatusCode; c = $r.Content; u = $r.BaseResponse.ResponseUri.AbsolutePath } }
  catch { $resp = $_.Exception.Response; $c = if ($resp) { (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } else { "" }; return @{ s = [int]$resp.StatusCode; c = $c } }
}
function Sign-In($email, $password) { $s = New-Object Microsoft.PowerShell.Commands.WebRequestSession; $null = Post-Form $s "/Account/Login" "/Account/Login" @(@("Email", $email), @("Password", $password)); return $s }
function Lines($rows) { $f = @(); for ($i = 0; $i -lt 5; $i++) { $r = if ($i -lt $rows.Count) { $rows[$i] } else { @("", "", "") }; $f += ,@("Lines[$i].SkuCode", $r[0]); $f += ,@("Lines[$i].Quantity", $r[1]); $f += ,@("Lines[$i].Uom", $r[2]) }; return $f }

$t = (Invoke-RestMethod -Method Post "$Api/auth/login" -ContentType application/json -Body '{"email":"admin@platform.local","password":"Emulator-Admin@2026"}').accessToken
$h = @{ Authorization = "Bearer $t" }
$sfx = Get-Random -Maximum 99999
function NewWh($code) { Invoke-RestMethod -Method Post "$Api/warehouses" -Headers $h -ContentType application/json -Body (@{ code = $code; name = "UI $code"; type = "Owned"; address = @{ line1 = "1"; city = "Pune"; state = "MH"; pincode = "411001" } } | ConvertTo-Json -Depth 4) }
$A = NewWh "UIA-$sfx"; $B = NewWh "UIB-$sfx"
$CEM = "CEM-ACC-PPC-50KG"

$admin = Sign-In "admin@platform.local" "Emulator-Admin@2026"
$form = (Get-Page $admin "/Receipts/Create").c
Check "receipt form: warehouse picker, SKU and unit pick lists, 5 line rows, add-line template" (($form -match "value=`"$($A.id)`"") -and ($form -match '<datalist id="sku-options">') -and ($form -match "value=`"$CEM`"") -and ($form -match 'name="Lines\[4\]\.SkuCode"') -and ($form -match 'data-line-template'))

"--- post a receipt through the form (blank rows ignored)"
$r = Post-Form $admin "/Receipts/Create" "/Receipts/Create" (@(@("WarehouseId", $A.id), @("SupplierName", "ACC Dealer Pune"), @("SupplierReferenceNo", "INV-$sfx"), @("Remarks", "")) + (Lines @(,@($CEM, "1.5", "TONNE"))))
Check "redirects to the new receipt's page" ($r.u -match '^/Receipts/Details/')
Check "receipt page shows reference, supplier and 1.5 TONNE = 30 BAG" (((Decode $r.c) -match 'GRN-\d{4}-\d{6}') -and ($r.c -match 'ACC Dealer Pune') -and ((Decode $r.c) -match '1.5 <span class="qty__uom">TONNE') -and ($r.c -match '<strong>30</strong> <span class="qty__uom">BAG'))

"--- validation on the form"
$bad = Post-Form $admin "/Receipts/Create" "/Receipts/Create" (@(@("WarehouseId", $A.id), @("SupplierName", "X")) + (Lines @(@("NOPE-$sfx", "1", "BAG"), @($CEM, "1", "SQFT"))))
Check "unknown SKU and wrong unit shown on their lines" (($bad.u -eq "/Receipts/Create") -and ((Decode $bad.c) -match "No SKU with code NOPE-$sfx") -and ((Decode $bad.c) -match "cannot be counted in SQFT"))
Check "entered lines kept on the form" ($bad.c -match "value=`"NOPE-$sfx`"")
$none = Post-Form $admin "/Receipts/Create" "/Receipts/Create" (@(@("WarehouseId", $A.id), @("SupplierName", "X")) + (Lines @()))
Check "no lines: 'Add at least one line'" ((Decode $none.c) -match 'Add at least one line')

"--- transfer: send, then receive with the button"
$tr = Post-Form $admin "/Transfers/Create" "/Transfers/Create" (@(@("WarehouseId", $A.id), @("ToWarehouseId", $B.id), @("Remarks", "MH-12 AB 1234")) + (Lines @(,@($CEM, "10", "BAG"))))
Check "transfer page: in transit with a Receive button" (($tr.u -match '^/Transfers/Details/') -and ($tr.c -match 'In transit') -and ($tr.c -match "Receive into UIB-$sfx"))
$trId = ($tr.u -split '/')[-1]
$rc = Post-Form $admin "/Transfers/Receive/$trId" "/Transfers/Details/$trId" @()
Check "after receiving: status Received, no Receive button" (($rc.c -match 'status-badge--active">Received') -and ($rc.c -notmatch 'Receive into'))
$over = Post-Form $admin "/Transfers/Create" "/Transfers/Create" (@(@("WarehouseId", $A.id), @("ToWarehouseId", $B.id)) + (Lines @(,@($CEM, "999", "BAG"))))
Check "sending too much: 'Not enough stock' on the form" (($over.u -eq "/Transfers/Create") -and ((Decode $over.c) -match 'Not enough stock'))

"--- adjustment and reversal"
$adj = Post-Form $admin "/Adjustments/Create" "/Adjustments/Create" (@(@("WarehouseId", $A.id), @("Reason", "Damage"), @("Remarks", "Torn bags")) + (Lines @(,@($CEM, "2", "BAG"))))
Check "damage posted: lines show -2 BAG" (($adj.u -match '^/Adjustments/Details/') -and ((Decode $adj.c) -match '-2 <span class="qty__uom">BAG'))
Check "adjustment reasons offered are only the three" ((Get-Page $admin "/Adjustments/Create").c -notmatch 'value="GrnReceipt"')
$adjId = ($adj.u -split '/')[-1]
$rev = Post-Form $admin "/Adjustments/Reverse/$adjId" "/Adjustments/Details/$adjId" @(,@("Remarks", "Bags were fine"))
Check "reversed: lands on the REV document" (((Decode $rev.c) -match 'REV-\d{4}-\d{6}') -and ($rev.c -match 'Reversal of'))
$orig = (Get-Page $admin "/Adjustments/Details/$adjId").c
Check "original shows Reversed and no reverse form" (($orig -match 'status-badge--inactive">Reversed') -and ($orig -notmatch 'Reverse this document'))

"--- stock screens"
$stock = (Get-Page $admin "/Stock?warehouseId=$($A.id)").c
Check "stock list: A has 20 BAG (30 in, 10 out, 2 damaged, 2 back)" ((Decode $stock) -match '<span class="qty">20</span> <span class="qty__uom">BAG')
Check "stock list offers Receive, Transfer, Adjust, Opening, Ledger check" (($stock -match 'Receive goods') -and ($stock -match '/Transfers/Create') -and ($stock -match '/Adjustments/Create') -and ($stock -match '/Stock/Opening') -and ($stock -match '/Stock/Reconcile'))
$mov = (Get-Page $admin "/Stock/Movements?warehouseId=$($A.id)").c
Check "movements list: reasons and signed changes" (((Decode $mov) -match 'Goods receipt') -and ((Decode $mov) -match 'Transfer out') -and ((Decode $mov) -match 'qty--out">.10') -and ($mov -match '>Reversal<'))
Check "ledger check page: every balance agrees" ((Get-Page $admin "/Stock/Reconcile").c -match 'Every balance agrees with its ledger')

"--- opening stock import through the web"
$token = Token (Get-Page $admin "/Stock/Opening").c
$handler = New-Object System.Net.Http.HttpClientHandler; $handler.CookieContainer = $admin.Cookies
$client = New-Object System.Net.Http.HttpClient($handler)
$mf = New-Object System.Net.Http.MultipartFormDataContent
$mf.Add((New-Object System.Net.Http.StringContent($token)), "__RequestVerificationToken")
$csv = "Warehouse Code,SKU Code,Quantity,Unit`nUIB-$sfx,TMT-JSW-08MM,2,TONNE"
$mf.Add((New-Object System.Net.Http.ByteArrayContent(,[Text.Encoding]::UTF8.GetBytes($csv))), "file", "opening.csv")
$pv = $client.PostAsync("$Web/Stock/Opening", $mf).Result.Content.ReadAsStringAsync().Result
Check "opening preview: 1 row ready, 2 TONNE = 2,000 KG, confirm button" (($pv -match 'Rows ready</span><span class="stat__value">1<') -and ((Decode $pv) -match '2,000 KG') -and ($pv -match 'Confirm and post'))
$importId = [regex]::Match($pv, 'name="importId" value="([0-9a-f-]{36})"').Groups[1].Value
$null = try { Invoke-WebRequest "$Web/Stock/OpeningCommit" -Method Post -Body "importId=$importId&__RequestVerificationToken=$([uri]::EscapeDataString((Token $pv)))" -ContentType "application/x-www-form-urlencoded" -WebSession $admin -UseBasicParsing -MaximumRedirection 0 -ErrorAction SilentlyContinue } catch { }
Check "opening posted: B holds 2,000 KG of 8 mm" ((Decode (Get-Page $admin "/Stock?warehouseId=$($B.id)&search=TMT-JSW-08MM").c) -match '<span class="qty">2,000</span>')

"--- audit log"
$au = (Get-Page $admin "/Audit?entity=StockDocument").c
Check "audit log lists stock documents with who and action" (($au -match 'status-badge--new">Created') -and ((Decode $au) -match 'GRN-'))
$auId = [regex]::Match($au, '/Audit/Details/([0-9a-f-]{36})').Groups[1].Value
Check "audit entry shows fields with before/after" ((Get-Page $admin "/Audit/Details/$auId").c -match 'audit-changes__value--after')

"--- a store keeper sees only what they may do"
$kEmail = "uikeeper$sfx@platform.local"
$null = Invoke-RestMethod -Method Post "$Api/users" -Headers $h -ContentType application/json -Body (@{ name = "UI Keeper"; email = $kEmail; password = "Keeper@12345"; roleIds = @(); scopes = @();
  access = @(@{ feature = "stock"; level = "View"; scopes = @(@{ scopeType = "Warehouse"; scopeId = $A.id }) }, @{ feature = "receipts"; level = "Manage"; scopes = @(@{ scopeType = "Warehouse"; scopeId = $A.id }) }) } | ConvertTo-Json -Depth 6)
$k = Sign-In $kEmail "Keeper@12345"
$ks = (Get-Page $k "/Stock").c
Check "keeper's stock page: Receive only (no Transfer/Adjust/Ledger check)" (($ks -match 'Receive goods') -and ($ks -notmatch '/Transfers/Create') -and ($ks -notmatch '/Adjustments/Create') -and ($ks -notmatch '/Stock/Reconcile'))
Check "keeper's menu: Stock and Goods receipts, no Transfers / Audit log" (($ks -match 'href="/Receipts"') -and ($ks -notmatch 'href="/Transfers"') -and ($ks -notmatch 'href="/Audit"'))
$kf = (Get-Page $k "/Receipts/Create").c
Check "keeper's receipt form offers only warehouse A" (($kf -match "value=`"$($A.id)`"") -and ($kf -notmatch "value=`"$($B.id)`""))
Check "keeper opening adjustments form -> not allowed page" ((Get-Page $k "/Adjustments/Create").c -match "have access to this")
