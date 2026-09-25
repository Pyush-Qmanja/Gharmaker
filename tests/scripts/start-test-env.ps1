# Starts Firebase emulators (firestore+auth, demo-platform), the test API on :5310 and the test Web on https :7331.
$ErrorActionPreference = "Stop"
$lim = "10000"
$sp = Join-Path $PSScriptRoot ".work"; New-Item -ItemType Directory -Force $sp | Out-Null
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'emulators:start|cloud-firestore-emulator|build\\api\\Platform.Api.dll|build\\web\\Platform.Web.dll' } |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
Start-Sleep -Seconds 2

if (-not (Test-Path "$sp\build\api\Platform.Api.dll") -or -not (Test-Path "$sp\build\web\Platform.Web.dll")) {
  Push-Location (Join-Path $PSScriptRoot "..\..\src")
  dotnet build Platform.Api -o "$sp\build\api" -v q -nologo | Select-String " error |Build succeeded" | Select-Object -First 3
  dotnet build Platform.Web -o "$sp\build\web" -v q -nologo | Select-String " error |Build succeeded" | Select-Object -First 3
  Pop-Location
}

$emuLog = "$sp\emu-t.log"
Start-Process cmd -ArgumentList "/c cd /d $repo && firebase emulators:start --only firestore,auth --project demo-platform > $emuLog 2>&1" -WindowStyle Hidden | Out-Null
$ok = $false
for ($i = 0; $i -lt 90; $i++) { Start-Sleep -Seconds 1; if ((Test-Path $emuLog) -and (Select-String -Path $emuLog -Pattern "All emulators ready" -Quiet)) { $ok = $true; break } }
if (-not $ok) { "EMULATOR FAILED"; Get-Content $emuLog -Tail 20; exit 1 }
"emulators ready"

foreach ($kv in @{
  "Firebase__ProjectId" = "demo-platform"; "Firebase__FirestoreEmulatorHost" = "127.0.0.1:8080"; "Firebase__AuthEmulatorHost" = "127.0.0.1:9099";
  "Firebase__CredentialsPath" = ""; "Firebase__WebApiKey" = "emulator-only";
  "Jwt__SigningKey" = "emulator-only-signing-key-not-a-secret-0123456789";
  "Seed__AdminEmail" = "admin@platform.local"; "Seed__AdminPassword" = "Emulator-Admin@2026";
  "RateLimiting__ReadsPerMinute" = "$lim"; "RateLimiting__WritesPerMinute" = "$lim"; "RateLimiting__SignInsPerMinute" = "$lim"; "RateLimiting__RegistrationsPerWindow" = "$lim"; "RateLimiting__CheckoutsPerMinute" = "$lim";
 "ASPNETCORE_URLS" = "http://localhost:5310"; "ASPNETCORE_ENVIRONMENT" = "Development" }.GetEnumerator()) {
  [Environment]::SetEnvironmentVariable($kv.Key, $kv.Value, "Process")
}
Start-Process dotnet -ArgumentList "$sp\build\api\Platform.Api.dll" -WorkingDirectory "$sp\build\api" -WindowStyle Hidden -RedirectStandardOutput "$sp\api-t.log" -RedirectStandardError "$sp\api-t.err" | Out-Null

[Environment]::SetEnvironmentVariable("ASPNETCORE_URLS", "https://localhost:7331", "Process")
[Environment]::SetEnvironmentVariable("Api__BaseUrl", "http://localhost:5310/", "Process")
Start-Process dotnet -ArgumentList "$sp\build\web\Platform.Web.dll" -WorkingDirectory "$sp\build\web" -WindowStyle Hidden -RedirectStandardOutput "$sp\web-t.log" -RedirectStandardError "$sp\web-t.err" | Out-Null

for ($i = 0; $i -lt 60; $i++) {
  Start-Sleep -Seconds 1
  try { $r = Invoke-WebRequest "http://localhost:5310/api/auth/login" -Method Post -ContentType "application/json" -Body '{"email":"admin@platform.local","password":"Emulator-Admin@2026"}' -UseBasicParsing; if ($r.StatusCode -eq 200) { "api ready"; break } } catch { }
}
for ($i = 0; $i -lt 30; $i++) {
  Start-Sleep -Seconds 1
  try { $r = Invoke-WebRequest "https://localhost:7331/Account/Login" -UseBasicParsing; if ($r.StatusCode -eq 200) { "web ready"; break } } catch { }
}
