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

> **Grant it deliberately.** Booking data includes the **name and email address of every
> person who has booked**. Anyone with this section can read that. It is ordinary personal
> data and the section is the control over it, so treat the grant as you would access to
> Members.

For reference, the section's alias is `UBookIt.Section`, which is the value stored against
a user group. You should not need it — the backoffice picker handles this — but it appears
in logs and in the database, and it is not the display name.

## What is in the section

| | |
|---|---|
| **Resources** | The bookable things themselves: opening hours, exceptions, duration limits, capabilities, and whether each may be booked directly |
| **Services** | What a visitor books by name, and the resource roles each service resolves to |
| **Bookings** | What the site has taken: a window you choose, filtered by status, showing when, who, which resources, which service, and status |

The Bookings view is **read-only**. Cancelling a booking from the backoffice is the next
piece of work; nothing about the section or its permissions changes when it arrives.

The view opens on the current week and you change the window with the two date controls.
It shows the statuses that hold their time — confirmed and requested — so a **cancelled
booking is one toggle away rather than missing**.

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
everything. It currently configures what can be booked, and reads what has been.
