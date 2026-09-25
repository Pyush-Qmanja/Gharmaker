param([string]$Base = "http://localhost:5310/api", [string]$Web = "https://localhost:7331", [switch]$Expiry)
# Phase 4 step 1: confirming orders. TEST environment only. Needs the retail cement price and 2523 GST rate from p3-test.ps1.
# -Expiry: the API must run with Storefront:HoldMinutes=1 and ExpirySweepSeconds=5 (restart-api.ps1 -HoldMinutes 1 -SweepSeconds 5).
$ErrorActionPreference = "Continue"
function Call($m, $u, $body, $tok) {
  try {
    $h = @{}; if ($tok) { $h.Authorization = "Bearer $tok" }
    $a = @{ Method = $m; Uri = "$Base/$u"; Headers = $h; UseBasicParsing = $true }
    if ($null -ne $body) { $a.ContentType = "application/json"; $a.Body = ($body | ConvertTo-Json -Depth 6) }
    $r = Invoke-WebRequest @a
    return @{ s = [int]$r.StatusCode; d = ($(if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null })); t = $r.Content }
  } catch {
    $code = $_.Exception.Response.StatusCode.value__
    $c = if ($_.Exception.Response) { (New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd() } else { $_.Exception.Message }
    return @{ s = $code; d = $null; t = $c }
  }
}
function Check($label, $ok, $detail = "") { "{0}  {1}{2}" -f $(if ($ok) { "PASS" } else { "FAIL" }), $label, $(if (-not $ok -and $detail) { "   [$detail]" } else { "" }) }
function Login($email, $pw) { (Call POST "auth/login" @{ email = $email; password = $pw }).d.accessToken }
function Addr($pin) { @{ line1 = "12 Site Road"; city = "Pune"; state = "Maharashtra"; pincode = $pin } }
$sfx = Get-Random -Maximum 99999
$admin = Login "admin@platform.local" "Emulator-Admin@2026"

"--- setup: one warehouse serving a PIN, 100 bags"
$wh = (Call POST "warehouses" @{ code = "P4-$sfx"; name = "Confirm Yard $sfx"; type = "Owned"; address = @{ line1 = "x"; city = "Pune"; state = "Maharashtra"; pincode = "411001" } } $admin).d
$pin = "4121" + ("{0:D2}" -f ($sfx % 100))
Call POST "delivery-areas" @{ warehouseId = $wh.id; pincodes = @($pin); leadTimeDays = 1 } $admin | Out-Null
Call POST "stock/receipts" @{ warehouseId = $wh.id; supplierName = "P4 Supplier"; lines = @(@{ skuCode = "CEM-ULT-OPC53-50KG"; quantity = 100; uom = "BAG" }) } $admin | Out-Null
$cemProd = (Call GET "storefront/products?search=ultratech" $null $null).d.items[0]
$cem = ((Call GET "catalog/products/$($cemProd.id)" $null $admin).d.skus | Where-Object { $_.code -eq "CEM-ULT-OPC53-50KG" })
function Balance { (Call GET "stock/balances?warehouseId=$($wh.id)" $null $admin).d.items | Where-Object { $_.skuCode -eq "CEM-ULT-OPC53-50KG" } }
function PlaceOrder($name, $qty) {
  $t = (Call POST "storefront/auth/register" @{ name = $name; email = "$($name.ToLower())$sfx@shop.local"; password = "Shop@12345" } $null).d.accessToken
  Call PUT "storefront/cart/pincode" @{ pincode = $pin } $t | Out-Null
  Call POST "storefront/cart/lines" @{ skuId = $cem.id; quantity = $qty; uom = "BAG" } $t | Out-Null
  $o = (Call POST "storefront/checkout" @{ clientId = [guid]::NewGuid(); address = (Addr $pin); phone = "+919800000011" } $t).d
  @{ t = $t; o = $o }
}
Check "setup ready" ($wh.id -and $cem.id)

if (-not $Expiry) {
  "--- confirm"
  $a = PlaceOrder "Chitra" 20
  Check "order placed, awaiting confirmation" ($a.o.status -eq "Placed")
  $viewer = Call POST "roles" @{ name = "Order viewer $sfx"; capabilities = @("orders.view") } $admin
  Call POST "users" @{ name = "Viewer"; email = "ov$sfx@platform.local"; password = "Test@12345"; roleIds = @($viewer.d.id); scopes = @(@{ scopeType = "Global" }) } $admin | Out-Null
  $tv = Login "ov$sfx@platform.local" "Test@12345"
  Check "orders VIEW only cannot confirm -> 403" ((Call POST "orders/$($a.o.id)/confirm" @{} $tv).s -eq 403)
  Check "customer token cannot use the staff confirm -> 403" ((Call POST "orders/$($a.o.id)/confirm" @{} $a.t).s -eq 403)
  $c = Call POST "orders/$($a.o.id)/confirm" @{} $admin
  Check "staff confirms -> Confirmed, with time" ($c.s -eq 200 -and $c.d.status -eq "Confirmed" -and $c.d.confirmedAt) $c.t
  Check "holds now 'Confirmed' (held for dispatch)" (@($c.d.holds | Where-Object { $_.status -eq "Confirmed" }).Count -eq 1)
  $b = Balance
  Check "stock stays reserved: 20 of 100" ("$($b.reserved) $($b.available)" -eq "20 80") "$($b.reserved) $($b.available)"
  $r = Call POST "orders/$($a.o.id)/confirm" @{} $admin
  Check "confirm twice -> 422" ($r.s -eq 422 -and $r.t -match "not waiting for confirmation") $r.t
  $co = (Call GET "storefront/orders/$($a.o.id)" $null $a.t).d
  Check "customer sees Confirmed, cannot cancel online" ($co.status -eq "Confirmed" -and -not $co.canCancel)
  $r = Call POST "storefront/orders/$($a.o.id)/cancel" @{ remarks = "x" } $a.t
  Check "customer cancel of a confirmed order -> 422 'please call us'" ($r.s -eq 422 -and $r.t -match "call us") $r.t
  Check "staff list: Confirmed tab finds it" ((Call GET "orders?status=Confirmed&search=$($a.o.referenceNo)" $null $admin).d.totalCount -eq 1)
  $x = Call POST "orders/$($a.o.id)/cancel" @{ remarks = "Customer called to cancel" } $admin
  Check "staff can still cancel a confirmed order" ($x.s -eq 200 -and $x.d.status -eq "Cancelled") $x.t
  $b = Balance
  Check "cancel frees the confirmed hold: 0 reserved" ("$($b.reserved) $($b.available)" -eq "0 100") "$($b.reserved) $($b.available)"
  Check "confirm a cancelled order -> 422" ((Call POST "orders/$($a.o.id)/confirm" @{} $admin).s -eq 422)
  Check "unknown order -> 404" ((Call POST "orders/$([guid]::NewGuid())/confirm" @{} $admin).s -eq 404)

  "--- web: staff confirms from the order page"
  $d = PlaceOrder "Devika" 5
  $s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
  $lp = Invoke-WebRequest "$Web/Account/Login" -WebSession $s -UseBasicParsing
  $tok = [regex]::Match($lp.Content, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
  Invoke-WebRequest "$Web/Account/Login" -Method Post -WebSession $s -UseBasicParsing -Body @{ Email = "admin@platform.local"; Password = "Emulator-Admin@2026"; __RequestVerificationToken = $tok } | Out-Null
  $page = Invoke-WebRequest "$Web/Orders/Details/$($d.o.id)" -WebSession $s -UseBasicParsing
  Check "order page offers 'Confirm this order'" ($page.Content -match "Confirm this order")
  $tok = [regex]::Match($page.Content, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
  $after = Invoke-WebRequest "$Web/Orders/Confirm/$($d.o.id)" -Method Post -WebSession $s -UseBasicParsing -Body @{ __RequestVerificationToken = $tok }
  Check "after confirming: 'Confirmed' badge, success message, no confirm card" ($after.Content -match 'status-badge--new">Confirmed' -and $after.Content -match "stays held until dispatch" -and $after.Content -notmatch "Confirm this order")
  $sp = New-Object Microsoft.PowerShell.Commands.WebRequestSession
  $lp = Invoke-WebRequest "$Web/shop/Account/Login" -WebSession $sp -UseBasicParsing
  $tok = [regex]::Match($lp.Content, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
  Invoke-WebRequest "$Web/shop/Account/Login" -Method Post -WebSession $sp -UseBasicParsing -Body @{ Email = "devika$sfx@shop.local"; Password = "Shop@12345"; __RequestVerificationToken = $tok } | Out-Null
  $mine = Invoke-WebRequest "$Web/shop/Orders/Details/$($d.o.id)" -WebSession $sp -UseBasicParsing
  Check "store order page: Confirmed, cash on delivery note, no cancel button" ($mine.Content -match ">Confirmed<" -and $mine.Content -match "pay in cash when it is delivered" -and $mine.Content -notmatch "Changed your mind")
  Call POST "orders/$($d.o.id)/cancel" @{ remarks = "test clean-up" } $admin | Out-Null
} else {
  "--- a confirmed order never expires; a placed one does"
  $p = PlaceOrder "Esha" 10
  $q = PlaceOrder "Farid" 15
  Check "confirm one of two" ((Call POST "orders/$($q.o.id)/confirm" @{} $admin).d.status -eq "Confirmed")
  Start-Sleep -Seconds 75
  $pe = (Call GET "orders/$($p.o.id)" $null $admin).d; $qe = (Call GET "orders/$($q.o.id)" $null $admin).d
  Check "unconfirmed order expired" ($pe.status -eq "Expired") $pe.status
  Check "confirmed order still Confirmed after its hold time" ($qe.status -eq "Confirmed") $qe.status
  $b = Balance
  Check "only the confirmed 15 bags stay reserved" ("$($b.reserved)" -eq "15") "$($b.reserved)"
  $late = PlaceOrder "Gita" 5
  Start-Sleep -Seconds 62
  $r = Call POST "orders/$($late.o.id)/confirm" @{} $admin
  Check "confirming after the hold ran out -> 422 (or already expired)" ($r.s -eq 422 -and $r.t -match "run out|expired") $r.t
  Call POST "orders/$($q.o.id)/cancel" @{} $admin | Out-Null
}
