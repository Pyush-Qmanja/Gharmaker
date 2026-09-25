# 8. Troubleshooting — message on screen → what to do

[← SOP index](README.md)

## Signing in and access

| Message | Cause | What to do |
| --- | --- | --- |
| "Email or password is incorrect." | Wrong details, or the account is deactivated | Retype carefully; otherwise ask an admin to check the user is Active |
| "Too many requests. Please wait … and try again." | Too many attempts or actions in a short time | Wait the time shown |
| Dashboard shows "No access yet" | No roles or feature access | Admin gives a role ([Scenario B](07-scenarios.md#b-onboard-a-new-employee)) |
| "You don't have access to this" | Your access does not include this action here | Ask an admin; say which screen and warehouse |
| "Add a warehouse first" (on Receive goods, Transfer, Adjust, Add PIN codes) | There is no active warehouse yet | Inventory › Warehouses › "New Warehouse", then try again |
| A warehouse is missing from filters | It is outside your places, or inactive | Ask an admin to add the place |
| Sent back to the sign-in page | The 60-minute session ended | Sign in again; unsaved input is lost |
| "Your session has ended. Please sign in again." | Deactivated, signed out everywhere by an admin, or the session expired | Sign in again; if refused, ask an admin whether you are active |
| New access not showing | — | Click any menu item; changes apply on the next click without signing out |
| "Something went wrong … reference: …" | Unexpected error | Send the reference to support |

## Users and roles

| Message | What to do |
| --- | --- |
| "Choose where … access applies." | Tick at least one place for that feature, or set it to "No access" |
| "You can only give roles whose access you hold yourself…" / "You can only give or remove access you hold yourself…" | A higher admin must give it |
| "This email already has an account." | Find and reactivate the existing user instead |
| "You cannot deactivate your own account." | Ask another admin |
| "Choose at least one capability." | Give the role at least one feature |
| "You can only change people below you in the hierarchy." | They are your peer or above you; ask someone above you both |
| "You can only change roles below your own in the hierarchy." | Only roles under yours in the chart are yours to edit |
| "You can only give or remove roles below your own in the hierarchy." | Greyed-out roles are not yours to give |
| "You can only give or remove features you hold yourself." | Ask someone who holds the feature |
| "You cannot change your own roles or access. Ask someone above you." | Only your profile is yours to edit |
| "The Administrator role always has every feature and cannot be changed." | By design; create another role instead |
| "The organisation needs at least one active administrator…" | Make someone else an administrator first |
| "A role cannot report to itself or to a role below it." | Choose a role higher up for "Reports to" |

## Catalogue import

| Message | What to do |
| --- | --- |
| "Missing column(s): …" | Start from the downloaded template; do not rename headers |
| "At most 5000 rows per file…" | Split the file |
| "Unknown base unit 'X'…" | Use a code from the template's Units sheet, or add the unit first |
| "SKU code X also appears on row N." | Each SKU code once per file |
| "…has a different category or HSN on row N…" | All rows of one product must share category and HSN |
| "SKU code X already belongs to another product." | You renamed the product or brand; keep the original names |
| "That preview has expired. Upload the file again." | More than 30 minutes passed; upload again |

## Stock

| Message | Cause | What to do |
| --- | --- | --- |
| "Not enough stock — … on hand, … held for orders, … needed." | You are taking more than Available | Check Reserved; wait for or cancel orders; reduce the amount |
| "X cannot be counted in 'U'. Use one of: …" | That unit has no conversion for this SKU | Use one of the listed units |
| "SKU X is inactive." | SKU switched off in the catalogue | Re-activate by import if needed |
| "Choose an active warehouse." | Warehouse deactivated | Pick another, or restore it |
| "A SKU is listed twice; put it on one line." | Same SKU on two lines | Add the amounts into one line |
| "For damage and expiry enter the amount lost as a positive number…" | Negative amount on Damage/Expiry | Enter it positive |
| "Say why, in a few words." | "Reason in words" empty | Fill it in |
| "… is held in {old} … but its base unit is now {new}…" | SKU's base unit changed after stock was booked | Contact the development team |
| "Checking the ledger needs stock management in every warehouse." | Ledger check needs Stock Manage everywhere | Ask the inventory manager |
| "Someone else is changing the same stock right now…" | Two postings at once | Try again |
| Reversal refused | Stock already used, or already reversed | Post a correcting document instead |

## Prices, GST, customers, orders

| Message | What to do |
| --- | --- |
| "There is already an active retail list…" | Only one Retail list; create a Tier list instead |
| "The retail list stays active…" | Retail cannot be deactivated; change its prices |
| "A price cannot start in the past…" | Leave "Starts" empty for now, or pick a future time |
| "The first slab must start at 0." / "Two slabs start at the same quantity." | Fix the slab rows |
| "{SKU} cannot be priced in {UOM}." | Choose a unit the SKU can be counted in |
| "This price list is deactivated. Restore it before setting prices." | Edit list → tick Active |
| "A GSTIN registered in {State} starts with {code}." | GSTIN and state do not match; correct one of them |
| "Choose an active tier price list." | The tier was deactivated; pick another |
| "Order … is already cancelled / expired." | Nothing to do; it is closed |
| "Order … is confirmed, not waiting for confirmation." | Already confirmed; nothing to do |
| "The stock hold for … has run out…" | The 48 hours passed before confirming; ask the customer to order again |

## Customer-side messages

See [Scenario K](07-scenarios.md#k-the-store-says-it-isnt-available) for availability
problems. Others:

| Customer sees | Cause | Fix |
| --- | --- | --- |
| "That email and password do not match a store account." | Wrong details or blocked | Check the customer is not blocked (Sales › Customers) |
| "We could not create an account with these details…" | Email already registered (as customer or staff) | Ask them to sign in instead |
| "Your session has ended. Please sign in again." | Session expired or account blocked | Sign in again |
| "A cart holds at most 50 items. Place this order first." | Cart full | Place the order, then start another |
| "Order … is confirmed. Please call us to change or cancel it." | The store already confirmed it | Staff change or cancel it from Sales › Orders |
| "Enter a 10-digit Indian mobile number…" | +91 chosen but number is not a 10-digit mobile | Correct the number or choose the right country code |
| "…someone has just ordered some of these items…" | Another customer bought the stock first | Update the cart and try again |
| "These items cannot all be delivered together…" | No single warehouse has every item (orders are never split) | Order some items separately, or staff transfer stock into one warehouse |
