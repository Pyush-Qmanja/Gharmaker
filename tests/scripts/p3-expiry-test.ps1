# Needs the API started with Storefront:HoldMinutes=1 and ExpirySweepSeconds=5 (restart-api.ps1 -HoldMinutes 1 -SweepSeconds 5),
# and the retail cement price and 2523 GST rate from p3-test.ps1.
param([string]$Base = "http://localhost:5310/api")
function Call($m, $u, $body, $tok) {
  try {
    $h = @{}; if ($tok) { $h.Authorization = "Bearer $tok" }
    $a = @{ Method = $m; Uri = "$Base/$u"; Headers = $h; UseBasicParsing = $true }
    if ($null -ne $body) { $a.ContentType = "application/json"; $a.Body = ($body | ConvertTo-Json -Depth 8) }
    $r = Invoke-WebRequest @a
    return @{ s = [int]$r.StatusCode; d = ($(if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null })) }
  } catch { $resp = $_.Exception.Response; return @{ s = [int]$resp.StatusCode; d = $null } }
}
function Check($label, $actual, $expected) { "{0}  {1,-70} got {2}, want {3}" -f ($(if ("$actual" -eq "$expected") { "PASS" } else { "FAIL" })), $label, $actual, $expected }
$sfx = Get-Random -Maximum 99999
$admin = (Call POST "auth/login" @{ email = "admin@platform.local"; password = "Emulator-Admin@2026" }).d.accessToken
$w = (Call POST "warehouses" @{ code = "EXP-$sfx"; name = "Expiry $sfx"; type = "Owned"; address = @{ line1 = "x"; city = "Pune"; state = "Maharashtra"; pincode = "411001" } } $admin).d
$pin = "4120" + ("{0:D2}" -f ($sfx % 100))
Call POST "delivery-areas" @{ warehouseId = $w.id; pincodes = @($pin); leadTimeDays = 2 } $admin | Out-Null
Call POST "stock/receipts" @{ warehouseId = $w.id; supplierName = "S"; lines = @(@{ skuCode = "CEM-ULT-OPC53-50KG"; quantity = 20; uom = "BAG" }) } $admin | Out-Null
$cemSku = ((Call GET "stock/sku-options" $null $admin).d | Where-Object { $_.code -eq "CEM-ULT-OPC53-50KG" }).skuId
$t = (Call POST "storefront/auth/register" @{ name = "Late Payer"; email = "late$sfx@shop.local"; password = "Shop@12345" } $null).d.accessToken
Call PUT "storefront/cart/pincode" @{ pincode = $pin } $t | Out-Null
Call POST "storefront/cart/lines" @{ skuId = $cemSku; quantity = 8; uom = "BAG" } $t | Out-Null
$o = (Call POST "storefront/checkout" @{ clientId = [guid]::NewGuid(); address = @{ line1 = "1 Road"; city = "Pune"; state = "Maharashtra"; pincode = $pin }; phone = "+919800000011" } $t).d
function Held() { ((Call GET "stock/balances?warehouseId=$($w.id)" $null $admin).d.items | Where-Object { $_.skuCode -eq "CEM-ULT-OPC53-50KG" }).reserved }
Check "order placed, 8 bags held" "$($o.status) $(Held)" "Placed 8"
$deadline = (Get-Date).AddSeconds(120)
do { Start-Sleep 5; $st = (Call GET "storefront/orders/$($o.id)" $null $t).d.status } while ($st -eq "Placed" -and (Get-Date) -lt $deadline)
Check "after the hold expires the order is Expired" $st "Expired"
Check "and the 8 bags are free again" (Held) 0
$so = (Call GET "orders/$($o.id)" $null $admin).d
Check "hold marked Expired" (@($so.holds | ForEach-Object status) -join ",") "Expired"
Check "customer can no longer cancel it -> 422" (Call POST "storefront/orders/$($o.id)/cancel" @{} $t).s 422
Check "ledger check: no mismatches" (Call GET "stock/reconcile" $null $admin).d.mismatches.Count 0
