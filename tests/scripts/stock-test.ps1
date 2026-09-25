param([string]$Base = "http://localhost:5310/api")
Add-Type -AssemblyName System.Net.Http
function Call($m, $u, $body, $tok) {
  try {
    $h = @{}; if ($tok) { $h.Authorization = "Bearer $tok" }
    $a = @{ Method = $m; Uri = "$Base/$u"; Headers = $h; UseBasicParsing = $true }
    if ($null -ne $body) { $a.ContentType = "application/json"; $a.Body = ($body | ConvertTo-Json -Depth 8) }
    $r = Invoke-WebRequest @a
    return @{ s = [int]$r.StatusCode; d = ($(if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null })) }
  } catch {
    $resp = $_.Exception.Response
    $c = if ($resp) { (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } else { $_.Exception.Message }
    return @{ s = [int]$resp.StatusCode; d = $c }
  }
}
function Check($label, $actual, $expected) { "{0}  {1,-72} got {2}, want {3}" -f ($(if ("$actual" -eq "$expected") { "PASS" } else { "FAIL" })), $label, $actual, $expected }
function Wh($code, $name) { @{ code = $code; name = $name; type = "Owned"; address = @{ line1 = "1"; city = "Pune"; state = "MH"; pincode = "411001" } } }
function Login($email, $pw) { (Call POST "auth/login" @{ email = $email; password = $pw }).d.accessToken }
function Line($sku, $qty, $uom) { @{ skuCode = $sku; quantity = $qty; uom = $uom } }
function OnHand($wh, $sku, $tok) { $r = Call GET "stock/balances?warehouseId=$wh&search=$sku" $null $tok; $row = $r.d.items | Where-Object skuCode -eq $sku; if ($row) { [decimal]$row.onHand } else { 0 } }

$sfx = Get-Random -Maximum 99999
$CEM = "CEM-ULT-OPC53-50KG"; $TMT = "TMT-TISCON-12MM"
$admin = Login "admin@platform.local" "Emulator-Admin@2026"
$A = (Call POST "warehouses" (Wh "ST-A-$sfx" "Stock Alpha") $admin).d
$B = (Call POST "warehouses" (Wh "ST-B-$sfx" "Stock Beta") $admin).d

"--- goods receipt with unit conversion (P7)"
$grn = Call POST "stock/receipts" @{ warehouseId = $A.id; supplierName = "UltraTech Dealer"; supplierReferenceNo = "INV-77"; lines = @((Line $CEM 2 "TONNE"), (Line $TMT 100 "PCS"), (Line "" 0 "")) } $admin
Check "receipt posted (blank line ignored)" $grn.s 201
Check "reference number GRN-yyyy-nnnnnn" ($grn.d.referenceNo -match '^GRN-\d{4}-\d{6}$') True
Check "2 TONNE cement = 40 BAG; 100 PCS 12 mm = 1066 KG" (($grn.d.lines | ForEach-Object { "$($_.baseQuantity) $($_.baseUom)" }) -join ", ") "40 BAG, 1066 KG"
Check "balance A cement" (OnHand $A.id $CEM $admin) 40
Check "balance A steel" (OnHand $A.id $TMT $admin) 1066

"--- validation"
$bad = Call POST "stock/receipts" @{ warehouseId = $A.id; supplierName = "X"; lines = @((Line "NOPE-1" 1 "BAG"), (Line $CEM 1 "SQFT")) } $admin
Check "unknown SKU and wrong unit -> 400" $bad.s 400
Check "errors on each line" (($bad.d -match 'Lines\[0\]\.SkuCode') -and ($bad.d -match 'Lines\[1\]\.Uom') -and ($bad.d -match 'cannot be counted in SQFT')) True
Check "zero quantity -> 400" (Call POST "stock/receipts" @{ warehouseId = $A.id; supplierName = "X"; lines = @((Line $CEM 0 "BAG")) } $admin).s 400
Check "no lines -> 400" (Call POST "stock/receipts" @{ warehouseId = $A.id; supplierName = "X"; lines = @((Line "" 0 "")) } $admin).s 400
Check "same SKU twice -> 400" (Call POST "stock/receipts" @{ warehouseId = $A.id; supplierName = "X"; lines = @((Line $CEM 1 "BAG"), (Line $CEM 1 "TONNE")) } $admin).s 400

"--- transfer: send, then receive (two steps)"
$trf = Call POST "stock/transfers" @{ warehouseId = $A.id; toWarehouseId = $B.id; lines = @((Line $CEM 10 "BAG")) } $admin
Check "transfer sent, in transit" "$($trf.s) $($trf.d.status)" "201 InTransit"
Check "A down to 30 at once" (OnHand $A.id $CEM $admin) 30
Check "B still empty while in transit" (OnHand $B.id $CEM $admin) 0
Check "send to same warehouse -> 400" (Call POST "stock/transfers" @{ warehouseId = $A.id; toWarehouseId = $A.id; lines = @((Line $CEM 1 "BAG")) } $admin).s 400
$rcv = Call POST "stock/transfers/$($trf.d.id)/receive" $null $admin
Check "received" "$($rcv.s) $($rcv.d.status)" "200 Received"
Check "B has 10" (OnHand $B.id $CEM $admin) 10
Check "receive again -> 422" (Call POST "stock/transfers/$($trf.d.id)/receive" $null $admin).s 422

"--- stock never goes negative"
$over = Call POST "stock/transfers" @{ warehouseId = $A.id; toWarehouseId = $B.id; lines = @((Line $CEM 1000 "BAG")) } $admin
Check "sending more than on hand -> 422" $over.s 422
Check "message names SKU, warehouse and amounts" ($over.d -match "Not enough stock" -and $over.d -match "30 BAG on hand") True
Check "A unchanged" (OnHand $A.id $CEM $admin) 30

"--- adjustments"
$dmg = Call POST "stock/adjustments" @{ warehouseId = $A.id; reason = "Damage"; remarks = "Bags torn in rain"; lines = @((Line $CEM 2 "BAG")) } $admin
Check "damage 2 BAG" "$($dmg.s) $($dmg.d.referenceNo -match '^ADJ-')" "201 True"
Check "A cement 28" (OnHand $A.id $CEM $admin) 28
Check "count correction -3 KG and +5 KG" "$((Call POST 'stock/adjustments' @{ warehouseId = $A.id; reason = 'AuditCorrection'; remarks = 'Cycle count'; lines = @((Line $TMT -3 'KG')) } $admin).s) $((Call POST 'stock/adjustments' @{ warehouseId = $A.id; reason = 'AuditCorrection'; remarks = 'Recount'; lines = @((Line $TMT 5 'KG')) } $admin).s)" "201 201"
Check "A steel 1068" (OnHand $A.id $TMT $admin) 1068
Check "damage entered as negative -> 400" (Call POST "stock/adjustments" @{ warehouseId = $A.id; reason = "Damage"; remarks = "x"; lines = @((Line $CEM -1 "BAG")) } $admin).s 400
Check "adjustment without remarks -> 400" (Call POST "stock/adjustments" @{ warehouseId = $A.id; reason = "AuditCorrection"; lines = @((Line $CEM 1 "BAG")) } $admin).s 400
Check "GRN reason not allowed on adjustment -> 400" (Call POST "stock/adjustments" @{ warehouseId = $A.id; reason = "GrnReceipt"; remarks = "x"; lines = @((Line $CEM 1 "BAG")) } $admin).s 400

"--- reversals (P9: documents are never edited)"
Check "reverse receipt when stock already used -> 422" (Call POST "stock/documents/$($grn.d.id)/reverse" @{ remarks = "Wrong supplier" } $admin).s 422
$rev = Call POST "stock/documents/$($dmg.d.id)/reverse" @{ remarks = "Bags were fine after all" } $admin
Check "reverse damage" "$($rev.s) $($rev.d.referenceNo -match '^REV-') $($rev.d.reversesDocumentId -eq $dmg.d.id)" "201 True True"
Check "A cement back to 30" (OnHand $A.id $CEM $admin) 30
Check "original now Reversed" (Call GET "stock/documents/$($dmg.d.id)" $null $admin).d.status "Reversed"
Check "reverse it again -> 422" (Call POST "stock/documents/$($dmg.d.id)/reverse" @{ remarks = "again" } $admin).s 422
Check "reverse the reversal -> 422" (Call POST "stock/documents/$($rev.d.id)/reverse" @{ remarks = "again" } $admin).s 422
Check "reversal without remarks -> 400" (Call POST "stock/documents/$($grn.d.id)/reverse" @{ remarks = "" } $admin).s 400
$revT = Call POST "stock/documents/$($trf.d.id)/reverse" @{ remarks = "Sent to wrong yard" } $admin
Check "reverse received transfer: A 40, B 0" "$($revT.s) $(OnHand $A.id $CEM $admin) $(OnHand $B.id $CEM $admin)" "201 40 0"

"--- ledger"
$led = Call GET "stock/ledger?warehouseId=$($A.id)&skuId=$($grn.d.lines[0].skuId)&pageSize=50" $null $admin
Check "cement movements at A (receipt, out, damage, reversal x2)" $led.d.totalCount 5
Check "newest entry's balance-after equals on hand" ([decimal]$led.d.items[0].balanceAfter) 40
Check "reversal entries name the entry they cancel" (@($led.d.items | Where-Object { $_.reversesEntryId }).Count) 2

"--- warehouse scope (P6)"
$kEmail = "keeper$sfx@platform.local"
$null = Call POST "users" @{ name = "Alpha Keeper"; email = $kEmail; password = "Keeper@12345"; roleIds = @(); scopes = @();
  access = @(@{ feature = "stock"; level = "View"; scopes = @(@{ scopeType = "Warehouse"; scopeId = $A.id }) },
             @{ feature = "receipts"; level = "Manage"; scopes = @(@{ scopeType = "Warehouse"; scopeId = $A.id }) },
             @{ feature = "transfers"; level = "Manage"; scopes = @(@{ scopeType = "Warehouse"; scopeId = $A.id }) }) } $admin
$k = Login $kEmail "Keeper@12345"
$kw = (Call GET "stock/warehouses" $null $k).d
Check "keeper's warehouses: only A, can receive and transfer, not adjust" "$(@($kw).Count) $($kw[0].code) $($kw[0].canReceive) $($kw[0].canTransfer) $($kw[0].canAdjust)" "1 ST-A-$sfx True True False"
Check "keeper receives into A" (Call POST "stock/receipts" @{ warehouseId = $A.id; supplierName = "Local"; lines = @((Line $CEM 5 "BAG")) } $k).s 201
Check "keeper receives into B -> 403" (Call POST "stock/receipts" @{ warehouseId = $B.id; supplierName = "Local"; lines = @((Line $CEM 5 "BAG")) } $k).s 403
Check "keeper adjusts A (view only) -> 403" (Call POST "stock/adjustments" @{ warehouseId = $A.id; reason = "Damage"; remarks = "x"; lines = @((Line $CEM 1 "BAG")) } $k).s 403
Check "keeper views B's stock -> 404" (Call GET "stock/balances?warehouseId=$($B.id)" $null $k).s 404
Check "keeper's stock list shows only A" ((@((Call GET "stock/balances?pageSize=100" $null $k).d.items | ForEach-Object warehouseCode | Sort-Object -Unique)) -join ",") "ST-A-$sfx"
$toA = Call POST "stock/transfers" @{ warehouseId = $B.id; toWarehouseId = $A.id; lines = @((Line $TMT 1 "KG")) } $admin
Check "admin can't send from empty B -> 422" $toA.s 422
$toB = Call POST "stock/transfers" @{ warehouseId = $A.id; toWarehouseId = $B.id; lines = @((Line $TMT 10 "KG")) } $k
Check "keeper sends A -> B" $toB.s 201
Check "keeper receives into B (not theirs) -> 403" (Call POST "stock/transfers/$($toB.d.id)/receive" $null $k).s 403
Check "keeper sees the transfer (touches A)" (Call GET "stock/documents/$($toB.d.id)" $null $k).s 200
Check "keeper can't see the admin's B-only documents" (@((Call GET "stock/documents?type=Transfer&pageSize=100" $null $k).d.items | Where-Object { $_.warehouseIds -notcontains $A.id -and $_.warehouseId -ne $A.id -and $_.toWarehouseId -ne $A.id }).Count) 0
Check "keeper can't run the ledger check -> 403" (Call GET "stock/reconcile" $null $k).s 403

"--- concurrency: 12 transfers of 5 BAG race for 45 BAG"
$start = OnHand $A.id $CEM $admin
$client = New-Object System.Net.Http.HttpClient; $client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", $admin)
$body = (@{ warehouseId = $A.id; toWarehouseId = $B.id; lines = @((Line $CEM 5 "BAG")) } | ConvertTo-Json -Depth 5)
$tasks = 1..12 | ForEach-Object { $client.PostAsync("$Base/stock/transfers", (New-Object System.Net.Http.StringContent($body, [Text.Encoding]::UTF8, "application/json"))) }
[System.Threading.Tasks.Task]::WaitAll($tasks)
$codes = $tasks | ForEach-Object { [int]$_.Result.StatusCode }
$ok = @($codes | Where-Object { $_ -eq 201 }).Count; $refused = @($codes | Where-Object { $_ -eq 422 }).Count; $busy = @($codes | Where-Object { $_ -eq 409 }).Count
"      12 racing transfers: $ok posted, $refused refused (not enough), $busy told to retry"
Check "started with 45 BAG" $start 45
Check "every answer is posted / not enough / busy (no errors)" ($ok + $refused + $busy) 12
Check "never more than the 9 that fit" ($ok -le 9) True
Check "A = 45 - 5 x posted: nothing lost or double-spent" (OnHand $A.id $CEM $admin) (45 - 5 * $ok)
Check "most get through" ($ok -ge 6) True

"--- opening stock import"
$csv = "Warehouse Code,SKU Code,Quantity,Unit`nST-B-$sfx,$TMT,1.5,TONNE`nST-B-$sfx,CEM-AMB-OPC53-50KG,80,BAG"
$badCsv = "Warehouse Code,SKU Code,Quantity,Unit`nNOWHERE,$TMT,1,KG`nST-B-$sfx,$TMT,-4,KG`nST-B-$sfx,$TMT,1,SQFT"
function Upload($text, $name, $tok) {
  $c = New-Object System.Net.Http.HttpClient; $c.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", $tok)
  $form = New-Object System.Net.Http.MultipartFormDataContent
  $form.Add((New-Object System.Net.Http.ByteArrayContent(,[Text.Encoding]::UTF8.GetBytes($text))), "file", $name)
  $r = $c.PostAsync("$Base/stock/opening/preview", $form).Result
  return @{ s = [int]$r.StatusCode; d = ($r.Content.ReadAsStringAsync().Result | ConvertFrom-Json) }
}
$p1 = Upload $badCsv "bad.csv" $admin
Check "bad file: 3 errors, cannot commit" "$($p1.s) $($p1.d.errorRows) $($p1.d.canCommit)" "200 3 False"
$p2 = Upload $csv "opening.csv" $admin
Check "good file: 2 rows ok, 1.5 TONNE = 1500 KG" "$($p2.d.validRows) $($p2.d.rows[0].baseQuantity)" "2 1500"
$cm = Call POST "stock/opening/$($p2.d.importId)/commit" $null $admin
Check "committed as one OPN document" "$($cm.s) $(@($cm.d.referenceNos).Count) $($cm.d.referenceNos[0] -match '^OPN-')" "200 1 True"
Check "B steel = 1500 (the keeper's 10 KG is still in transit)" (OnHand $B.id $TMT $admin) 1500
Check "commit again -> 404" (Call POST "stock/opening/$($p2.d.importId)/commit" $null $admin).s 404
Check "keeper (no adjust) previews opening -> 403" (Upload $csv "x.csv" $k).s 403

"--- audit log (P10)"
$au = Call GET "audit?entity=Warehouse&entityId=$($A.id)" $null $admin
Check "warehouse creation audited" "$($au.d.items[0].action) $($au.d.items[0].label)" "Created ST-A-$sfx"
$docAudit = Call GET "audit?entity=StockDocument&pageSize=100" $null $admin
Check "stock documents audited" ($docAudit.d.totalCount -ge 10) True
$kUser = ((Call GET "users?search=keeper$sfx" $null $admin).d.items)[0]
$null = Call PUT "users/$($kUser.id)" @{ name = "Alpha Keeper Renamed"; phone = $null; roleIds = @(); scopes = @(); access = $kUser.access; isActive = $true } $admin
$ua = (Call GET "audit?entity=User&entityId=$($kUser.id)" $null $admin).d.items[0]
Check "user rename audited with before and after" "$($ua.action) $(($ua.changes | Where-Object field -eq 'name').before) $(($ua.changes | Where-Object field -eq 'name').after)" "Updated `"Alpha Keeper`" `"Alpha Keeper Renamed`""
Check "who and from where recorded" (($ua.createdByName -eq "Administrator") -and ($ua.device -ne $null)) True
Check "keeper can't read the audit log -> 403" (Call GET "audit" $null $k).s 403
Check "ledger entries are not audited (they are the trail)" (Call GET "audit?entity=StockLedgerEntry" $null $admin).d.totalCount 0

"--- the Phase 2 gate: every balance agrees with its ledger"
$rc = Call GET "stock/reconcile" $null $admin
Check "reconcile: no mismatches" "$($rc.s) $(@($rc.d.mismatches).Count)" "200 0"
"      checked $($rc.d.entriesChecked) ledger entries against $($rc.d.balancesChecked) balances"
