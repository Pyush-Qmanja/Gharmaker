---
paths:
  - "src/Platform.Mobile.Supervisor/**/*.cs"
  - "src/Platform.Mobile.Driver/**/*.cs"
  - "src/Platform.Api/Controllers/Sync/**/*.cs"
---

# Mobile — offline-first field capture (P5)

The supervisor app runs on a mid-range Android phone, on a site, with two bars
of signal or none. Offline is the normal case, not the error case.

## Targets

- A 50-person site marked and submitted in **under two minutes**.
- Fully usable with the network off for an entire working day.
- Works on **Android 9, 2 GB RAM**. Measure on that, not on a flagship.

## Sync rules

1. **Write locally first, always.** The UI never waits on the network. SQLite is
   the app's source of truth until the server acknowledges.
2. **Every record carries a client-generated UUID** (`client_id`) and a device
   timestamp, set at capture time on the device.
3. **Sync is an idempotent batch upload keyed by `client_id`.** The same batch
   sent five times produces one result. The server returns the original outcome
   on any repeat.
4. **The server returns three outcomes per record** — accepted, rejected with a
   reason, or flagged for review. The app shows each one plainly. Never a silent
   drop.
5. **Conflicts are surfaced, never resolved silently.** A same-day duplicate
   becomes a review item for the project manager.
6. **The sync status screen is a feature, not debug UI.** How many records are
   waiting and how old the oldest one is. This is how a supervisor knows today's
   marking actually reached the office.
7. **Retry with backoff.** Never a tight retry loop — it drains the battery of a
   phone that has to last a full shift.
8. **Warn loudly before any action that discards unsynced records.** A reinstall
   with queued attendance is real data loss and the app must say so.

## Server side

- Store both `device_time` and `server_time`. Never overwrite device time.
- `attendance` has a unique constraint on `(site_id, member_id, date)`. A second
  mark is an adjustment with a reason, never a second row.
- Geofence validation **warns, it does not block.** Accept the record, flag it
  for review. Blocking fails on the day GPS drifts, and that day will come.

## UI

- Bulk marking is the default path. Individual taps are for exceptions only.
- Touch targets at least 48 dp. The attendance screen is usable one-handed.
- Hindi and English, switchable per user.
- Validation and business rules shared with the API as a common C# project — the
  wage-band rule is written once and runs in both places.
