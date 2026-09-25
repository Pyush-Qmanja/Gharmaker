# Rebuilds the Web project into the test build folder and restarts it on https :7331 (API stays up).
$sp = Join-Path $PSScriptRoot ".work"; New-Item -ItemType Directory -Force $sp | Out-Null
Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'build\\web\\Platform.Web.dll' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
Start-Sleep 1
Push-Location (Join-Path $PSScriptRoot "..\..\src")
dotnet build Platform.Web -o "$sp\build\web" -v q -nologo 2>&1 | Select-String " error |Build succeeded" | Select-Object -First 5
Pop-Location
[Environment]::SetEnvironmentVariable("ASPNETCORE_URLS", "https://localhost:7331", "Process")
[Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development", "Process")
[Environment]::SetEnvironmentVariable("Api__BaseUrl", "http://localhost:5310/", "Process")
Start-Process dotnet -ArgumentList "$sp\build\web\Platform.Web.dll" -WorkingDirectory "$sp\build\web" -WindowStyle Hidden -RedirectStandardOutput "$sp\web-t.log" -RedirectStandardError "$sp\web-t.err" | Out-Null
for ($i = 0; $i -lt 30; $i++) { Start-Sleep 1; try { if ((Invoke-WebRequest "https://localhost:7331/Account/Login" -UseBasicParsing).StatusCode -eq 200) { "web ready"; break } } catch { } }
Remove-Item -Recurse -Force "$sp\edge-profile" -ErrorAction SilentlyContinue
