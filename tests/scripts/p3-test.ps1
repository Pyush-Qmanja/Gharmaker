param([string]$Base = "http://localhost:5310/api")
$ErrorActionPreference = "Continue"
$script:ShopBodies = New-Object System.Collections.Generic.List[string]
function Call($m, $u, $body, $tok) {
  try {
    $h = @{}; if ($tok) { $h.Authorization = "Bearer $tok" }
    $a = @{ Method = $m; Uri = "$Base/$u"; Headers = $h; UseBasicParsing = $true }
    if ($null -ne $body) { $a.ContentType = "application/json; charset=utf-8"; $a.Body = [Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Depth 10)) }
    $r = Invoke-WebRequest @a
    $text = [Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray())
    if ($u -like "storefront*") { $script:ShopBodies.Add($text) }
    return @{ s = [int]$r.StatusCode; d = ($(if ($text) { $text | ConvertFrom-Json } else { $null })); t = $text }
  } catch {
    $resp = $_.Exception.Response
    $c = if ($resp) { (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } else { $_.Exception.Message }
    if ($u -like "storefront*") { $script:ShopBodies.Add($c) }
    return @{ s = [int]$resp.StatusCode; d = $c; t = $c }
  }
}
function Check($label, $actual, $expected) { "{0}  {1,-78} got {2}, want {3}" -f ($(if ("$actual" -eq "$expected") { "PASS" } else { "FAIL" })), $label, $actual, $expected }
function Login($email, $pw) { (Call POST "auth/login" @{ email = $email; password = $pw }).d.accessToken }
function Wh($code, $name, $city) { @{ code = $code; name = $name; type = "Owned"; address = @{ line1 = "Plot 9"; city = $city; state = "Maharashtra"; pincode = "411001" } } }
function Addr($state, $pin) { @{ line1 = "12 Site Road"; line2 = "Near Bridge"; city = "Pune"; state = $state; pincode = $pin } }

$sfx = Get-Random -Maximum 99999
$admin = Login "admin@platform.local" "Emulator-Admin@2026"
$today = (Get-Date).ToUniversalTime().AddHours(5.5).Date

"--- business settings (GST needs the registered state)"
Check "GSTIN of another state -> 400" (Call PUT "settings/business" @{ legalName = "Demo Build Supply Pvt Ltd"; gstin = "29AAPFU0939F1ZV"; address = (Addr "Maharashtra" "411001") } $admin).s 400
$bs = Call PUT "settings/business" @{ legalName = "Demo Build Supply Pvt Ltd"; gstin = "27AAPFU0939F1ZV"; address = (Addr "maharashtra" "411001") } $admin
Check "save business settings (state spelt officially)" "$($bs.s) $($bs.d.isComplete) $($bs.d.address.state)" "200 True Maharashtra"

"--- warehouses and delivery areas"
$w1 = (Call POST "warehouses" (Wh "SF-PUN-$sfx" "Pune Yard $sfx" "Pune") $admin).d
$w2 = (Call POST "warehouses" (Wh "SF-MUM-$sfx" "Mumbai Depot $sfx" "Mumbai") $admin).d
$pinA = "4110" + ("{0:D2}" -f ($sfx % 100)); $pinB = "4000" + ("{0:D2}" -f ($sfx % 100))
$add1 = Call POST "delivery-areas" @{ warehouseId = $w1.id; pincodes = @("$pinA, 411099"); leadTimeDays = 1 } $admin
Check "W1 serves two PIN codes (pasted list)" "$($add1.s) $($add1.d.added)" "200 2"
Check "W2 serves pinA in 3 days, pinB in 1" "$((Call POST 'delivery-areas' @{ warehouseId = $w2.id; pincodes = @($pinA); leadTimeDays = 3 } $admin).s) $((Call POST 'delivery-areas' @{ warehouseId = $w2.id; pincodes = @($pinB); leadTimeDays = 1 } $admin).s)" "200 200"
Check "adding the same PIN again updates, not duplicates" (Call POST "delivery-areas" @{ warehouseId = $w1.id; pincodes = @("411099"); leadTimeDays = 2 } $admin).d.updated 1
Check "bad PIN code -> 400" (Call POST "delivery-areas" @{ warehouseId = $w1.id; pincodes = @("12345"); leadTimeDays = 1 } $admin).s 400
Check "list W1 areas" (Call GET "delivery-areas?warehouseId=$($w1.id)" $null $admin).d.totalCount 2

"--- catalogue items used"
$cemProd = (Call GET "catalog/products?search=ultratech" $null $admin).d.items[0]
$cem = ((Call GET "catalog/products/$($cemProd.id)" $null $admin).d.skus | Where-Object { $_.code -eq "CEM-ULT-OPC53-50KG" })
$tmtProd = (Call GET "catalog/products?search=tiscon" $null $admin).d.items[0]
$tmt = ((Call GET "catalog/products/$($tmtProd.id)" $null $admin).d.skus | Where-Object { $_.code -eq "TMT-TISCON-12MM" })
Check "found cement and steel SKUs" "$($cem.code) $($tmt.code)" "CEM-ULT-OPC53-50KG TMT-TISCON-12MM"

"--- GST rates (dated, P8)"
Check "rate starting in the past -> 400" (Call POST "tax-rates" @{ hsnCode = "2523"; ratePercent = 28; cessPercent = 0; validFrom = (Get-Date).ToUniversalTime().AddDays(-2).ToString("o") } $admin).s 400
Check "bad HSN -> 400" (Call POST "tax-rates" @{ hsnCode = "252"; ratePercent = 28 } $admin).s 400
Check "set 2523 (cement) at 28%" (Call POST "tax-rates" @{ hsnCode = "2523"; ratePercent = 28; cessPercent = 0; remarks = "test" } $admin).s 201
Check "set 7214 (steel) at 18%" (Call POST "tax-rates" @{ hsnCode = "7214"; ratePercent = 18; cessPercent = 0 } $admin).s 201
$rates = (Call GET "tax-rates?pageSize=100" $null $admin).d.items
Check "2523 is current with products counted" "$(($rates | Where-Object hsnCode -eq '2523').isCurrent) $((($rates | Where-Object hsnCode -eq '2523').productCount) -gt 0)" "True True"
Check "missing-rate list excludes 2523" (@((Call GET "tax-rates/missing" $null $admin).d | Where-Object hsnCode -eq "2523").Count) 0

"--- price lists and prices"
$lists = (Call GET "price-lists?pageSize=100" $null $admin).d.items
$retail = $lists | Where-Object { $_.type -eq "Retail" } | Select-Object -First 1
Check "seeded retail list" $retail.code "RETAIL"
Check "a second active retail list -> 409" (Call POST "price-lists" @{ name = "Retail 2"; code = "R2-$sfx"; type = "Retail" } $admin).s 409
Check "retail list cannot be deactivated -> 422" (Call DELETE "price-lists/$($retail.id)" $null $admin).s 422
$tier = (Call POST "price-lists" @{ name = "Contractors $sfx"; code = "CONTR-$sfx"; type = "Tier" } $admin).d
Check "contract list without customer -> 400" (Call POST "price-lists" @{ name = "C"; code = "C-$sfx"; type = "Contract" } $admin).s 400
Check "first slab must start at 0 -> 400" (Call POST "prices" @{ priceListId = $retail.id; skuId = $cem.id; uom = "BAG"; slabs = @(@{ minQuantity = 10; unitPrice = 400 }) } $admin).s 400
Check "unit the SKU cannot use -> 400" (Call POST "prices" @{ priceListId = $retail.id; skuId = $cem.id; uom = "SQFT"; slabs = @(@{ minQuantity = 0; unitPrice = 400 }) } $admin).s 400
Check "retail cement 400/BAG, 380 from 100 bags" (Call POST "prices" @{ priceListId = $retail.id; skuId = $cem.id; uom = "BAG"; slabs = @(@{ minQuantity = 100; unitPrice = 380 }, @{ minQuantity = 0; unitPrice = 400 }) } $admin).s 201
Check "retail steel 65000/TONNE, 63000 from 5 t" (Call POST "prices" @{ priceListId = $retail.id; skuId = $tmt.id; uom = "tonne"; slabs = @(@{ minQuantity = 0; unitPrice = 65000 }, @{ minQuantity = 5; unitPrice = 63000 }) } $admin).s 201
Check "tier cement 360/BAG" (Call POST "prices" @{ priceListId = $tier.id; skuId = $cem.id; uom = "BAG"; slabs = @(@{ minQuantity = 0; unitPrice = 360 }) } $admin).s 201
$future = (Get-Date).ToUniversalTime().AddDays(3).ToString("o")
Check "future retail cement price 420 (upcoming)" (Call POST "prices" @{ priceListId = $retail.id; skuId = $cem.id; uom = "BAG"; slabs = @(@{ minQuantity = 0; unitPrice = 420 }); validFrom = $future } $admin).s 201
$grid = (Call GET "prices?priceListId=$($retail.id)&search=ultratech" $null $admin).d.items[0].skus | Where-Object { $_.skuId -eq $cem.id }
Check "grid: current 400, upcoming 420" "$($grid.current.slabs[0].unitPrice) $($grid.upcoming.slabs[0].unitPrice)" "400 420"
$hist = @((Call GET "prices/history?priceListId=$($retail.id)&skuId=$($cem.id)" $null $admin).d)
Check "history: newest first, exactly one current" "$($hist[0].slabs[0].unitPrice) $(@($hist | Where-Object isCurrent).Count)" "420 1"

"--- stock into the two warehouses"
function Receive($wid, $code, $qty, $uom) { (Call POST "stock/receipts" @{ warehouseId = $wid; supplierName = "Test Supplier"; lines = @(@{ skuCode = $code; quantity = $qty; uom = $uom }) } $admin).s }
Check "receive W1 50 bags, W2 100 bags, W1 2000 kg steel" "$(Receive $w1.id 'CEM-ULT-OPC53-50KG' 50 'BAG') $(Receive $w2.id 'CEM-ULT-OPC53-50KG' 100 'BAG') $(Receive $w1.id 'TMT-TISCON-12MM' 2000 'KG')" "201 201 201"

"--- storefront catalogue (anonymous)"
Check "categories" (Call GET "storefront/categories" $null $null).s 200
Check "brands" (Call GET "storefront/brands" $null $null).s 200
$card = (Call GET "storefront/products?search=ultratech&pincode=$pinA" $null $null).d.items[0]
Check "card: from 380/BAG, in stock" "$($card.fromPrice.unitPrice) $($card.fromPrice.uom) $($card.status)" "380 BAG InStock"
$p = (Call GET "storefront/products/$($cemProd.id)?pincode=$pinA" $null $null).d
$v = $p.variants | Where-Object { $_.skuId -eq $cem.id }
Check "variant: 100 BAG (best single warehouse, never 50+100), GST 28, quickest tomorrow" "$($v.availability.quantity) $($v.availability.uom) $($v.price.taxRatePercent) $($v.delivery.earliestOn)" "100 BAG 28 $($today.AddDays(1).ToString('yyyy-MM-dd'))"
Check "PIN nobody serves -> NotDeliverable" ((Call GET "storefront/products/$($cemProd.id)?pincode=799999" $null $null).d.variants | Where-Object { $_.skuId -eq $cem.id }).availability.status "NotDeliverable"
Check "pinB (W2 only) -> 100" ((Call GET "storefront/products/$($cemProd.id)?pincode=$pinB" $null $null).d.variants | Where-Object { $_.skuId -eq $cem.id }).availability.quantity 100
Check "no PIN -> CheckPincode, no quantity" "$(($p2 = (Call GET "storefront/products/$($cemProd.id)" $null $null).d.variants | Where-Object { $_.skuId -eq $cem.id }).availability.status) [$($p2.availability.quantity)]" "CheckPincode []"
Check "unknown product -> 404" (Call GET "storefront/products/$([guid]::NewGuid())" $null $null).s 404

"--- customer accounts, and the two kinds of token kept apart"
$reg = Call POST "storefront/auth/register" @{ name = "Asha Builder"; email = "asha$sfx@shop.local"; password = "Shop@12345"; phone = "+919800000001"; companyName = "Asha Constructions" } $null
Check "register customer A" $reg.s 200
$a = $reg.d.accessToken
Check "same email again -> 422, generic message" (Call POST "storefront/auth/register" @{ name = "X"; email = "asha$sfx@shop.local"; password = "Shop@12345" } $null).s 422
Check "customer login" (Call POST "storefront/auth/login" @{ email = "asha$sfx@shop.local"; password = "Shop@12345" } $null).s 200
Check "customer wrong password -> 401" (Call POST "storefront/auth/login" @{ email = "asha$sfx@shop.local"; password = "Wrong@12345" } $null).s 401
Check "customer cannot sign in to the admin API -> 401" (Call POST "auth/login" @{ email = "asha$sfx@shop.local"; password = "Shop@12345" } $null).s 401
Check "staff cannot sign in to the store -> 401" (Call POST "storefront/auth/login" @{ email = "admin@platform.local"; password = "Emulator-Admin@2026" } $null).s 401
Check "customer token on warehouses -> 403" (Call GET "warehouses" $null $a).s 403
Check "customer token on stock sku-options -> 403" (Call GET "stock/sku-options" $null $a).s 403
Check "customer token on orders (staff) -> 403" (Call GET "orders" $null $a).s 403
Check "customer token on auth/me -> 403" (Call GET "auth/me" $null $a).s 403
Check "staff token on cart -> 403" (Call GET "storefront/cart" $null $admin).s 403
Check "no token on cart -> 401" (Call GET "storefront/cart" $null $null).s 401
Check "profile" (Call GET "storefront/me" $null $a).d.companyName "Asha Constructions"

"--- cart: slabs, units, GST, availability"
$c = (Call POST "storefront/cart/lines" @{ skuId = $cem.id; quantity = 120; uom = "bag" } $a).d
$l = $c.lines[0]
Check "120 bags at the 100-bag slab: 380, taxable 45600" "$($l.unitPrice) $($l.taxableAmount)" "380 45600"
Check "intra-state GST 14% + 14%" "$($l.cgstAmount) $($l.sgstAmount) $($l.igstAmount) $($l.totalAmount)" "6384 6384 0 58368"
Check "no PIN yet -> cannot check out" "$($c.canCheckout) $(($c.problems -join '|') -match 'PIN')" "False True"
$c = (Call PUT "storefront/cart/pincode" @{ pincode = $pinA } $a).d
Check "with PIN: 120 bags would need two warehouses -> 'Only 100 BAG', no checkout (never split)" "$($c.canCheckout) $(($c.lines[0].problem) -match 'Only 100 BAG')" "False True"
$c = (Call PUT "storefront/cart/lines/$($cem.id)" @{ quantity = 100; uom = "BAG" } $a).d
Check "100 bags: one warehouse (3 days), one date" "$($c.canCheckout) $($c.delivery.earliestOn) $($c.delivery.latestOn)" "True $($today.AddDays(3).ToString('yyyy-MM-dd')) $($today.AddDays(3).ToString('yyyy-MM-dd'))"
$c = (Call POST "storefront/cart/lines" @{ skuId = $tmt.id; quantity = 1.5; uom = "TONNE" } $a).d
$s = $c.lines | Where-Object { $_.skuId -eq $tmt.id }
Check "steel 1.5 t: 97500 + 18% GST" "$($s.taxableAmount) $($s.cgstAmount) $($s.totalAmount)" "97500 8775 115050"
$c = (Call POST "storefront/cart/lines" @{ skuId = $tmt.id; quantity = 500; uom = "KG" } $a).d
$s = $c.lines | Where-Object { $_.skuId -eq $tmt.id }
Check "500 KG more merges into the TONNE line: 2 TONNE" "$($s.quantity) $($s.uom) $($s.taxableAmount)" "2 TONNE 130000"
Check "unit the SKU cannot use -> 400" (Call POST "storefront/cart/lines" @{ skuId = $cem.id; quantity = 1; uom = "SQFT" } $a).s 400
Check "100 bags + steel: each fits somewhere, but not together -> cart problem, no checkout" "$($c.canCheckout) $((($c.problems) -join '|') -match 'cannot all be delivered together')" "False True"
Check "that message names no warehouse (P1)" ((($c.problems) -join '|') -match 'SF-|Pune Yard|Mumbai Depot|warehouse') $false
$c = (Call PUT "storefront/cart/lines/$($cem.id)" @{ quantity = 200; uom = "BAG" } $a).d
$l = $c.lines | Where-Object { $_.skuId -eq $cem.id }
Check "200 bags > 100 in any one place: line problem, no checkout" "$($c.canCheckout) $($l.problem -match 'Only 100 BAG')" "False True"
$c = (Call PUT "storefront/cart/lines/$($cem.id)" @{ quantity = 50; uom = "BAG" } $a).d
Check "50 bags + steel: both in the Pune yard -> one warehouse, tomorrow" "$($c.canCheckout) $($c.delivery.earliestOn) $($c.delivery.latestOn)" "True $($today.AddDays(1).ToString('yyyy-MM-dd')) $($today.AddDays(1).ToString('yyyy-MM-dd'))"
Check "totals are the sum of lines (50 bags at 400 + GST, steel)" $c.totals.totalAmount (25600 + 153400)

"--- checkout: order placed, stock held, idempotent"
$key = [guid]::NewGuid()
$o = Call POST "storefront/checkout" @{ clientId = $key; address = (Addr "Maharashtra" $pinA); phone = "+919800000001" } $a
Check "checkout" "$($o.s) $($o.d.status) $($o.d.referenceNo -match '^ORD-\d{4}-\d{6}$')" "200 Placed True"
Check "order totals as shown in the cart" $o.d.totals.totalAmount (25600 + 153400)
Check "order has one delivery date" "$($o.d.delivery.earliestOn) $($o.d.delivery.latestOn)" "$($today.AddDays(1).ToString('yyyy-MM-dd')) $($today.AddDays(1).ToString('yyyy-MM-dd'))"
$again = Call POST "storefront/checkout" @{ clientId = $key; address = (Addr "Maharashtra" $pinA); phone = "+919800000001" } $a
Check "same checkout key -> same order, not a second one" "$($again.s) $($again.d.id -eq $o.d.id)" "200 True"
Check "cart is empty after checkout" @((Call GET "storefront/cart" $null $a).d.lines).Count 0
$b1 = (Call GET "stock/balances?warehouseId=$($w1.id)" $null $admin).d.items | Where-Object { $_.skuCode -eq "CEM-ULT-OPC53-50KG" }
$b2 = (Call GET "stock/balances?warehouseId=$($w2.id)" $null $admin).d.items | Where-Object { $_.skuCode -eq "CEM-ULT-OPC53-50KG" }
Check "held all in W1 (50 of 50); W2 untouched (0 of 100)" "$($b1.reserved) $($b1.available) $($b2.reserved) $($b2.available)" "50 0 0 100"
Check "storefront now offers 100 bags (W2 alone)" ((Call GET "storefront/products/$($cemProd.id)?pincode=$pinA" $null $null).d.variants | Where-Object { $_.skuId -eq $cem.id }).availability.quantity 100
Check "staff cannot adjust away held stock -> 422" (Call POST "stock/adjustments" @{ warehouseId = $w1.id; reason = "Damage"; remarks = "wet"; lines = @(@{ skuCode = "CEM-ULT-OPC53-50KG"; quantity = 5; uom = "BAG" }) } $admin).s 422
$so = (Call GET "orders/$($o.d.id)" $null $admin).d
Check "staff order view: both lines held in ONE warehouse" "$(@($so.holds).Count) $((@($so.holds.warehouseCode) | Sort-Object -Unique) -join ',')" "2 SF-PUN-$sfx"
Check "staff order list finds it by reference" (Call GET "orders?search=$($o.d.referenceNo)" $null $admin).d.totalCount 1

"--- another customer: inter-state (IGST), shortage message, 404 for others' orders"
$bt = (Call POST "storefront/auth/register" @{ name = "Bala Traders"; email = "bala$sfx@shop.local"; password = "Shop@12345" } $null).d.accessToken
Call PUT "storefront/cart/pincode" @{ pincode = $pinB } $bt | Out-Null
Call POST "storefront/cart/lines" @{ skuId = $cem.id; quantity = 120; uom = "BAG" } $bt | Out-Null
$fail = Call POST "storefront/checkout" @{ clientId = [guid]::NewGuid(); address = (Addr "Karnataka" $pinB); phone = "+919800000002" } $bt
Check "120 bags when 100 are free for pinB -> 422, says 'Only 100 BAG'" "$($fail.s) $($fail.t -match 'Only 100 BAG')" "422 True"
Call PUT "storefront/cart/lines/$($cem.id)" @{ quantity = 30; uom = "BAG" } $bt | Out-Null
$ob = Call POST "storefront/checkout" @{ clientId = [guid]::NewGuid(); address = (Addr "Karnataka" $pinB); phone = "+919800000002"; gstin = "29AAPFU0939F1ZV" } $bt
Check "Karnataka delivery: IGST 28% on 12000" "$($ob.s) $($ob.d.isInterState) $($ob.d.totals.igstAmount) $($ob.d.totals.cgstAmount) $($ob.d.totals.totalAmount)" "200 True 3360 0 15360"
Check "B cannot see A's order -> 404" (Call GET "storefront/orders/$($o.d.id)" $null $bt).s 404
Check "B cannot cancel A's order -> 404" (Call POST "storefront/orders/$($o.d.id)/cancel" @{ remarks = "x" } $bt).s 404
Check "B's list has only B's order" (Call GET "storefront/orders" $null $bt).d.totalCount 1

"--- concurrency: 5 customers race for 10 bags (3 each) on a PIN only W1 serves"
Check "receive 10 more bags into W1" (Receive $w1.id 'CEM-ULT-OPC53-50KG' 10 'BAG') 201
$racers = 1..5 | ForEach-Object {
  $t = (Call POST "storefront/auth/register" @{ name = "Racer $_"; email = "racer$_-$sfx@shop.local"; password = "Shop@12345" } $null).d.accessToken
  Call PUT "storefront/cart/pincode" @{ pincode = "411099" } $t | Out-Null
  Call POST "storefront/cart/lines" @{ skuId = $cem.id; quantity = 3; uom = "BAG" } $t | Out-Null
  $t
}
Add-Type -AssemblyName System.Net.Http
$client = New-Object System.Net.Http.HttpClient
$tasks = foreach ($t in $racers) {
  $req = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Post, "$Base/storefront/checkout")
  $req.Headers.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", $t)
  $body = @{ clientId = [guid]::NewGuid(); address = (Addr "Maharashtra" "411099"); phone = "+919800000009" } | ConvertTo-Json -Depth 5
  $req.Content = New-Object System.Net.Http.StringContent($body, [Text.Encoding]::UTF8, "application/json")
  $client.SendAsync($req)
}
[System.Threading.Tasks.Task]::WaitAll([System.Threading.Tasks.Task[]]$tasks)
$codes = $tasks | ForEach-Object { [int]$_.Result.StatusCode }
Check "exactly 3 succeed, the rest refused (422/409)" "$(@($codes | Where-Object { $_ -eq 200 }).Count) $(@($codes | Where-Object { $_ -eq 422 -or $_ -eq 409 }).Count)" "3 2"
$b1 = (Call GET "stock/balances?warehouseId=$($w1.id)" $null $admin).d.items | Where-Object { $_.skuCode -eq "CEM-ULT-OPC53-50KG" }
Check "W1 never over-promised: reserved 59 of 60" "$($b1.onHand) $($b1.reserved)" "60 59"

"--- cancel gives the stock back"
$cx = Call POST "storefront/orders/$($o.d.id)/cancel" @{ remarks = "Changed plan" } $a
Check "A cancels own order" "$($cx.s) $($cx.d.status) $($cx.d.canCancel)" "200 Cancelled False"
Check "cancel again -> 422" (Call POST "storefront/orders/$($o.d.id)/cancel" @{} $a).s 422
$b1 = (Call GET "stock/balances?warehouseId=$($w1.id)" $null $admin).d.items | Where-Object { $_.skuCode -eq "CEM-ULT-OPC53-50KG" }
$b2 = (Call GET "stock/balances?warehouseId=$($w2.id)" $null $admin).d.items | Where-Object { $_.skuCode -eq "CEM-ULT-OPC53-50KG" }
Check "released: W1 reserved 9, W2 reserved 30" "$($b1.reserved) $($b2.reserved)" "9 30"
Check "staff cancels B's order" (Call POST "orders/$($ob.d.id)/cancel" @{ remarks = "Out of delivery range" } $admin).d.status "Cancelled"
Check "customer sees staff's cancel" (Call GET "storefront/orders/$($ob.d.id)" $null $bt).d.status "Cancelled"

"--- tier and contract prices"
$custB = (Call GET "customers?search=bala$sfx" $null $admin).d.items[0]
Check "put B on the Contractors tier" (Call PUT "customers/$($custB.id)" @{ name = $custB.name; tierPriceListId = $tier.id; isActive = $true } $admin).d.tierPriceListName "Contractors $sfx"
Check "B now sees 360/BAG" ((Call GET "storefront/products/$($cemProd.id)" $null $bt).d.variants | Where-Object { $_.skuId -eq $cem.id }).price.slabs[0].unitPrice 360
$custA = (Call GET "customers?search=asha$sfx" $null $admin).d.items[0]
$contract = (Call POST "price-lists" @{ name = "Asha contract"; code = "ASHA-$sfx"; type = "Contract"; customerId = $custA.id } $admin).d
Call POST "prices" @{ priceListId = $contract.id; skuId = $cem.id; uom = "BAG"; slabs = @(@{ minQuantity = 0; unitPrice = 350 }) } $admin | Out-Null
Check "A (contract) sees 350" ((Call GET "storefront/products/$($cemProd.id)" $null $a).d.variants | Where-Object { $_.skuId -eq $cem.id }).price.slabs[0].unitPrice 350
Check "visitor still sees retail 400" ((Call GET "storefront/products/$($cemProd.id)" $null $null).d.variants | Where-Object { $_.skuId -eq $cem.id }).price.slabs[0].unitPrice 400
Check "tier list must be a Tier -> 400" (Call PUT "customers/$($custB.id)" @{ name = $custB.name; tierPriceListId = $retail.id; isActive = $true } $admin).s 400

"--- blocking a customer"
Check "staff blocks B" (Call PUT "customers/$($custB.id)" @{ name = $custB.name; isActive = $false } $admin).d.isActive False
Check "B's token stops working at once -> 401" (Call GET "storefront/me" $null $bt).s 401
Check "B cannot sign in -> 401" (Call POST "storefront/auth/login" @{ email = "bala$sfx@shop.local"; password = "Shop@12345" } $null).s 401

"--- ledger still agrees"
Check "ledger check: no mismatches" (Call GET "stock/reconcile" $null $admin).d.mismatches.Count 0

"--- opacity (P1): no storefront response names a warehouse"
$needles = @("SF-PUN-$sfx", "SF-MUM-$sfx", "Pune Yard $sfx", "Mumbai Depot $sfx", "$($w1.id)", "$($w2.id)", "warehouse", "Warehouse", '"lat"', '"lng"', "hold", "Hold", "supplier", "Supplier", "Test Supplier", "allocation")
$leaks = foreach ($n in $needles) { if (@($script:ShopBodies | Where-Object { $_ -like "*$n*" }).Count -gt 0) { $n } }
Check "scanned $($script:ShopBodies.Count) storefront responses; leaks found" (@($leaks) -join ",") ""
