$Web = "https://localhost:7331"
$Api = "http://localhost:5310/api"
$script:ShopPages = New-Object System.Collections.Generic.List[string]
function Token($html) { if ($html -match 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"') { $Matches[1] } }
function Check($label, $ok) { "{0}  {1}" -f ($(if ($ok) { "PASS" } else { "FAIL" })), $label }
function Keep($path, $content) { if ($path -like "/shop*") { $script:ShopPages.Add($content) } }
function Get-Page($session, $path) {
  try { $r = Invoke-WebRequest "$Web$path" -WebSession $session -UseBasicParsing -MaximumRedirection 5; $c = [Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray()); Keep $path $c; return @{ s = [int]$r.StatusCode; c = $c; u = $r.BaseResponse.ResponseUri.AbsolutePath + $r.BaseResponse.ResponseUri.Query } }
  catch { $resp = $_.Exception.Response; $c = if ($resp) { (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } else { "" }; return @{ s = [int]$resp.StatusCode; c = $c; u = "" } }
}
function Post-Form($session, $path, $pagePath, $fields) {
  $token = Token (Get-Page $session $pagePath).c
  $body = ($fields + @(,@("__RequestVerificationToken", $token)) | ForEach-Object { [uri]::EscapeDataString($_[0]) + "=" + [uri]::EscapeDataString([string]$_[1]) }) -join "&"
  try { $r = Invoke-WebRequest "$Web$path" -Method Post -Body $body -ContentType "application/x-www-form-urlencoded" -WebSession $session -UseBasicParsing -MaximumRedirection 5
        $c = [Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray()); Keep $path $c
        return @{ s = [int]$r.StatusCode; c = $c; u = $r.BaseResponse.ResponseUri.AbsolutePath + $r.BaseResponse.ResponseUri.Query } }
  catch { $resp = $_.Exception.Response; $c = if ($resp) { (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } else { "" }; return @{ s = [int]$resp.StatusCode; c = $c; u = "" } }
}
function ApiCall($m, $u, $body, $tok) {
  $a = @{ Method = $m; Uri = "$Api/$u"; Headers = @{ Authorization = "Bearer $tok" }; ContentType = "application/json" }
  if ($null -ne $body) { $a.Body = ($body | ConvertTo-Json -Depth 8) }
  Invoke-RestMethod @a
}
function Decode($html) { [System.Net.WebUtility]::HtmlDecode($html) }

"--- setup through the API: a warehouse serving a PIN code, stock, price and GST"
$sfx = Get-Random -Maximum 99999
$admin = (Invoke-RestMethod "$Api/auth/login" -Method Post -ContentType "application/json" -Body '{"email":"admin@platform.local","password":"Emulator-Admin@2026"}').accessToken
ApiCall PUT "settings/business" @{ legalName = "Demo Build Supply Pvt Ltd"; gstin = "27AAPFU0939F1ZV"; address = @{ line1 = "1 Main Road"; city = "Pune"; state = "Maharashtra"; pincode = "411001" } } $admin | Out-Null
$wh = ApiCall POST "warehouses" @{ code = "UIS-$sfx"; name = "Hidden Yard $sfx"; type = "Owned"; address = @{ line1 = "x"; city = "Pune"; state = "Maharashtra"; pincode = "411001" } } $admin
$pin = "4130" + ("{0:D2}" -f ($sfx % 100))
ApiCall POST "delivery-areas" @{ warehouseId = $wh.id; pincodes = @($pin); leadTimeDays = 2 } $admin | Out-Null
ApiCall POST "stock/receipts" @{ warehouseId = $wh.id; supplierName = "UI Supplier $sfx"; lines = @(@{ skuCode = "CEM-ULT-OPC53-50KG"; quantity = 40; uom = "BAG" }) } $admin | Out-Null
$retail = (ApiCall GET "price-lists?pageSize=100" $null $admin).items | Where-Object { $_.type -eq "Retail" } | Select-Object -First 1
$cemProd = (Invoke-RestMethod "$Api/storefront/products?search=ultratech").items[0]
$cemSku = ((ApiCall GET "catalog/products/$($cemProd.id)" $null $admin).skus | Where-Object { $_.code -eq "CEM-ULT-OPC53-50KG" }).id
Check "setup: warehouse, PIN $pin, 40 bags" ($wh.id -and $cemSku)

"--- browsing as a visitor"
$v = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$home1 = Get-Page $v "/shop"
Check "store home opens for visitors" ($home1.s -eq 200 -and $home1.c -match 'Building materials, delivered to your site')
$pinPost = Post-Form $v "/shop/home/pincode" "/shop" @(@("pincode", $pin), @("returnUrl", "/shop/products/details/$($cemProd.id)"))
$pd = $pinPost.c
Check "PIN code remembered: product shows in stock with delivery date" ($pd -match 'Delivering to ' + $pin -and $pd -match 'stock-badge--in' -and $pd -match 'Delivery by')
Check "price, bulk rate and GST shown" ((Decode $pd) -match '₹400\.00' -and (Decode $pd) -match '₹380\.00' -and $pd -match 'excl\. 28% GST')
Check "listing shows the product in stock" ((Get-Page $v "/shop/products?search=ultratech").c -match 'stock-badge--in')
Check "staff pages refuse the visitor (sent to staff sign-in)" ((Get-Page $v "/Orders").u -like "/Account/Login*")

"--- add to cart as a visitor, then create an account: the item is added"
$add = Post-Form $v "/shop/cart/add" "/shop/products/details/$($cemProd.id)" @(@("SkuId", $cemSku), @("Quantity", "12"), @("Uom", "BAG"), @("returnUrl", "/shop/products/details/$($cemProd.id)"))
Check "visitor asked to sign in" ($add.u -like "/shop/account/login*")
$email = "uishop$sfx@shop.local"
$reg = Post-Form $v "/shop/account/register" "/shop/account/register" @(@("Name", "Ravi Site Engineer"), @("Email", $email), @("Password", "Shop@12345"), @("Phone", "+919811112222"), @("CompanyName", "Ravi Infra"))
Check "account created, lands on the cart with the item" ($reg.u -eq "/shop/cart" -and $reg.c -match 'CEM-ULT-OPC53-50KG')
$cart = Decode $reg.c
Check "cart: 12 bags at ₹400, CGST+SGST, total ₹6,144.00" ($cart -match '₹4,800\.00' -and $cart -match 'CGST' -and $cart -match '₹6,144\.00')
Check "cart can go to checkout, delivery date shown" ($reg.c -match 'href="/shop/checkout"' -and $reg.c -match 'Delivery by')
Check "header shows the customer and cart count" ($reg.c -match 'Ravi Site Engineer' -and $reg.c -match 'shop-cart__count">1<')

"--- change quantity, then checkout"
$upd = Post-Form $v "/shop/cart/update?skuId=$cemSku" "/shop/cart" @(@("Quantity", "100"), @("Uom", "BAG"))
Check "100 bags reaches the bulk rate (₹38,000.00 before GST)" ((Decode $upd.c) -match '₹38,000\.00')
$upd = Post-Form $v "/shop/cart/update?skuId=$cemSku" "/shop/cart" @(@("Quantity", "60"), @("Uom", "BAG"))
Check "60 bags > 40 available: explained, checkout disabled" ((Decode $upd.c) -match 'Only 40 BAG can be delivered' -and $upd.c -notmatch 'href="/shop/checkout"')
$upd = Post-Form $v "/shop/cart/update?skuId=$cemSku" "/shop/cart" @(@("Quantity", "10"), @("Uom", "BAG"))
$co = Get-Page $v "/shop/checkout"
Check "checkout page with a one-time key" ($co.s -eq 200 -and $co.c -match 'name="Form.ClientId"')
$clientId = [regex]::Match($co.c, 'name="Form.ClientId" value="([^"]+)"').Groups[1].Value
$bad = Post-Form $v "/shop/checkout" "/shop/checkout" @(@("Form.ClientId", $clientId), @("Form.Phone__dial", "+91"), @("Form.Address.Line1", ""), @("Form.Address.City", "Pune"), @("Form.Address.State", "Maharashtra"), @("Form.Address.Pincode", $pin), @("Form.Phone", "12345"))
Check "missing address line and bad phone are shown on the form" ($bad.c -match 'field-validation-error|form-field__error">[^<]+' -and $bad.c -match '10-digit Indian mobile')
$fields = @(@("Form.ClientId", $clientId), @("Form.Address.Line1", "Plot 7, Site Road"), @("Form.Address.Line2", ""), @("Form.Address.City", "Pune"), @("Form.Address.State", "Maharashtra"), @("Form.Address.Pincode", $pin), @("Form.Phone", "+919811112222"), @("Form.Gstin", ""), @("Form.SaveAddress", "true"))
$placed = Post-Form $v "/shop/checkout" "/shop/checkout" $fields
Check "order placed: thank-you page" ($placed.u -like "/shop/orders/details/*" -and $placed.c -match 'your order is placed')
$ref = [regex]::Match($placed.c, 'ORD-\d{4}-\d{6}').Value
Check "order number shown ($ref)" ($ref -ne "")
$again = Post-Form $v "/shop/checkout" "/shop/cart" $fields
Check "posting the same checkout again shows the same order" ($again.c -match [regex]::Escape($ref))
Check "my orders lists it" ((Get-Page $v "/shop/orders").c -match [regex]::Escape($ref))
Check "cart is empty after the order" ((Get-Page $v "/shop/cart").c -match 'Your cart is empty')
Check "customer cannot open staff orders (sent to staff sign-in)" ((Get-Page $v "/Orders").u -like "/Account/Login*")

"--- staff see the order, the held stock and the customer"
$staff = New-Object Microsoft.PowerShell.Commands.WebRequestSession
Post-Form $staff "/Account/Login" "/Account/Login" @(@("Email", "admin@platform.local"), @("Password", "Emulator-Admin@2026")) | Out-Null
$orders = Get-Page $staff "/Orders?search=$ref"
Check "staff orders list finds it" ($orders.c -match [regex]::Escape($ref))
$orderId = [regex]::Match($placed.u, '(?i)/shop/orders/details/([0-9a-f-]+)').Groups[1].Value
$sod = Get-Page $staff "/Orders/Details/$orderId"
Check "staff order page shows the warehouse holding it" ($sod.c -match 'Stock held' -and $sod.c -match "UIS-$sfx")
Check "staff see the customer" ((Get-Page $staff "/Customers?search=uishop$sfx").c -match 'Ravi Site Engineer')
Check "staff session is not a store customer (cart asks to sign in)" ((Get-Page $staff "/shop/cart").u -like "/shop/account/login*")

"--- the customer cancels"
$cx = Post-Form $v "/shop/orders/cancel/$orderId" "/shop/orders/details/$orderId" @(@("Remarks", "Ordered by mistake"))
Check "cancelled, and shown as such" ($cx.c -match 'status-badge--error">Cancelled')

"--- staff pricing, GST, delivery areas and business settings screens"
$pl = Get-Page $staff "/PriceLists"
Check "price lists page" ($pl.s -eq 200 -and $pl.c -match 'RETAIL')
$grid = Get-Page $staff "/PriceLists/Prices/$($retail.id)?search=ultratech"
Check "price grid shows the current price" ((Decode $grid.c) -match '₹400\.00' -and $grid.c -match 'Change')
$sp = Get-Page $staff "/PriceLists/SetPrice/$($retail.id)?productId=$($cemProd.id)&skuId=$cemSku"
Check "set-price form with history" ($sp.s -eq 200 -and $sp.c -match 'Price history' -and $sp.c -match 'name="Slabs\[0\].UnitPrice"')
$badPrice = Post-Form $staff "/PriceLists/SetPrice/$($retail.id)?productId=$($cemProd.id)&skuId=$cemSku" "/PriceLists/SetPrice/$($retail.id)?productId=$($cemProd.id)&skuId=$cemSku" @(@("Uom", "BAG"), @("Slabs[0].MinQuantity", "0"), @("Slabs[0].UnitPrice", ""))
Check "a price is required" ($badPrice.c -match 'form-field__error">[^<]+')
$future = (Get-Date).AddDays(10).ToString("yyyy-MM-ddT10:00")
$okPrice = Post-Form $staff "/PriceLists/SetPrice/$($retail.id)?productId=$($cemProd.id)&skuId=$cemSku" "/PriceLists/SetPrice/$($retail.id)?productId=$($cemProd.id)&skuId=$cemSku" @(@("Uom", "BAG"), @("Slabs[0].MinQuantity", "0"), @("Slabs[0].UnitPrice", "415"), @("Slabs[1].MinQuantity", "200"), @("Slabs[1].UnitPrice", "395"), @("ValidFromIst", $future), @("Remarks", "UI test"))
Check "future price saved and shown as the next change" ($okPrice.u -like "/PriceLists/Prices/*" -and $okPrice.c -match 'Price saved\. It applies from')
Check "GST rates page" ((Get-Page $staff "/TaxRates").c -match '2523')
$tax = Post-Form $staff "/TaxRates/Create" "/TaxRates/Create" @(@("HsnCode", "68109990"), @("RatePercent", "18"), @("CessPercent", "0"), @("ValidFromIst", ""), @("Remarks", "UI test"))
Check "add a GST rate" ($tax.u -eq "/TaxRates" -and $tax.c -match '68109990')
$da = Post-Form $staff "/DeliveryAreas/Add" "/DeliveryAreas/Add" @(@("WarehouseId", $wh.id), @("Pincodes", "413101, 413102`n413103"), @("LeadTimeDays", "3"))
Check "add three PIN codes at once" ($da.c -match '3 PIN codes added')
$daBad = Post-Form $staff "/DeliveryAreas/Add" "/DeliveryAreas/Add" @(@("WarehouseId", $wh.id), @("Pincodes", "41310"), @("LeadTimeDays", "3"))
Check "a bad PIN code is explained" ($daBad.c -match 'six-digit PIN code')
Check "business settings say the store can take orders" ((Get-Page $staff "/BusinessSettings").c -match 'The store can take orders')
Check "dashboard shows the new modules" ((Get-Page $staff "/").c -match 'Price lists' -and (Get-Page $staff "/").c -match 'Delivery areas')

"--- opacity (P1): no store page names a warehouse"
$needles = @("UIS-$sfx", "Hidden Yard $sfx", "$($wh.id)", "UI Supplier $sfx", "Stock held", "warehouse code")
$leaks = foreach ($n in $needles) { if (@($script:ShopPages | Where-Object { $_ -like "*$n*" }).Count -gt 0) { $n } }
Check "scanned $($script:ShopPages.Count) store pages; no warehouse detail ($(@($leaks) -join ','))" (@($leaks).Count -eq 0)
