# Needs the test API started with production limits: restart-api.ps1 -StrictLimits (Web on :7331 stays as is).
$Api = "http://localhost:5310/api"
$Web = "https://localhost:7331"
function Check($label, $ok) { "{0}  {1}" -f ($(if ($ok) { "PASS" } else { "FAIL" })), $label }
function Send($method, $url, $body, $headers) {
  try {
    $a = @{ Method = $method; Uri = $url; UseBasicParsing = $true; Headers = ($(if ($headers) { $headers } else { @{} })) }
    if ($null -ne $body) { $a.ContentType = "application/json"; $a.Body = ($body | ConvertTo-Json -Depth 5) }
    $r = Invoke-WebRequest @a
    return @{ s = [int]$r.StatusCode; h = $r.Headers; c = $r.Content }
  } catch {
    $resp = $_.Exception.Response
    $c = if ($resp) { (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } else { "" }
    return @{ s = [int]$resp.StatusCode; h = $resp.Headers; c = $c }
  }
}
$sfx = Get-Random -Maximum 99999
$ipA = "203.0.113.$($sfx % 200 + 10)"
$ipB = "198.51.100.$($sfx % 200 + 10)"

"--- security headers"
$apiResp = Send GET "$Api/storefront/brands" $null @{}
Check "API: nosniff, no framing, locked-down content policy" ($apiResp.h["X-Content-Type-Options"] -eq "nosniff" -and $apiResp.h["X-Frame-Options"] -eq "DENY" -and $apiResp.h["Content-Security-Policy"] -match "default-src 'none'")
Check "API: responses not cached, no Server banner" ($apiResp.h["Cache-Control"] -match "no-store" -and -not $apiResp.h["Server"])
$shop = Invoke-WebRequest "$Web/shop" -UseBasicParsing
Check "Web: content policy allows only this site" ($shop.Headers["Content-Security-Policy"] -match "default-src 'self'" -and $shop.Headers["Content-Security-Policy"] -match "frame-ancestors 'none'")
Check "Web: nosniff, DENY framing, referrer and permissions policies" ($shop.Headers["X-Content-Type-Options"] -eq "nosniff" -and $shop.Headers["X-Frame-Options"] -eq "DENY" -and $shop.Headers["Referrer-Policy"] -and $shop.Headers["Permissions-Policy"])
Check "Web: no Server banner" (-not $shop.Headers["Server"])
$admin = Invoke-WebRequest "$Web/Account/Login" -UseBasicParsing
Check "Web: admin pages get the same headers" ($admin.Headers["Content-Security-Policy"] -match "default-src 'self'")

"--- sign-in: 10 attempts a minute per visitor"
$codes = 1..11 | ForEach-Object { (Send POST "$Api/auth/login" @{ email = "nobody$sfx@platform.local"; password = "Wrong@12345" } @{ "X-Forwarded-For" = $ipA }).s }
Check "first 10 wrong passwords -> 401, the 11th -> 429 ($($codes -join ','))" (@($codes[0..9] | Where-Object { $_ -eq 401 }).Count -eq 10 -and $codes[10] -eq 429)
$limited = Send POST "$Api/auth/login" @{ email = "admin@platform.local"; password = "Emulator-Admin@2026" } @{ "X-Forwarded-For" = $ipA }
Check "even the right password waits once limited, with Retry-After and a clear message" ($limited.s -eq 429 -and [int]$limited.h["Retry-After"] -gt 0 -and $limited.c -match "Too many requests")
Check "the store sign-in shares the limit for that visitor" ((Send POST "$Api/storefront/auth/login" @{ email = "x@y.z"; password = "Wrong@12345" } @{ "X-Forwarded-For" = $ipA }).s -eq 429)
Check "another visitor is not affected" ((Send POST "$Api/auth/login" @{ email = "admin@platform.local"; password = "Emulator-Admin@2026" } @{ "X-Forwarded-For" = $ipB }).s -eq 200)

"--- registration never says which emails exist"
$first = Send POST "$Api/storefront/auth/register" @{ name = "Sec Test"; email = "sec$sfx@shop.local"; password = "Shop@12345" } @{ "X-Forwarded-For" = $ipB }
Check "a new account is created" ($first.s -eq 200)
$dupCustomer = Send POST "$Api/storefront/auth/register" @{ name = "Sec Test"; email = "sec$sfx@shop.local"; password = "Shop@12345" } @{ "X-Forwarded-For" = $ipB }
$dupStaff = Send POST "$Api/storefront/auth/register" @{ name = "Sec Test"; email = "admin@platform.local"; password = "Shop@12345" } @{ "X-Forwarded-For" = $ipB }
$msgCustomer = ($dupCustomer.c | ConvertFrom-Json).detail
$msgStaff = ($dupStaff.c | ConvertFrom-Json).detail
Check "a customer's email and a staff email get the same answer (422)" ($dupCustomer.s -eq 422 -and $dupStaff.s -eq 422 -and $msgCustomer -eq $msgStaff)
Check "the answer does not confirm the email exists" ($msgCustomer -notmatch "already has an account" -and $dupCustomer.c -notmatch '"Email"')

"--- registration: 5 per 15 minutes per visitor"
$more = 1..3 | ForEach-Object { (Send POST "$Api/storefront/auth/register" @{ name = "Sec $_"; email = "sec$_-$sfx@shop.local"; password = "Shop@12345" } @{ "X-Forwarded-For" = $ipB }).s }
$sixth = Send POST "$Api/storefront/auth/register" @{ name = "Sec 9"; email = "sec9-$sfx@shop.local"; password = "Shop@12345" } @{ "X-Forwarded-For" = $ipB }
Check "attempts 4-5 answered (duplicates count too), 6th and later -> 429 ($($more -join ','),$($sixth.s))" ($more[0] -eq 200 -and $more[1] -eq 200 -and $more[2] -eq 429 -and $sixth.s -eq 429)
Check "the wait is shown in minutes" ($sixth.c -match "minutes")

"--- the web app passes each visitor's IP and shows the message"
$s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
function WebLogin($session) {
  $page = (Invoke-WebRequest "$Web/Account/Login" -WebSession $session -UseBasicParsing).Content
  $token = [regex]::Match($page, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"').Groups[1].Value
  (Invoke-WebRequest "$Web/Account/Login" -Method Post -WebSession $session -UseBasicParsing -Body @{ Email = "nobody$sfx@platform.local"; Password = "Wrong@12345"; __RequestVerificationToken = $token }).Content
}
$pages = 1..11 | ForEach-Object { WebLogin $s }
Check "after 10 tries the admin sign-in page says to wait" ($pages[10] -match "Too many requests")
