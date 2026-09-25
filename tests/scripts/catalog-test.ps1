param([string]$Base = "http://localhost:5310/api")
Add-Type -AssemblyName System.Net.Http
function Call($m, $u, $body, $tok) {
  try {
    $h = @{}; if ($tok) { $h.Authorization = "Bearer $tok" }
    $a = @{ Method = $m; Uri = "$Base/$u"; Headers = $h; UseBasicParsing = $true }
    if ($null -ne $body) { $a.ContentType = "application/json"; $a.Body = ($body | ConvertTo-Json -Depth 6) }
    $r = Invoke-WebRequest @a
    return @{ s = [int]$r.StatusCode; d = ($(if ($r.Content -and $r.Headers['Content-Type'] -match 'json') { $r.Content | ConvertFrom-Json } else { $r.RawContentLength })) }
  } catch {
    $resp = $_.Exception.Response
    $c = if ($resp) { (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } else { $_.Exception.Message }
    return @{ s = [int]$resp.StatusCode; d = $c }
  }
}
function Upload($u, $csv, $name, $tok) {
  $client = New-Object System.Net.Http.HttpClient
  $client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", $tok)
  $form = New-Object System.Net.Http.MultipartFormDataContent
  $file = New-Object System.Net.Http.ByteArrayContent(,[Text.Encoding]::UTF8.GetBytes($csv))
  $form.Add($file, "file", $name)
  $resp = $client.PostAsync("$Base/$u", $form).Result
  $text = $resp.Content.ReadAsStringAsync().Result
  return @{ s = [int]$resp.StatusCode; d = ($text | ConvertFrom-Json) }
}
function Check($label, $actual, $expected) { "{0}  {1,-62} got {2}, want {3}" -f ($(if ("$actual" -eq "$expected") { "PASS" } else { "FAIL" })), $label, $actual, $expected }
function Find-Cat($nodes, $name) { foreach ($n in $nodes) { if ($n.name -eq $name) { return $n }; $c = Find-Cat $n.children $name; if ($c) { return $c } } }

$t = (Call POST "auth/login" @{ email = "admin@platform.local"; password = "Emulator-Admin@2026" }).d.accessToken
"--- seeded data"
Check "units seeded" (Call GET "uoms?pageSize=100" $null $t).d.totalCount 20
$tree = (Call GET "catalog/categories" $null $t).d
Check "top-level categories" (($tree | ForEach-Object name) -join ", ") "Cement, Steel, Bricks & Blocks, Sand & Aggregate, Tiles, Plumbing, Paint"
Check "Steel has TMT Bars" (($tree | Where-Object name -eq "Steel").children.name) "TMT Bars"
Check "all products" (Call GET "catalog/products?pageSize=100" $null $t).d.totalCount 20

"--- browse filters"
$steel = Find-Cat $tree "Steel"
$tmt = Call GET "catalog/products?categoryId=$($steel.id)" $null $t
Check "Steel (parent) includes TMT products" "$($tmt.d.totalCount): $(($tmt.d.items.name) -join ', ')" "2: JSW Neosteel 550D, Tata Tiscon 550SD"
$cement = Find-Cat $tree "Cement"
Check "Cement (OPC + PPC) products" (Call GET "catalog/products?categoryId=$($cement.id)" $null $t).d.totalCount 5
$kaj = (Call GET "brands?search=kajaria" $null $t).d.items[0].id
Check "brand filter Kajaria" (Call GET "catalog/products?brandId=$kaj" $null $t).d.totalCount 2
Check "search 'tisc' (partial word)" ((Call GET "catalog/products?search=tisc" $null $t).d.items.name -join ", ") "Tata Tiscon 550SD"
Check "search 'royale 20' (two words)" ((Call GET "catalog/products?search=royale%2020" $null $t).d.items.name -join ", ") "Asian Paints Royale Luxury Emulsion"
Check "search by category word 'cement'" (Call GET "catalog/products?search=cement" $null $t).d.totalCount 5
Check "search by SKU code 'tmt-jsw-12mm'" ((Call GET "catalog/products?search=tmt-jsw-12mm" $null $t).d.items.name -join ", ") "JSW Neosteel 550D"
Check "search + category: 'ultratech' under Steel" (Call GET "catalog/products?search=ultratech&categoryId=$($steel.id)" $null $t).d.totalCount 0
$list = (Call GET "catalog/products?categoryId=$($steel.id)" $null $t).d.items | Where-Object name -eq "Tata Tiscon 550SD"
Check "list shows variants" ($list.variants -join ", ") "8 mm, 10 mm, 12 mm, 16 mm, 20 mm"

"--- product detail and unit conversion (P7)"
$tiscon = (Call GET "catalog/products/$($list.id)" $null $t).d
Check "breadcrumb" (($tiscon.breadcrumb.name) -join " > ") "Steel > TMT Bars"
$sku12 = $tiscon.skus | Where-Object code -eq "TMT-TISCON-12MM"
Check "12 mm units (own + standard)" (($sku12.units.uom) -join ", ") "KG, PCS, BUNDLE, QUINTAL, TONNE, THOUSAND"
function Conv($skuId, $v, $from, $to) { $r = Call GET "catalog/skus/$skuId/convert?value=$v&from=$from&to=$to" $null $t; if ($r.s -eq 200) { "$($r.d.to.value) $($r.d.to.uom)" } else { $r.s } }
Check "TMT 12mm: 25 PCS -> KG" (Conv $sku12.id 25 PCS KG) "266.5 KG"
Check "TMT 12mm: 25 PCS -> TONNE" (Conv $sku12.id 25 PCS TONNE) "0.2665 TONNE"
Check "TMT 12mm: 2 TONNE -> BUNDLE" (Conv $sku12.id 2 TONNE BUNDLE) "37.5235 BUNDLE"
$cemId = ((Call GET "catalog/products/$((Call GET 'catalog/products?search=ultratech%20opc' $null $t).d.items[0].id)" $null $t).d.skus[0]).id
Check "Cement: 3 TONNE -> BAG" (Conv $cemId 3 TONNE BAG) "60 BAG"
Check "Cement: 1000 KG -> BAG (via TONNE)" (Conv $cemId 1000 KG BAG) "20 BAG"
Check "Cement: 1 TRUCK -> TONNE" (Conv $cemId 1 TRUCK TONNE) "20 TONNE"
Check "Cement: BAG -> SQFT is refused (400)" (Conv $cemId 1 BAG SQFT) 400
$tileId = ((Call GET "catalog/products/$((Call GET 'catalog/products?search=eternity%20600x600' $null $t).d.items[0].id)" $null $t).d.skus[0]).id
Check "Tile 600x600: 500 SQFT -> BOX" (Conv $tileId 500 SQFT BOX) "32.25 BOX"
Check "Tile 600x600: 100 SQM -> BOX (via SQFT)" (Conv $tileId 100 SQM BOX) "69.4272 BOX"
$sandId = ((Call GET "catalog/products/$((Call GET 'catalog/products?search=manufactured' $null $t).d.items[0].id)" $null $t).d.skus[0]).id
Check "M-Sand: 2 BRASS -> TONNE" (Conv $sandId 2 BRASS TONNE) "9.0621 TONNE"

"--- import: template, preview with errors"
$tpl = Call GET "catalog/import/template" $null $t
Check "template download (xlsx bytes > 5000)" ($tpl.s -eq 200 -and $tpl.d -gt 5000) True
$bad = @"
Category,Brand,Product,HSN,SKU Code,Variant,Base Unit,Conversions,Active
Hardware > Binding Wire,Tata Wiron,Tata Wiron Binding Wire,7217,WIRE-TATA-20G,20 gauge 25 kg coil,KG,COIL=25,Yes
Hardware > Binding Wire,Tata Wiron,Tata Wiron Binding Wire,7217,WIRE-TATA-18G,18 gauge,KGS,,Yes
Hardware > Binding Wire,Tata Wiron,Tata Wiron Binding Wire,72,WIRE-TATA-16G,16 gauge,KG,,Yes
Hardware > Binding Wire,Tata Wiron,Tata Wiron Binding Wire,7217,WIRE-TATA-16G,16 gauge again,KG,,Yes
Hardware > Nails,Tata Wiron,Tata Wiron Binding Wire,7217,WIRE-TATA-14G,14 gauge,KG,,Yes
Cement > OPC,Ambuja,Ambuja Cool Walls,2523,CEM-ULT-OPC53-50KG,50 kg bag,BAG,TONNE=20,Maybe
"@
$p1 = Upload "catalog/import/preview" $bad "bad.csv" $t
Check "preview with errors: status" $p1.s 200
Check "preview: canCommit false, 6 error rows (incl. unknown COIL)" "$($p1.d.canCommit) $($p1.d.errorRows)" "False 6"
foreach ($row in $p1.d.rows) { "      row $($row.rowNumber): $($row.messages -join ' | ')" }
Check "commit of an errored preview -> 422" (Call POST "catalog/import/$($p1.d.importId)/commit" $null $t).s 422

"--- import: clean file, commit, re-import"
$good = @"
Category,Brand,Product,HSN,SKU Code,Variant,Base Unit,Conversions,Active
Hardware > Binding Wire,Tata Wiron,Tata Wiron Binding Wire,7217,WIRE-TATA-20G,20 gauge,KG,BUNDLE=25,Yes
Hardware > Binding Wire,Tata Wiron,Tata Wiron Binding Wire,7217,WIRE-TATA-18G,18 gauge,KG,BUNDLE=25,Yes
Steel > TMT Bars,Tata Tiscon,Tata Tiscon 550SD,7214,TMT-TISCON-25MM,25 mm,KG,PCS=46.20,Yes
Cement > OPC,UltraTech,UltraTech OPC 53 Grade,2523,CEM-ULT-OPC53-50KG,50 kg bag,BAG,TONNE=20; TRUCK=500,Yes
"@
$p2 = Upload "catalog/import/preview" $good "good.csv" $t
Check "clean preview: canCommit, new/update" "$($p2.d.canCommit) new=$($p2.d.newSkus) upd=$($p2.d.updatedSkus)" "True new=3 upd=1"
Check "preview lists new category + brand" "$($p2.d.newCategories -join ', ') | $($p2.d.newBrands -join ', ')" "Hardware, Hardware > Binding Wire | Tata Wiron"
$c2 = Call POST "catalog/import/$($p2.d.importId)/commit" $null $t
Check "commit" "$($c2.s) cat=$($c2.d.categoriesCreated) brand=$($c2.d.brandsCreated) prod=$($c2.d.productsCreated) sku+=$($c2.d.skusCreated) sku~=$($c2.d.skusUpdated)" "200 cat=2 brand=1 prod=1 sku+=3 sku~=1"
Check "commit same preview twice -> 404" (Call POST "catalog/import/$($p2.d.importId)/commit" $null $t).s 404
Check "Tiscon now has 25 mm" (((Call GET "catalog/products?search=tiscon" $null $t).d.items[0].variants) -contains "25 mm") True
Check "cement truck now 500 bags: 1 TRUCK -> BAG" (Conv $cemId 1 TRUCK BAG) "500 BAG"
Check "new product searchable 'wiron'" (Call GET "catalog/products?search=wiron" $null $t).d.totalCount 1
$p3 = Upload "catalog/import/preview" $good "good.csv" $t
Check "re-import same file = all updates" "new=$($p3.d.newSkus) upd=$($p3.d.updatedSkus) newCats=$($p3.d.newCategories.Count)" "new=0 upd=4 newCats=0"
Check "wrong file type -> 400" (Upload "catalog/import/preview" "hello" "notes.txt" $t).s 400
Check "missing columns -> 400" (Upload "catalog/import/preview" "Name,Price`nX,1" "x.csv" $t).s 400
