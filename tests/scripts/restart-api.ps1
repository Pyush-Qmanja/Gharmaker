# Rebuilds the API into the test build folder and restarts it on :5310 against the running emulators (Web stays up).
param([int]$HoldMinutes = 2880, [int]$SweepSeconds = 60, [switch]$StrictLimits)
# Functional suites sign in and write far faster than people do: generous limits unless -StrictLimits (production defaults).
$lim = if ($StrictLimits) { "" } else { "10000" }
$sp = Join-Path $PSScriptRoot ".work"; New-Item -ItemType Directory -Force $sp | Out-Null
Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'build\\api\\Platform.Api.dll' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
Start-Sleep 1
Push-Location (Join-Path $PSScriptRoot "..\..\src")
dotnet build Platform.Api -o "$sp\build\api" -v q -nologo 2>&1 | Select-String " error |Build succeeded" | Select-Object -First 5
Pop-Location
foreach ($kv in @{
  "Firebase__ProjectId" = "demo-platform"; "Firebase__FirestoreEmulatorHost" = "127.0.0.1:8080"; "Firebase__AuthEmulatorHost" = "127.0.0.1:9099";
  "Firebase__CredentialsPath" = ""; "Firebase__WebApiKey" = "emulator-only";
  "Jwt__SigningKey" = "emulator-only-signing-key-not-a-secret-0123456789";
  "Seed__AdminEmail" = "admin@platform.local"; "Seed__AdminPassword" = "Emulator-Admin@2026";
  "RateLimiting__ReadsPerMinute" = "$lim"; "RateLimiting__WritesPerMinute" = "$lim"; "RateLimiting__SignInsPerMinute" = "$lim"; "RateLimiting__RegistrationsPerWindow" = "$lim"; "RateLimiting__CheckoutsPerMinute" = "$lim";
 "ASPNETCORE_URLS" = "http://localhost:5310"; "ASPNETCORE_ENVIRONMENT" = "Development";
  "Storefront__HoldMinutes" = "$HoldMinutes"; "Storefront__ExpirySweepSeconds" = "$SweepSeconds" }.GetEnumerator()) {
  [Environment]::SetEnvironmentVariable($kv.Key, $kv.Value, "Process")
}
Start-Process dotnet -ArgumentList "$sp\build\api\Platform.Api.dll" -WorkingDirectory "$sp\build\api" -WindowStyle Hidden -RedirectStandardOutput "$sp\api-t.log" -RedirectStandardError "$sp\api-t.err" | Out-Null
for ($i = 0; $i -lt 60; $i++) {
  Start-Sleep 1
  try { $r = Invoke-WebRequest "http://localhost:5310/api/auth/login" -Method Post -ContentType "application/json" -Body '{"email":"admin@platform.local","password":"Emulator-Admin@2026"}' -UseBasicParsing; if ($r.StatusCode -eq 200) { "api ready"; break } } catch { }
}
