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

These are stated because a management section invites the assumption that it manages
everything. It configures what can be booked, reads what has been, and calls one off.
