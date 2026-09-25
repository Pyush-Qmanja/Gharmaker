# End-to-end test suites

PowerShell suites that drive the real API and web app against the **Firebase
emulators** — never a real Firebase project. About 490 checks across identity,
access and the role hierarchy, catalogue, stock, pricing and GST, the online
store, orders and security.

## Safety

- The test apps run on their own ports: **API http://localhost:5310**, **web
  https://localhost:7331**, emulators on 8080 (Firestore) and 9099 (Auth), project
  `demo-platform`. Your normal apps (7261 / 7031) and your real project are never touched.
- The test API gets throwaway settings from the scripts (emulator hosts, a JWT key
  and an admin `admin@platform.local` / `Emulator-Admin@2026` that exist only in the
  emulator). No real credential is needed or used.
- Working files (test builds, logs, screenshots) go to `tests/scripts/.work/`, which git ignores.

## Needs

.NET 8 SDK, Node.js, Java 11+ and the Firebase CLI (`npm i -g firebase-tools`) for the
emulators, and Windows PowerShell 5.1+. Screenshots (`shots.ps1`) use Microsoft Edge.

## Run

From the repository root, in PowerShell:

```powershell
# Fresh emulators + test API + test web (builds them the first time)
tests\scripts\start-test-env.ps1

# Suites (order matters for a few: run p3-test before ui-shop-test and p4-confirm-test)
tests\scripts\perm-test.ps1
tests\scripts\scope-test.ps1
tests\scripts\catalog-test.ps1
tests\scripts\access-test.ps1
tests\scripts\stock-test.ps1
tests\scripts\p3-test.ps1
tests\scripts\p4-confirm-test.ps1
tests\scripts\ui-test.ps1
tests\scripts\ui-scope-test.ps1
tests\scripts\ui-catalog-test.ps1
tests\scripts\ui-access-test.ps1
tests\scripts\ui-stock-test.ps1
tests\scripts\ui-shop-test.ps1
tests\scripts\hierarchy-test.ps1
```

Each line of output is `PASS` or `FAIL` with a label.

After code changes, rebuild and restart the test apps with `restart-api.ps1` and
`restart-web.ps1` (the emulators keep running). Special modes:

| Suite | Start the API with |
| --- | --- |
| `security-test.ps1` (rate limits) | `restart-api.ps1 -StrictLimits` |
| `p3-expiry-test.ps1`, `p4-confirm-test.ps1 -Expiry` (hold expiry) | `restart-api.ps1 -HoldMinutes 1 -SweepSeconds 5` |

`snapshot.ps1` / `shop-snapshot.ps1` save signed-in pages under
`src/Platform.Web/wwwroot/_preview` (git-ignored) and `shots.ps1` screenshots them.

Stop everything:

```powershell
Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'emulators:start|cloud-firestore-emulator|build\\api\\Platform.Api.dll|build\\web\\Platform.Web.dll' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
```

Unit and reflection tests are separate: `dotnet test Platform.sln`.
