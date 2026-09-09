# The uBookIt backoffice section

Installing uBookIt adds a **uBookIt** section to the Umbraco backoffice, alongside
Content, Media and the rest. It is where a site's bookable resources and services are
configured, and where the bookings it has taken are read.

## Who can use it

Access is granted the same way as any other section: **Users → User Groups → *(a group)* →
Sections**, and tick **uBookIt Section**.

That one grant governs both halves. It decides whether the section appears in the
backoffice *and* whether that user may call uBookIt's management API — the endpoints the
section's own screens are built on. There is nothing else to configure and no second
permission to keep in step.

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
details also decides who may erase them, on top of access to the uBookIt section. A user your
site has decided may not so much as see a booker's name cannot destroy it.

**It cannot be undone.** There is no restore. Once the details are gone, no permission, no
group and no support call brings them back — which is the whole point, and the reason the
next two limits matter.

**Running it twice is safe.** Erasing an already-erased booking succeeds and changes nothing,
keeping the *first* erasure's timestamp. That is deliberately unlike cancelling, which refuses
a second attempt: a retry after a network timeout must not become a second, differently-dated
erasure.

After erasure, everyone with access to the section — in the Sensitive data group or not —
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

From the Bookings view **you can see bookings and cancel them**. Those are the two things v1
does: it does not approve, decline, amend or take a booking on someone's behalf.

**Cancelling tells nobody.** The time is released immediately and the booking keeps its
record, but uBookIt sends no email or message to the person who booked — so if they should
know, that is yours to do. Your site can react automatically: see
[reacting to bookings](notifications.md).

The view opens on the current week and you change the window with the two date controls.
It shows the statuses that hold their time — confirmed and requested — so a **cancelled
booking is one tick away rather than missing**.

Ticking statuses shows **only** those, rather than adding them to what is already listed:
tick Cancelled on its own and you get the cancelled bookings, not the confirmed ones with
the cancelled ones added.

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
- **It does not approve or decline.** Placement confirms immediately. The statuses for an
  approval workflow exist in the data model so it can be added without a breaking change,
  but no pathway produces them today.
- **It does not amend a booking's time.** There is no reschedule; the shape of that
  operation is a cancellation and a new booking.
- **It does not find a person across bookings.** Erasure works on one booking at a time; see
  above.

These are stated because a management section invites the assumption that it manages
everything. It configures what can be booked, reads what has been, calls one off, and erases a
booker's details on request.
