# 2. Administration

[← SOP index](README.md)

Menu group **Administration**: Users, Roles, Audit log, Business settings.

## 2.1 How access works

Access has two parts: **what** a person may do (a feature at a level) and **where** (which
warehouses). Every action is checked by the server, so hiding a menu is only a convenience.

**Levels:** "No access", "View", "Manage". Manage includes View.

| Group | Feature | What "Manage" allows | Where it applies |
| --- | --- | --- | --- |
| Catalogue | Catalogue | Excel import of categories, products, SKUs; units | Whole organisation |
| Catalogue | Brands | Create, edit, deactivate brands | Whole organisation |
| Inventory | Warehouses | Create, edit, deactivate warehouses | Per warehouse |
| Inventory | Stock | Adjustments, opening stock, ledger check | Per warehouse |
| Inventory | Goods receipts | Post and reverse receipts | Per warehouse |
| Inventory | Transfers | Send (at the source) and receive (at the destination) | Per warehouse |
| Inventory | Delivery areas | Add, change, remove PIN codes | Per warehouse |
| Sales | Pricing and GST | Set prices and GST rates, manage price lists | Whole organisation |
| Sales | Customers | Change price tier, block or restore | Whole organisation |
| Sales | Orders | Confirm or cancel an order | Whole organisation |
| Administration | Users | Create, edit, deactivate users | Whole organisation |
| Administration | Roles | Create, edit, deactivate roles | Whole organisation |
| Administration | Audit log | (View only) | Whole organisation |
| Administration | Business settings | Edit legal name, GSTIN, address | Whole organisation |

**How a person's access adds up:**

- **Roles** — a standard bundle (e.g. "Store keeper"). On each user you choose **where the
  roles apply**: "Everywhere" or specific warehouses.
- **Feature access** — single features given directly to one user, each with its own
  places. Use it for exceptions, e.g. "can also view stock at the Nashik yard".
- The person gets **everything** from both. Changes take effect on their next click; no
  sign-out needed.
- **You can only give access you hold yourself**, in the places you hold it. A branch
  manager cannot make someone an administrator.
- A per-warehouse feature given through a role with **no place chosen gives nothing**.

### The role hierarchy — who can manage whom

Every role **reports to** another role, like an organisation chart, with
**Administrator** at the top. See it under Roles › **"Role hierarchy"**.

```
Administrator                 ← manages everything
├── Branch manager            ← manages Store keeper, Helper and the people holding them
│   └── Store keeper
│       └── Helper
└── Sales head                ← a separate branch: cannot touch Branch manager's people
    └── Sales executive
```

- **Administrator** always has every feature. Nobody can change, rename or deactivate
  it, and only administrators can make someone an administrator. There must always be
  at least one active administrator.
- Anyone else can create, change or deactivate only **roles below their own**, and
  only people whose roles are **all below theirs** (and who hold no access the manager
  lacks). Peers and people above you are out of reach.
- You cannot change **your own** roles or access — ask someone above you. You can still
  edit your own name and phone.
- On the user form, roles you may not give appear **greyed out**; a person keeps them.

### Changes reach people who are already signed in

| Change | When the signed-in person feels it |
| --- | --- |
| Role or feature access added / removed, places changed, role deactivated | Their **next click** — menus and permissions are re-read on every page; no sign-out needed |
| User deactivated | **At once** — their next click shows the sign-in page with "Your session has ended" |
| "Sign out everywhere" | **At once**, on every device; they can sign in again with their password |
| Name changed | Their next click shows the new name |

**Out-of-scope records:** a warehouse (or its stock, documents, PIN codes) outside your
places is invisible — lists skip it, and opening it by link shows "not found". Trying to
change it gives "You cannot change this … — it is outside the scope where you manage …".

## 2.2 Users

**Menu:** Administration › Users. View to list; Manage to create, edit, deactivate.

**List:** User (name, email), Roles, "Roles apply in", Feature access, Status, Last updated.
Sorted by name. **Search matches the start of the email**, not the name — type
`ramesh@` rather than `Ramesh`.

### Create a user ("+ New user", or Dashboard › "Invite a user")

1. **Profile** — "Full name", "Phone" (country code + number, optional), "Email" (their
   sign-in; **cannot be changed later**), "Initial password" (8+ characters). Give the
   password to the person privately.
2. **Roles** — tick **"Roles held"**, then choose **"Where the roles apply"**:
   "Everywhere" or one or more warehouses.
3. **Feature access** (optional) — for each extra feature choose "View" or "Manage" and
   tick where it applies. A per-warehouse feature at View/Manage **must** have a place, or
   you get "Choose where … access applies."
4. **"What this user can do"** shows the result *as last saved* — it does not update while
   you tick boxes. Save, then reopen to check.
5. Click **"Create"** → "User created."

### Edit a user

Same sections, without email and password. The **"Status"** panel has **"Active"**. Save →
"User saved."

### Deactivate a user (leaver, suspension)

Row ban icon → confirm, or untick "Active" in Edit. The person:

- cannot sign in (they see the generic "Email or password is incorrect."),
- is **signed out at once** on every device if already signed in — their next click
  shows "Your session has ended",
- stays on every record they created (history is kept).

Restore by ticking "Active" again (they sign in afresh). You cannot deactivate your own
account, anyone at or above your level, or the last active administrator.

### Sign out everywhere

- **Someone else** (lost phone, shared computer, suspicious sign-in): open the user →
  panel **"Sessions"** → **"Sign out everywhere"**. Their password still works.
- **Yourself**: your name (top right) → **"Sign out everywhere"** → confirm.

**Messages you may see**

| Message | Meaning |
| --- | --- |
| "This email already has an account." | Email already used. Search for the existing user. |
| "One or more roles do not exist or are inactive." | A role was deactivated meanwhile; reload. |
| "You can only give roles whose access you hold yourself…" | You tried to give more than you have. Ask a higher admin. |
| "You can only give or remove role scopes where you manage users yourself." | The warehouse is outside your places. |
| "You cannot deactivate your own account." | Ask another admin. |
| "You can only change people below you in the hierarchy." | That person is your peer or above you (or holds access you do not). Ask someone above you both. |
| "You can only give or remove roles below your own in the hierarchy." | The role is not below yours; it shows greyed out. |
| "You cannot change your own roles or access. Ask someone above you." | Only your profile is yours to edit. |
| "The organisation needs at least one active administrator…" | Make someone else an administrator first. |

## 2.3 Roles

**Menu:** Administration › Roles. View to list; Manage to change.

1. **"+ New role"** → **"Role name"** (unique, e.g. "Store keeper").
2. **"Reports to"** — the role above it (your own role, or one below it). Only people
   holding a role above it can change it or give it to others.
3. In **"Feature access"** choose "No access" / "View" / "Manage" per feature. At least one
   is needed. You can only give features you hold yourself.
4. **"Create"**.

- Places are **not** chosen on the role — they are chosen per user when the role is given.
- **Editing a role changes access for everyone holding it, on their next click.**
- **Deactivating a role** removes its access from everyone holding it; restore from Edit.
- One role is built in: **"Administrator"** — every feature, cannot be changed. Keep at
  least two active administrators.
- **"Role hierarchy"** (button on the Roles list) shows the chart and what each role can do.
- The search box on Roles does not filter; the list is short, scroll it.
- Nobody can widen their own role or a peer's: roles are managed only from above.

**Suggested roles** (create as needed):

| Role | Reports to | Access |
| --- | --- | --- |
| Store keeper | Inventory manager | Warehouses View; Stock Manage; Goods receipts Manage; Transfers Manage; Catalogue View |
| Inventory manager | Administrator | Store keeper + Delivery areas Manage + Warehouses Manage |
| Sales executive | Administrator (or a Sales head) | Catalogue View; Pricing and GST View; Customers Manage; Orders Manage; Stock View |
| Pricing manager | Administrator | Catalogue View; Pricing and GST Manage |
| Accounts | Administrator | Pricing and GST View; Orders View; Customers View; Business settings View; Audit log View |
| Auditor | Administrator | View on everything, Audit log View |

## 2.4 Audit log

**Menu:** Administration › Audit log. View only; nothing here can be edited.

- Columns: **When** (India time), **Who** (or "System"), **Action** (Created / Updated /
  Deactivated / Restored), **Record**, **Fields changed**, **From** (IP address).
- Filter by **kind of record** (Stock documents, Warehouses, Users, Roles, Brands, Units,
  Products, SKUs, Categories). Others (customers, orders, price lists, business settings)
  appear under "All records".
- Open an entry to see **Who, When, IP address, Device** and a **Before / After** table.
  **"Full history of this record"** shows every change to that one record.
- Field names are technical (`is_active`, `role_ids`); values are shown as stored.

Use it to answer: *who changed this price list? who gave Ramesh manage access? who
deactivated this warehouse?*

## 2.5 Business settings

**Menu:** Administration › Business settings. View to see; Manage to edit.

This is the registered business that invoices customers. **The store cannot take orders
until it is complete** ("The store is not taking orders yet.").

1. **Registration** — "Legal name" exactly as on the GST certificate; "GSTIN" (15
   characters).
2. **Registered address** — lines, City, **State**, PIN code.
3. **"Save business details"** → "Business details saved. GST is now worked out from
   this address."

- The GSTIN must belong to the chosen state: "A GSTIN registered in Maharashtra starts
  with 27."
- **The state decides GST on every order:** delivery in the same state → **CGST + SGST**;
  another state → **IGST**. The green banner confirms which.
- Warehouses are internal stock points and never appear on tax documents.
