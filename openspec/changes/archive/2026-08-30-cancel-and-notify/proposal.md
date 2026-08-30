## Why

Steps 7 and 8 of `docs/mvp.md`, and they are one change rather than two.

**Cancel** is the second of the two honest v1 management verbs. `IBookingService.CancelAsync`
has existed since the core domain change and nothing can reach it: there is no endpoint and
no control. An operator who needs to release a slot has to edit the database.

**Notify** is here because cancel needs it. The package tells nobody anything — it *consumes*
Umbraco notifications for migrations and theming and **publishes none**, so a site cannot
react to a booking even if it wants to. Ship cancel alone and an operator cancels a booking,
the row updates, and the customer **turns up anyway**. A cancel button with no way for the
site to tell anyone is worse than no cancel button.

v1 publishes the **notification**, never the message: no email, no templates, no SMTP. The
site owns its own mail, as it owns its own markup.

## What Changes

- **A Core observation port.** Placement and cancellation report what happened, in Core's own
  vocabulary. `UBookIt.Core` has **zero package references** today — no Umbraco, nothing —
  and that stays true: the port is Core's, and the Umbraco adapter lives outside it.
- **An Umbraco adapter** translating those to `IEventAggregator` notifications, so a site
  subscribes with `INotificationAsyncHandler<T>` exactly as it would for any Umbraco event.
- **`POST ubookitbackoffice/api/v1/bookings/{id}/cancel`**, under the section-access policy
  every management endpoint uses, returning the booking as it now stands.
- **A cancel action on the bookings row**, for bookings that can still be cancelled, behind
  the accessible in-page confirmation the resource list already uses — never a native
  `confirm()`.
- **Documentation** stating plainly that the package notifies nobody by itself, and what a
  site must do to send anything.

## Non-goals

- **Any email, template or mail configuration.** Stated twice because it is the thing most
  likely to creep in: a package that owns a channel it cannot test on a customer's
  infrastructure owns a support burden instead of a feature.
- **Letting a visitor cancel their own booking.** MVP says *the owner* cancels. A
  visitor-facing cancellation needs a way to prove the booking is theirs, which is an
  authentication design rather than a button.
- **Approve, decline, amend, or booking on someone's behalf.** Each a domain change, each
  already named out of scope in `docs/mvp.md`.
- **Events for anything other than placed and cancelled.** Resource and service edits are
  configuration; nothing in v1 needs to react to them, and an event nobody consumes is a
  contract to maintain for free.
- **Retrying, queueing or persisting a failed notification.** A subscriber that throws must
  not break a booking (see design D3), and beyond that its reliability is its own business.

## Capabilities

### Modified Capabilities

- `bookings`: **two requirements.** *Availability and placement service ports* currently says
  the availability and booking services "SHALL depend only on the two store ports
  (`IResourceStore`, `IBookingStore`) so implementations can be swapped without changing
  Core" — a third port makes that sentence false, and the guarantee it protects
  (swappability, and Core owning its own dependencies) is carried forward and restated rather
  than weakened. A new requirement covers what is observed, when, and that an observer cannot
  break the operation it is observing.
- `booking-management`: gains the cancel endpoint and the row action — the second and last v1
  verb, so the capability's Purpose stops saying *cancel* is outstanding.
- `packaging`: gains a requirement that the notifications a site can subscribe to are
  documented, since a hook nobody knows about is not an extension point.

## Impact

- **`UBookIt.Core`**: the observation port and its two payloads, `BookingService` reporting
  through it. **Still zero package references** — the port is Core's own type, and this is
  checked rather than assumed.
- **`UBookIt.Persistence`**: the Umbraco adapter and its registration, alongside the existing
  service registrations. It is where the composer already lives and the only project of the
  three that both references Umbraco and is loaded by every consumer.
- **`UBookIt.Backoffice`**: the cancel endpoint, its failure mapping, the row action and its
  confirmation, and the regenerated TypeScript client.
- **Public API surface** (additive): the Core port, the two payload types, the two Umbraco
  notification types, and the endpoint. **The notification types are the part that is a
  promise** — a site's handler is compiled against them, so their shape is a compatibility
  commitment from the moment anyone subscribes.
- **No persistence change, no schema change, no migration.** Cancellation already writes only
  a status, and events are not stored.

## Risks

**A subscriber can throw, and the booking is already committed when it does.** This is the
one way this change can damage something that previously worked: if an observer's exception
propagates, a placement that *succeeded and is stored* reports failure to the visitor, who
books again. The booking must be unaffected by anything a subscriber does — design D3 — and
that is a guarantee with a test rather than a note.

**A published event is a contract from the first subscriber.** The payload shape cannot then
change without breaking someone's handler. It carries the booking and nothing derived, so
there is one source of truth and nothing to keep in step.
