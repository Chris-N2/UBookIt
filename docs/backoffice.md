# The uBookIt backoffice section

Installing uBookIt adds a **uBookIt** section to the Umbraco backoffice, alongside
Content, Media and the rest. It is where a site's bookable resources and services are
configured, and where the bookings it has taken are read.

## Who can use it

Access has two steps, both in **Users → User Groups → *(a group)***: tick **uBookIt
Section** under *Sections*, then tick what the group may do under *Default permissions* —
**tick the section, then tick what they may do**. The section grant is the outer gate: it
decides whether the section appears and whether uBookIt's management API may be called at
all. The permissions decide what within it:

| Permission | What it allows |
|---|---|
| **See bookings** | The Bookings list and its reads. Contact details still need the Sensitive data group on top — see below. |
| **Act on bookings** | Cancel, confirm, decline and move. Includes seeing them: acting on what you cannot see makes no sense, so this needs no second tick. |
| **Configure resources and services** | Create, edit and delete resources and services, and assign who is responsible for them. |
| **Change site settings** | The Settings screen — how bookings behave and who is told about them. See [Configuration](configuration.md). **Not granted automatically, including on upgrade.** |

A group with the section and no uBookIt permissions sees the section shell and nothing in
it. Permissions union across a user's groups, and none of them ever substitutes for the
section grant — a group holding every permission but not the section reaches nothing.

**Upgrading from a version before these permissions existed?** Every group that already
held the uBookIt section is granted the first three automatically, once, at the first start
— so nobody loses access by upgrading. Groups you create afterwards start with nothing
ticked, and a group whose permissions you later empty stays emptied.

> **"Change site settings" is never granted automatically — on upgrade or otherwise, and
> not even to Umbraco administrators.** After upgrading you will find the Settings tab
> present and explaining that it needs a grant; tick the permission for the people who
> should decide these things, and it appears.
>
> This is deliberate rather than an oversight. Those settings reach the site's retention
> posture, its anonymous delivery-API exposure and the addresses bookers' details are sent
> to. Handing them to every group that happened to hold "Configure resources and services"
> would widen privilege during an upgrade, silently, into exactly what the separate
> permission exists to keep apart.

The backoffice hides screens and buttons a user's permissions do not cover, as a
courtesy; **the server makes the actual decision on every request**, so a bookmarked link
or a handwritten API call is refused regardless of what was visible.

**Access to another section does not grant uBookIt.** A user with full Content access and
no uBookIt grant cannot reach uBookIt's endpoints, and a user granted only uBookIt can use
it without being given Content. That is deliberate: the section is the unit of access, so
the answer to "can this person manage bookings?" is visible in one place rather than
inferred from an unrelated permission.

> **Grant it deliberately.** The section grant decides who can see the bookings a site has
> taken — when they are, what was booked, and whether they were cancelled. Who can see the
> **name and email address** of the person who booked is a *second* question, answered by
> the Sensitive data group below.

For reference, the section's alias is `UBookIt.Section`, which is the value stored against
a user group. You should not need it — the backoffice picker handles this — but it appears
in logs and in the database, and it is not the display name.

## Who can see booker contact details

Booking rows carry the **name and email address** of the person who booked. Those are shown
only to backoffice users in Umbraco's built-in **Sensitive data** group. Everyone else sees
the booking — its reference, when it runs, what it claims, its status — with the contact
details replaced by *"Contact details hidden"*.

To grant it: **Users → *(the user)* → Groups**, and add **Sensitive data**.

This is Umbraco's own group, not something uBookIt adds. It is the same mechanism that
governs document-type properties marked as sensitive, so a site that already uses that
concept is not learning a second one. uBookIt reads it and cannot create, rename or
reconfigure it.

> **Being an administrator does not grant this.** Umbraco's installer puts only the site's
> **original** super user — the account created during installation — into the Sensitive
> data group. Every user created afterwards starts outside it, including one you add to
> Administrators. So a new colleague with the highest role your site offers will open the
> Bookings list and find every contact detail hidden. That is uBookIt working correctly, and
> adding them to Sensitive data is the fix. The list says so on screen for the same reason
> this paragraph exists.

The redaction happens on the server. A user without the group is not sent the values at all,
so they are not present in the page, in the browser's network tools, or anywhere else on the
client — hiding them in the interface would not have been redaction.

Two limits worth stating:

- **It is all-or-nothing.** The group carries no finer grain — there is no "may see names but
  not email addresses", and no per-resource variation. That is Umbraco's design, and uBookIt
  uses it rather than inventing a parallel scheme that could disagree with it.
- **It does not reach your own code.** uBookIt's notifications hand your handlers the whole
  booking, contact details included, because that is how a site sends its own confirmation
  emails — see [notifications](notifications.md). There is no signed-in backoffice user in a
  notification handler, so the question this group answers does not arise there.

## Erasing a booker's details

A person can ask you to remove the personal data you hold about them. uBookIt does this by
**anonymising the booking, not deleting it**: the booking keeps its reference, its time, the
resources it claims and its status, and it goes on blocking that slot. What leaves is the
person — name, email address, phone number, and the member key if there was one.

Deleting the row instead would quietly hand back time you had sold, and destroy your own
record of what happened. That is not what somebody asking to be forgotten has asked for, and
it is not something a site can agree to on their behalf.

`POST /umbraco/ubookitbackoffice/api/v1/bookings/{id}/erase-booker`

**Who can do it.** The same **Sensitive data** group that decides who may *read* contact
details also decides who may erase them, on top of access to the uBookIt section and the
**See bookings** permission. A user your site has decided may not so much as see a booker's
name cannot destroy it, and one who may not see bookings at all reaches neither.

**It cannot be undone.** There is no restore. Once the details are gone from the booking, no
permission, no group and no support call brings them back — which is the whole point, and the
reason the next three limits matter.

**On a site that sends email, erasure does not reach what has already left.** A confirmation or
cancellation message delivered before the erasure is in somebody's mailbox, and if a mail server
rejected the address it may have quoted it into this site's own error log. Neither is a store
uBookIt can reach, so neither is erased — see [reacting to bookings](notifications.md). If you are
honouring a right-to-be-forgotten request, that is the boundary to know about **before** you tell
somebody their details are gone.

**Running it twice is safe.** Erasing an already-erased booking succeeds and changes nothing,
keeping the *first* erasure's timestamp. That is deliberately unlike cancelling, which refuses
a second attempt: a retry after a network timeout must not become a second, differently-dated
erasure.

After erasure, everyone who can see the bookings list — in the Sensitive data group or not —
sees the row marked *"Contact details erased"* rather than *"Contact details hidden"*. The two
say different things and lead to different actions: **hidden** means a colleague in the group
can read them for you, and **erased** means there is nobody to ask.

### Finding the bookings to erase

A request to be forgotten arrives as an email address, so there is a search for exactly that:

`POST /umbraco/ubookitbackoffice/api/v1/bookings/find-by-booker`

It returns every booking still holding that address, with no date window — a subject's request
never says when they booked — and it needs the **same Sensitive data group** as reading or
erasing contact details. Somebody who may not see a booker's name cannot ask questions about one
either.

**It matches the whole address, exactly.** There is no partial, prefix or wildcard search, and
there will not be: *"which of my bookers are at this domain"* is a different question from *"is
this person in my records"*, and only the second one is anybody's business. Matching follows your
database's collation, which for a default SQL Server install means capitalisation does not
matter.

**Erased bookings do not appear.** They hold no address to match. Two consequences worth knowing:
a person's bookings drop out of their own search results as you erase them, and **an empty result
does not prove an erasure worked** — it means either "erased" or "never booked here", and the
search cannot tell you which. If you need to confirm, note the booking references before you
erase.

### Two things it does not do

- **It erases one booking, not a person.** If somebody booked with you three times, that is
  three erasures — search first, then erase each result. uBookIt will not erase them in one
  call: a bulk irreversible action needs a confirmation step designed for it, and that is not
  built.
- **It finds bookings made with *that* address.** Somebody who booked twice under two different
  addresses has one set found. Nothing can fix that, and it is stated so that a search returning
  one booking is not read as proof there is only one.
- **You can erase a booking that has not happened yet, and nothing stops you.** Doing so
  leaves you unable to contact somebody who is going to turn up. Whether the reason you held
  their details still applies is your site's judgement and not the package's, so uBookIt
  permits it and tells you the consequence rather than deciding for you.

> **There is no erase button in the backoffice yet.** The endpoint is available for a site or
> an integration to call. A one-way, irreversible action needs a confirmation step designed
> for it, and shipping the button before that step would be the easiest way to lose somebody's
> booking details to a mis-click.

### Details can also disappear with nobody having erased them

If your site has configured a **retention period**, bookings are erased automatically once they
are old enough — see [Erasing old bookings automatically](#erasing-old-bookings-automatically)
below. A booking marked *"Contact details erased"* with no record of anyone erasing it is
almost certainly that, and not a fault.

## Erasing old bookings automatically

Erasing on request answers somebody who asks. Retention answers the other half: personal data
should not be kept for ever just because nobody got round to removing it.

Set a number of days in `appsettings.json`:

```json
{
  "UBookIt": {
    "RetentionDays": 90
  }
}
```

uBookIt then erases the booker of every booking whose slot **ended** more than that many days
ago, using exactly the same erasure described above — the booking, its reference, its time and
its status all survive; only the person leaves.

> ### Turning this on erases your history immediately
>
> **Not gradually — on the first run, within minutes.** If you set `RetentionDays` to 90 on a
> site holding three years of bookings, almost every booker's name, email address and phone
> number is gone before the hour is out, and **no permission, group or support call brings any
> of them back**.
>
> There is no "are you sure?" step, because there is nowhere to put one: the setting takes
> effect in a config file, not in a screen. This paragraph is the confirmation dialog. Decide
> the number with that in mind, and consider whether you want a database backup first.

**It is off unless you set it.** No `RetentionDays`, no automatic erasure — uBookIt will not
start deleting your data because you upgraded.

**A value it cannot read means off, not a default.** If the setting is blank, is not a number,
is zero or negative, or is larger than 36525 (a century), uBookIt logs an **error** at startup
and erases nothing. It will never guess a period on your behalf: keeping data longer than you
meant is something you can correct, and erasing it sooner than you meant is not. If you want the
shortest possible period, set `1` — `0` is treated as a mistake, because it is far more often
somebody writing "off". A period beyond a century is refused for the same reason: it is either a
typo or an attempt to say "never", and removing the setting already says never.

**To turn retention off, remove the setting — do not blank it.** An empty value counts as one
you meant to write and could not be read, so it is reported as an error rather than passed over
in silence. That is deliberate: an environment variable resolving to nothing on a site that
meant `90` is exactly the case worth interrupting somebody for. A JSON `null` reads as absent.

**The clock runs from the end of the booking**, not from when it was made or when anything
happened to it.

**Every booking counts, whatever its status.** Cancelled, declined and never-confirmed bookings
hold a real person's details just as firmly as ones that went ahead, so retention treats them
alike.

**A booking whose slot has not ended is never erased by retention**, however long ago it was
made and even if it was cancelled months in advance. If you want somebody's details gone before
then, erase that booking by hand — that is what the endpoint above is for.

**The setting is read at startup**, so a change to it takes effect when the site restarts.

**On a load-balanced site it runs once**, not once per server. uBookIt uses Umbraco's own
scheduling to arrange that.

## What visitors are told

The booking form carries a short privacy notice, above the button that submits it. It says
four things, and every one of them is a fact about what uBookIt does rather than a claim
somebody wrote:

- **What is collected** — the booker's name and email address, and their phone number if
  they give one.
- **Why** — to hold and identify the booking, and so the site is able to contact the booker
  about it. **Whether it says a confirmation will be sent depends on your configuration**: it
  says so only on a site where uBookIt will actually send one, and says only that the site is
  able to make contact everywhere else. It is never a promise your site does not keep. See
  [reacting to bookings](notifications.md) for what turns sending on.
- **How long it is kept** — read from `RetentionDays`. If you have set a period, the notice
  states it. If you have not, it says so plainly rather than going quiet.
- **Who can see it** — that contact details are visible in the backoffice only to staff with
  access to personal data.

**You cannot mistype the retention period into it.** There is no wording to keep in step with
your configuration, because the sentence is generated from the same value the retention job
acts on. Change the period, restart, and the notice changes with it.

The same is true of what the notice says about being contacted. If you turn on
`UBookIt:Notifications:SendBookerEmails` and your site can send mail, the notice says a
confirmation will be sent — because one will. If either is missing it says only that the site is
able to contact the booker. It is decided by the same condition the sending itself is, so the
notice cannot promise a message your site does not send. See
[reacting to bookings](notifications.md).

### Linking your own privacy policy

```json
{
  "UBookIt": {
    "PrivacyPolicyUrl": "/privacy"
  }
}
```

An absolute `https://` URL or a site-relative path beginning with a single `/`. When set, the
notice links to it; when not, the notice renders its four statements and no link.

**Anything else is refused and logged as an error**, and the notice then renders without a
link. That is deliberately stricter than uBookIt's other settings: this is the only value that
ends up in a link on a public page, so a `javascript:` URL there would be a script running on
your visitors' click. uBookIt accepts the two forms above and nothing else — not by blocking
the dangerous ones, but by allowing only these.

> **This notice is not a privacy policy.** It describes what one package does with what one
> form collects. It says nothing about who you are as a data controller, your lawful basis,
> your jurisdiction, anything else you do with the data, or how somebody complains — because
> uBookIt cannot know any of that. If you need a privacy policy, you still need one, and the
> setting above is how the booking form points at it.

**A theme can replace it.** If your site uses a uBookIt theme that supplies its own view for
the contact-details step, whether the notice appears is the theme's decision — see the
theming guide. Nothing about it will look broken if a theme leaves it out.

## What is in the section

| | |
|---|---|
| **Resources** | The bookable things themselves: opening hours, exceptions, duration limits, capabilities, and whether each may be booked directly |
| **Services** | What a visitor books by name, and the resource roles each service resolves to |
| **Bookings** | What the site has taken: a window you choose, filtered by status, showing the reference, when, who booked *(if you may see it — below)*, which resources, which service, and status |

**The reference is the first column, because it is the one you scan.** Every booking carries a
short reference — `7QX4-M2NP` — which the person who booked was shown on their confirmation.
When somebody telephones, that is what they are holding, so it is what you match against. It is
assigned when the booking is placed and never changes.

From the Bookings view **you can see bookings, cancel them, and — where a booking awaits
approval — confirm or decline it**, and you can **move a booking to a new date, time or
length**. It does not take a booking on someone's behalf.

### Approving bookings

By default nothing here needs approving: placement confirms on the spot. Set
`UBookIt:AutoConfirm` to `false` and placement produces a **requested** booking instead —
it holds its time exactly as a confirmed one does, so nobody can book over it while you
decide, and the confirm and decline controls appear on its row. Confirm keeps the booking
and its time; decline keeps the record but releases the time immediately, and there is no
undo — a declined booking cannot be re-confirmed, so decline asks you first.

**A requested booking waits for you, indefinitely.** Nothing expires it and nothing chases
you: if it is never acted on it simply holds its slot until its time passes. The message to
your `InternalRecipients` (if configured) flags a booking awaiting approval, and the status
filter here finds them all — checking for pending requests is part of running a site with
approval on.

**Confirming or declining tells the person who booked only if booking emails are
configured** — the same condition as cancelling, below. Declining someone who will not get
an email means telling them is yours to do.

**Cancelling tells nobody unless you have configured it to.** The time is released immediately
and the booking keeps its record. Whether the person who booked hears about it depends on
`UBookIt:Notifications:SendBookerEmails` and on your site being able to send mail — if either is
missing, uBookIt sends nothing and telling them is yours to do. Your site can also react
automatically instead: see [reacting to bookings](notifications.md).

A booking whose booker has been erased has no address left, so **nothing is sent to them** —
but your own recipients are still told, because the booking is real and you are entitled to know
it was cancelled.

The view opens on the current week and you change the window with the two date controls.
It shows the statuses that hold their time — confirmed and requested — so a **cancelled or
declined booking is one tick away rather than missing**.

Ticking statuses shows **only** those, rather than adding them to what is already listed:
tick Cancelled on its own and you get the cancelled bookings, not the confirmed ones with
the cancelled ones added.

### Moving a booking

Every booking that still holds its time — confirmed or requested — has a **Move** control on
its row. It opens a small dialog with the booking's current date, start time and length filled
in; change any of them and press Move.

**A move changes when, and nothing else.** The booking keeps its reference (the person who
booked can go on quoting it), its status (a requested booking that has moved is still
requested), the person who booked, the service it was placed for and the resources it claims.

**The new time has to be one the resources could take.** The same rules as a visitor's
booking apply — opening hours, the booking grid, the length limits (the service's as well as
the resources', for a booking placed for a service), and nothing else already there — with two
exceptions made for you: the resource's **minimum notice does not bind you**
(somebody rang to say they are running late; you are the one the site trusts to decide), and
neither does its **booking horizon**. What still binds everyone is that a booking cannot be
moved into the past.

**If the time cannot be taken, the dialog says why and stays open**, so you change the time
rather than start again. It tells you whether the time was outside opening hours, already
booked, in the past, off the booking grid, or the time the booking already holds.

**There is no picker showing where a booking could go.** You choose a time and are told
whether it can be taken. That is a known limitation of this screen rather than a promise
about the future; if you need to see free time first, the front end's availability is the
place to look.

**A service booking moves with the resources it was given.** If one of them is busy at the new
time, the move is refused — it is not quietly handed a different room or a different person.
Swapping a resource is a different operation, and it is not built.

**Nothing records where a booking used to be.** After a move, the booking shows only where it
is now. Your site can keep its own record by subscribing to the moved notification, which is
the one notification that carries the previous time — see [reacting to bookings](notifications.md).

**Moving tells the person who booked only if booking emails are configured** — the same
condition as cancelling — and the message says both the old time and the new one. Your own
recipients are not told: you, or a colleague, just did it from this screen. If the person who
booked will not get an email, telling them is yours to do.

A moved booking may leave the window you are looking at. The list says where it went, above
the table, so a row that vanishes is one you moved rather than one you lost.

## Responsibility

Each resource and each service can name the people **responsible** for it: backoffice users,
backoffice user groups, or both, picked in a *Responsibility* box on the resource and service
editors. Being responsible means one thing — **you are emailed about its bookings** when the
site sends internal messages (see [notifications](notifications.md#telling-the-people-responsible)
for how the recipients are worked out and how this sits beside the `InternalRecipients` list).

**It is not permissions.** Assigning a user grants them nothing: no section access, no extra
visibility, no ability to act on anything — and it takes nothing away. uBookIt only ever
*reads* a group's membership; put people in ordinary Umbraco user groups for access, and use
responsibility purely to say who cares about what. A responsible user who cannot see the
bookings screen will still receive the messages, because internal messages carry no booker
details and link to a screen that applies its own access control when followed.

**A party that stops existing stays visible.** Delete a user or group and its assignment shows
in the editor marked as no longer resolving, rather than vanishing — so you can see that Studio
2's contact went away and pick a replacement. Until you do, that assignment simply sends
nothing. Saving replaces the whole set with what the pickers hold, so changing a picker's
selection and saving drops that picker's stale entries with the change — which is the natural
moment for them to go. A stale entry in the picker you did not touch stays, still marked. Users the mail path skips — disabled accounts, invitations never accepted — are marked
with their state in the same box.

## The service shown against a booking

A booking records the service it was placed for. Two things about that are worth knowing
before they look like bugs.

**The service name is the one recorded when the booking was placed.** Renaming a service
does not retitle bookings already placed for it, and deleting a service does not remove the
name from bookings that named it — they keep saying what was sold at the time. This is
deliberate: the alternative is that last year's bookings silently change what they say when
somebody edits a service today.

**A booking with no service was booked directly**, against a resource offered on its own.
It is not a booking whose service failed to be recorded. Both kinds are normal, and which
one a resource allows is the *booked directly* setting on the resource itself.

## What the section does not do

- **It does not place bookings.** Recording a booking on someone's behalf — a phone
  booking — is not built. Bookings arrive through the front-end flow.
- **It does not change which resources a booking claims.** A move keeps every resource; a
  busy one refuses the move rather than being swapped. See [moving a booking](#moving-a-booking).
- **It does not keep a history of where a booking has been.** A booking shows where it is now.
- **It does not find a person across bookings.** Erasure works on one booking at a time; see
  above.

These are stated because a management section invites the assumption that it manages
everything. It configures what can be booked, reads what has been, decides a pending request,
moves a booking to a new time, calls a booking off, and erases a booker's details on request.
