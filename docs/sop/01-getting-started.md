# 1. Getting started

[← SOP index](README.md)

## 1.1 Signing in

1. Open the portal. You land on **"Welcome back"**.
2. Enter your **"Work email"** and **"Password"** and click **"Sign in →"**.
3. You go to the Dashboard (or back to the page you were trying to open).

| Situation | What happens |
| --- | --- |
| Wrong email or password | "Email or password is incorrect." The same message is shown for an unknown email or a deactivated account, on purpose, so nobody can probe which emails exist. |
| 10 attempts in a minute from the same place | "Too many requests. Please wait … and try again." Wait and retry. |
| Forgot password / need an account | There is no self-service reset yet. Ask your administrator. |

**Session length:** you stay signed in for **60 minutes from sign-in**, whether you are
active or not, and closing the browser also signs you out. When the session ends you are
sent back to the sign-in page and returned to where you were afterwards. Unsaved form
input is lost, so save long forms promptly.

**Signing out:** click your name (top right) → **"Sign out"**. Always sign out on a
shared computer. Forgot to? **"Sign out everywhere"** in the same menu ends every
session you have, on every device.

**"Your session has ended. Please sign in again."** means an administrator deactivated
you or signed you out everywhere, or the session timed out. Changes to your access do
**not** sign you out — they simply apply from your next click.

## 1.2 The screen layout

- **Left sidebar** — "Dashboard", then groups **Catalogue**, **Inventory**, **Sales**,
  **Administration**. You only see the screens you have access to. On a phone, open it
  with the menu button.
- **Top bar** — where you are (Group › Screen › Page), **"View store"** (opens the
  online store in a new tab) and your account menu.
- **Dashboard** — a greeting, **"Your modules"** (one card per screen you can open, with
  a count, e.g. how many orders are awaiting confirmation) and **"Quick actions"**
  (e.g. "Receive goods", "Send a transfer", "Invite a user") — again, only the ones you
  may do.

If the dashboard says **"No access yet"**, your account has no roles or feature
access. Ask an administrator (see [Scenario B](07-scenarios.md#b-onboard-a-new-employee)).

## 1.3 Lists

Most screens start with a list.

- **Search** — type and press Enter; **×** clears it. What search matches differs by
  screen (it is stated in each guide — e.g. Users search by the *start of the email*).
- **Filters** — drop-downs such as warehouse or brand apply as soon as you change them.
- **Pages** — 20 rows per page; "Previous" / "Next" at the bottom.
- **Row actions** — pencil = **"Edit"**; ban icon = **"Deactivate"** (asks
  "Deactivate this record? It can be restored from Edit.").
- **Status** — "Active" / "Inactive".

## 1.4 Forms

- Required fields are checked when you save. Errors appear **under the field**; problems
  that belong to no single field appear in the **red box at the top**.
- **"Cancel"** leaves without saving. **"Create"** / **"Save changes"** saves; changes
  apply immediately.
- **Active checkbox** (on Edit) — untick to deactivate, tick to restore. Inactive records
  stay in history but cannot be picked or used.

### Phone numbers

Every phone field has a **country-code drop-down** (default **+91 India**) and a box for
the number. Type the number any way you like — `98765 43210`, `098765-43210` — spaces,
dashes and a leading 0 are removed. It is stored in international form, e.g.
`+919876543210`.

- With +91 chosen the number must be **10 digits starting with 6, 7, 8 or 9**.
- For other countries, choose the code and type the local number.
- If you type a number that already starts with `+`, the drop-down is ignored.

## 1.5 Messages

- **Green** messages ("User created.", "GRN-2026-000012 posted.") disappear after
  5 seconds.
- **Red** messages stay until you close them — read them; they say what to fix.
- **"You don't have access to this"** — your access does not include that action
  there. Ask an administrator if you need it.
- **"Something went wrong … quote this reference: …"** — note the reference and send it
  to support.
- **"Someone else is changing the same stock right now. Nothing was saved"** — two
  people posted at once; simply try again.

## 1.6 Nothing is deleted

- **Masters** (users, roles, brands, units, warehouses, price lists, customers) are
  **deactivated**, never deleted, and can be restored from Edit.
- **Documents** (receipts, transfers, adjustments, opening stock) are never edited or
  deleted. A mistake is undone by **reversing** the document, which posts a new `REV-`
  document.
- **Everything is audited** — see [Audit log](02-administration.md#24-audit-log).
