# Registers a store customer on the test Web app, fills a cart and places an order, and saves the signed-in store
# pages as static snapshots under wwwroot/_preview (delete that folder before committing).
param([string]$Pin = "")
$Web = "https://localhost:7331"
$out = (Join-Path $PSScriptRoot "..\..\src\Platform.Web\wwwroot\_preview")
New-Item -ItemType Directory -Force $out | Out-Null
function Token($html) { if ($html -match 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"') { $Matches[1] } }
function Post($s, $path, $page, $fields) {
  $token = Token (Invoke-WebRequest "$Web$page" -WebSession $s -UseBasicParsing).Content
  $body = ($fields + @(,@("__RequestVerificationToken", $token)) | ForEach-Object { [uri]::EscapeDataString($_[0]) + "=" + [uri]::EscapeDataString([string]$_[1]) }) -join "&"
  Invoke-WebRequest "$Web$path" -Method Post -Body $body -ContentType "application/x-www-form-urlencoded" -WebSession $s -UseBasicParsing -MaximumRedirection 5
}
function Save($s, $path, $name) {
  $r = Invoke-WebRequest "$Web$path" -WebSession $s -UseBasicParsing
  [IO.File]::WriteAllText("$out\$name.html", [Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray()), [Text.Encoding]::UTF8)
  "{0,-45} -> _preview/{1}.html" -f $path, $name
}
$s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$sfx = Get-Random -Maximum 99999
Post $s "/shop/home/pincode" "/shop" @(@("pincode", $Pin), @("returnUrl", "/shop")) | Out-Null
Post $s "/shop/account/register" "/shop/account/register" @(@("Name", "Meera Contractor"), @("Email", "meera$sfx@shop.local"), @("Password", "Shop@12345"), @("Phone", "+919822223333"), @("CompanyName", "Meera Builders")) | Out-Null
$prod = (Invoke-RestMethod "http://localhost:5310/api/storefront/products?search=ultratech").items[0].id
$tmt = (Invoke-RestMethod "http://localhost:5310/api/storefront/products?search=tiscon").items[0].id
$cem = (Invoke-RestMethod "http://localhost:5310/api/storefront/products/$prod").variants[0].skuId
$steel = ((Invoke-RestMethod "http://localhost:5310/api/storefront/products/$tmt").variants | Where-Object { $_.code -eq "TMT-TISCON-12MM" }).skuId
Post $s "/shop/cart/add" "/shop/products/details/$prod" @(@("SkuId", $cem), @("Quantity", "25"), @("Uom", "BAG")) | Out-Null
Post $s "/shop/cart/add" "/shop/products/details/$tmt" @(@("SkuId", $steel), @("Quantity", "0.5"), @("Uom", "TONNE")) | Out-Null
Save $s "/shop/cart" "shop-cart"
Save $s "/shop/checkout" "shop-checkout"
$co = (Invoke-WebRequest "$Web/shop/checkout" -WebSession $s -UseBasicParsing).Content
$key = [regex]::Match($co, 'name="Form.ClientId" value="([^"]+)"').Groups[1].Value
$placed = Post $s "/shop/checkout" "/shop/checkout" @(@("Form.ClientId", $key), @("Form.Address.Line1", "Survey 12, Hinjewadi Phase 3"), @("Form.Address.City", "Pune"), @("Form.Address.State", "Maharashtra"), @("Form.Address.Pincode", $Pin), @("Form.Phone", "+919822223333"), @("Form.SaveAddress", "true"))
[IO.File]::WriteAllText("$out\shop-order.html", [Text.Encoding]::UTF8.GetString($placed.RawContentStream.ToArray()), [Text.Encoding]::UTF8)
"order page -> _preview/shop-order.html ($($placed.BaseResponse.ResponseUri.AbsolutePath))"
Save $s "/shop/orders" "shop-orders"
Save $s "/shop/account/profile" "shop-profile"
