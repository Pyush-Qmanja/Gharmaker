$Web = "https://localhost:7331"; $Api = "http://localhost:5310/api"
Add-Type -AssemblyName System.Net.Http
function Token($html) { if ($html -match 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"') { $Matches[1] } }
function Decode($html) { [System.Net.WebUtility]::HtmlDecode($html) }
function Products($html) { ([regex]::Matches($html, 'class="identity__primary"[^>]*>([^<]*)<') | ForEach-Object { Decode $_.Groups[1].Value }) -join " | " }
function TreeLinks($html) { [regex]::Matches($html, '<a class="(category-tree__link[^"]*)" href="/Catalog\?categoryId=([0-9a-f-]{36})[^"]*"><svg[^>]*><use[^>]*></use></svg> ([^<]*)</a>') | ForEach-Object { [pscustomobject]@{ Active = $_.Groups[1].Value -match 'active'; Id = $_.Groups[2].Value; Name = Decode $_.Groups[3].Value } } }
function Check($label, $ok) { "{0}  {1}" -f ($(if ($ok) { "PASS" } else { "FAIL" })), $label }
function Get-Page($session, $path) {
  try { $r = Invoke-WebRequest "$Web$path" -WebSession $session -UseBasicParsing -MaximumRedirection 5; return @{ s = [int]$r.StatusCode; c = $r.Content; t = $r.Headers['Content-Type'] } }
  catch { $resp = $_.Exception.Response; $c = if ($resp) { (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } else { "" }; return @{ s = [int]$resp.StatusCode; c = $c } }
}
function Post-Form($session, $path, $pagePath, $fields, $html) {
  $token = Token $(if ($html) { $html } else { (Get-Page $session $pagePath).c })
  $body = ($fields + @(,@("__RequestVerificationToken", $token)) | ForEach-Object { [uri]::EscapeDataString($_[0]) + "=" + [uri]::EscapeDataString([string]$_[1]) }) -join "&"
  try { $r = Invoke-WebRequest "$Web$path" -Method Post -Body $body -ContentType "application/x-www-form-urlencoded" -WebSession $session -UseBasicParsing -MaximumRedirection 5; return @{ s = [int]$r.StatusCode; c = $r.Content } }
  catch { $resp = $_.Exception.Response; $c = if ($resp) { (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } else { "" }; return @{ s = [int]$resp.StatusCode; c = $c } }
}
function Upload-Form($session, $csv, $name) {
  $token = Token (Get-Page $session "/Catalog/Import").c
  $handler = New-Object System.Net.Http.HttpClientHandler; $handler.CookieContainer = $session.Cookies
  $client = New-Object System.Net.Http.HttpClient($handler)
  $form = New-Object System.Net.Http.MultipartFormDataContent
  $form.Add((New-Object System.Net.Http.StringContent($token)), "__RequestVerificationToken")
  $file = New-Object System.Net.Http.ByteArrayContent(,[Text.Encoding]::UTF8.GetBytes($csv)); $form.Add($file, "file", $name)
  $resp = $client.PostAsync("$Web/Catalog/Import", $form).Result
  return @{ s = [int]$resp.StatusCode; c = $resp.Content.ReadAsStringAsync().Result }
}
function Sign-In($email, $password) {
  $s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
  $r = Post-Form $s "/Account/Login" "/Account/Login" @(@("Email", $email), @("Password", $password))
  return @{ session = $s; page = $r }
}

$admin = Sign-In "admin@platform.local" "Emulator-Admin@2026"
Check "dashboard shows Products and Units modules" (($admin.page.c -match 'module-card__title">Products') -and ($admin.page.c -match 'module-card__title">Units'))

"--- browse"
$cat = (Get-Page $admin.session "/Catalog").c
$tree = TreeLinks $cat
Check "tree shows categories incl. nested TMT Bars and Bricks & Blocks" ((($tree.Name) -contains "Cement") -and (($tree.Name) -contains "TMT Bars") -and (($tree.Name) -contains "Bricks & Blocks") -and ($cat -match 'category-tree__link--active" href="/Catalog"><svg[^>]*><use[^>]*></use></svg> All products'))
Check "products listed with variants chips + Import button" (($cat -match 'Tata Tiscon 550SD') -and ($cat -match 'class="chip">12 mm') -and ($cat -match 'Import from Excel'))
$steelId = ($tree | Where-Object Name -eq "Steel").Id
$steel = (Get-Page $admin.session "/Catalog?categoryId=$steelId").c
Check "Steel page: heading + exactly its 2 products (via sub-category)" (($steel -match 'page-header__title">Steel<') -and ((Products $steel) -eq "JSW Neosteel 550D | Tata Tiscon 550SD"))
Check "Steel link is highlighted in the tree" ((TreeLinks $steel | Where-Object Active).Name -eq "Steel")
$search = (Get-Page $admin.session "/Catalog?search=royale%204").c
Check "search 'royale 4' finds only Asian Paints Royale" ((Products $search) -eq "Asian Paints Royale Luxury Emulsion")
$kajId = [regex]::Match($cat, 'value="([0-9a-f-]{36})"[^>]*>Kajaria<').Groups[1].Value
$kaj = (Get-Page $admin.session "/Catalog?brandId=$kajId").c
Check "brand filter Kajaria: its 2 tiles only, brand kept selected" (((Products $kaj) -eq "Kajaria Eternity 600x600 | Kajaria Eternity 800x800") -and ($kaj -match "value=`"$kajId`" selected"))

"--- product page + converter"
$prodId = [regex]::Match($steel, '/Catalog/Product/([0-9a-f-]{36})"[^>]*>Tata Tiscon 550SD').Groups[1].Value
$prod = (Get-Page $admin.session "/Catalog/Product/$prodId").c
Check "breadcrumb Catalogue > Steel > TMT Bars" (($prod -match '>Steel</a>') -and ($prod -match '>TMT Bars</a>'))
Check "SKU rows with own conversions '1 PCS = 10.66 KG'" (($prod -match 'TMT-TISCON-12MM') -and ($prod -match '1 PCS = 10.66 KG'))
$sku12 = [regex]::Match($prod, 'value="([0-9a-f-]{36})"[^>]*>12 mm<').Groups[1].Value
$conv = (Get-Page $admin.session "/Catalog/Product/$prodId`?skuId=$sku12&value=25&from=PCS&to=KG").c
Check "converter: 25 PCS of 12 mm = 266.5 KG" ($conv -match '25 PCS =</span>\s*<strong>266.5 KG</strong>')
$conv2 = (Get-Page $admin.session "/Catalog/Product/$prodId`?skuId=$sku12&value=2&from=TONNE&to=PCS").c
Check "converter: 2 TONNE of 12 mm = 187.6173 PCS" ($conv2 -match '2 TONNE =</span>\s*<strong>187.6173 PCS</strong>')
$bad = (Get-Page $admin.session "/Catalog/Product/$prodId`?skuId=$sku12&value=1&from=KG&to=SQFT").c
Check "converter: KG -> SQFT shows a clear error" ($bad -match 'converter__error[\s\S]{0,400}?KG cannot be converted to SQFT')

"--- import"
$imp = (Get-Page $admin.session "/Catalog/Import").c
Check "import page: steps, column help, template link" (($imp -match 'Download Excel template') -and ($imp -match 'SKU Code') -and ($imp -match 'TONNE=20; TRUCK=400'))
$tpl = Get-Page $admin.session "/Catalog/Template"
Check "template downloads as xlsx" (($tpl.s -eq 200) -and ($tpl.t -match 'spreadsheetml'))
$badCsv = "Category,Brand,Product,HSN,SKU Code,Variant,Base Unit,Conversions,Active`nFixings > Tie Wire,Wirex,Wirex Tie Wire,7217,UIW-20G,20 gauge,KGS,,Yes`nFixings > Tie Wire,Wirex,Wirex Tie Wire,7217,UIW-18G,18 gauge,KG,COIL=25,Yes"
$p1 = Upload-Form $admin.session $badCsv "bad.csv"
$p1d = Decode $p1.c
Check "preview with errors: messages shown, no Confirm button" (($p1d -match "Unknown base unit 'KGS'") -and ($p1d -match "Unknown unit 'COIL'") -and ($p1d -notmatch 'Confirm and save') -and ($p1d -match 'Nothing can be saved'))
$goodCsv = "Category,Brand,Product,HSN,SKU Code,Variant,Base Unit,Conversions,Active`nFixings > Tie Wire,Wirex,Wirex Tie Wire,7217,UIW-20G,20 gauge,KG,BUNDLE=25,Yes`nFixings > Tie Wire,Wirex,Wirex Tie Wire,7217,UIW-18G,18 gauge,KG,BUNDLE=25,Yes`nCement > PPC,Dalmia,Dalmia DSP PPC,2523,CEM-DAL-PPC-50KG,50 kg bag,BAG,TONNE=20; TRUCK=400,Yes"
$p2 = Upload-Form $admin.session $goodCsv "good.csv"
Check "clean preview: 2 new, 1 update, new category + brand listed, Confirm button" (($p2.c -match 'New SKUs</span><span class="stat__value">2<') -and ($p2.c -match 'SKUs updated</span><span class="stat__value">1<') -and ($p2.c -match 'class="chip">Fixings &gt; Tie Wire') -and ($p2.c -match 'class="chip">Wirex') -and ($p2.c -match 'Confirm and save'))
$importId = [regex]::Match($p2.c, 'name="importId" value="([0-9a-f-]{36})"').Groups[1].Value
# PowerShell 5.1 drops cookies set on a redirect, so follow it by hand.
$tok = Token $p2.c
$null = try { Invoke-WebRequest "$Web/Catalog/Commit" -Method Post -Body "importId=$importId&__RequestVerificationToken=$([uri]::EscapeDataString($tok))" -ContentType "application/x-www-form-urlencoded" -WebSession $admin.session -UseBasicParsing -MaximumRedirection 0 -ErrorAction SilentlyContinue } catch { }
$done = Get-Page $admin.session "/Catalog"
$flash = Decode ([regex]::Match($done.c, 'toast-message__text">([^<]*)<').Groups[1].Value)
"      flash: $flash"
Check "confirm saves and returns to catalogue with summary" (($flash -eq 'Import saved: 2 SKUs added, 1 SKU updated, 1 new product, 2 new categories, 1 new brand.') -and ((TreeLinks $done.c).Name -contains "Tie Wire"))
$again = Post-Form $admin.session "/Catalog/Commit" $null @(@("importId", $importId)) $p2.c
Check "confirming the same preview again: 'expired' message" ($again.c -match 'That preview has expired')

"--- units screen"
$units = (Get-Page $admin.session "/Uoms?pageSize=50").c
Check "units list: BAG depends on product, TONNE = 1000" (($units -match 'code-tag">BAG<') -and ($units -match 'Depends on product') -and ($units -match '>\s*1000\s*<'))
Check "unit form: Measures drop-down" ((Get-Page $admin.session "/Uoms/Create").c -match '<select[^>]*name="Dimension"')

"--- a catalogue viewer cannot import"
$t = (Invoke-RestMethod -Method Post "$Api/auth/login" -ContentType application/json -Body '{"email":"admin@platform.local","password":"Emulator-Admin@2026"}').accessToken
$h = @{ Authorization = "Bearer $t" }
$role = Invoke-RestMethod -Method Post "$Api/roles" -Headers $h -ContentType application/json -Body (@{ name = "Catalogue Viewer $(Get-Random)"; capabilities = @("catalog.view") } | ConvertTo-Json)
$email = "viewer$(Get-Random)@platform.local"
$null = Invoke-RestMethod -Method Post "$Api/users" -Headers $h -ContentType application/json -Body (@{ name = "Sales Desk"; email = $email; password = "Sales@12345"; roleIds = @($role.id); scopes = @(@{ scopeType = "Global" }) } | ConvertTo-Json -Depth 4)
$viewer = Sign-In $email "Sales@12345"
$vcat = (Get-Page $viewer.session "/Catalog").c
Check "viewer can browse, no Import button" (($vcat -match 'Tata Tiscon 550SD') -and ($vcat -notmatch 'Import from Excel'))
$vup = Upload-Form $viewer.session $goodCsv "good.csv"
Check "viewer uploading anyway gets 'not allowed' (403)" (($vup.s -eq 403) -and ($vup.c -match 'have access to this'))
