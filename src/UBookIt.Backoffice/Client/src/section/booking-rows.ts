/**
 * Everything the bookings list decides before it renders anything.
 *
 * These are pure functions rather than element methods for one reason: the
 * decisions worth guarding here are all invisible on screen when they are
 * wrong. A window sent as an instant still returns bookings. A status parameter
 * sent as an empty array still returns a page. A time formatted in the wrong
 * zone is still a time. Each produces a plausible list, and a test that drives
 * the element and reads the rendered table cannot tell any of them from the
 * correct behaviour.
 *
 * So the seam is here, where a request's *shape* and a cell's *derivation* can
 * be asserted directly.
 */

/** The parts of a listed booking these decisions depend on. */
export type BookingLike = {
  startUtc: string;
  endUtc: string;
  timeZoneId: string;
  service?: { serviceId: string; displayName: string } | null;
};

/** A window as the endpoint takes it: two site-local dates, never instants. */
export type Window = {
  from: string;
  to: string;
};

/**
 * A `Date` as `YYYY-MM-DD` **in that date's own local calendar**.
 *
 * Deliberately not `toISOString().slice(0, 10)`, which converts to UTC first
 * and so reports the previous day for anyone east of Greenwich for part of
 * every day — an off-by-one that only appears for some users at some hours, and
 * looks like the list being wrong rather than the date being wrong.
 */
export function toDateValue(date: Date): string {
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  const day = `${date.getDate()}`.padStart(2, "0");

  return `${date.getFullYear()}-${month}-${day}`;
}

/**
 * Monday to Sunday of the week containing <paramref name="today" />.
 *
 * The browser's calendar, not the site's: the screen needs *a* sensible opening
 * week and the only calendar it has is the user's. The two can differ by a day
 * for an operator working from another country at the turn of a week, and the
 * consequence is a default window one day out — visible in the two date
 * controls, and changeable. Asking the server what "this week" means would be a
 * new endpoint for a default value.
 */
export function currentWeek(today: Date): Window {
  const monday = new Date(today.getFullYear(), today.getMonth(), today.getDate());

  // getDay() is 0 for Sunday, so Sunday belongs to the week that started six
  // days ago rather than to the one starting tomorrow.
  const offset = (monday.getDay() + 6) % 7;
  monday.setDate(monday.getDate() - offset);

  const sunday = new Date(monday);
  sunday.setDate(monday.getDate() + 6);

  return { from: toDateValue(monday), to: toDateValue(sunday) };
}

/**
 * The `statuses` query value for a set of selected names — or `undefined`.
 *
 * Omitting the parameter yields the statuses that block time, which is what
 * "the operator has chosen nothing" means.
 *
 * **An earlier version of this comment claimed an empty array would return
 * nothing. It does not** — the endpoint treats an empty set exactly as an
 * absent one, so that request is harmless. The reason to omit is that the port
 * settles this default and a view that restates it creates a second one.
 *
 * The request that genuinely differs is naming **all four** statuses to mean
 * "the default": that includes cancelled and declined, which the default
 * excludes, so it answers a question the operator did not ask.
 */
export function statusesParam(selected: readonly string[]): string[] | undefined {
  return selected.length === 0 ? undefined : [...selected];
}

/**
 * The exact query the list sends.
 *
 * A separate function because **the shape of this object is a guarantee**, and
 * it is one that fails invisibly. A window converted to instants still returns
 * bookings; a `statuses: []` still returns a page; both produce a list that
 * looks entirely reasonable and is answering a different question. Nothing a
 * reader can see distinguishes them, so the assertion has to be made against
 * the request itself.
 *
 * `from` and `to` leave here as the date strings the controls hold. **No
 * instant, offset or zone is computed** — the endpoint resolves them against
 * the site's zone, which is where that rule was deliberately put so it runs
 * once rather than once per client.
 */
export function listQuery(
  window: Window,
  statuses: readonly string[],
  skip: number,
  take: number,
): { from: string; to: string; statuses?: string[]; skip: number; take: number } {
  return {
    from: window.from,
    to: window.to,
    statuses: statusesParam(statuses),
    skip,
    take,
  };
}

/**
 * Whether a window is complete enough to ask about.
 *
 * A date input reports an empty value while it is being edited — clear one
 * segment and the whole control reads `""`. Asking with that produces a 400
 * from model binding, so an operator halfway through changing a date gets an
 * alert interrupting them, the table removed, and a framework-worded message
 * about a parameter they have never heard of.
 *
 * The view can see this before it asks, so it does. **This is the cause behind
 * the stale-error race** — the race guard stops a failure outliving a later
 * success, and this stops the failure happening at all. Fixing only the former
 * leaves the interruption in place for anyone who pauses mid-edit.
 */
export function shouldLoad(window: Window): boolean {
  return window.from !== "" && window.to !== "";
}

/**
 * Where paging should land when the page just emptied under the operator.
 *
 * Cancelling removes a row from the current result set — on the default filter
 * the booking leaves the view entirely — and cancelling the only row on page two
 * leaves `skip` pointing past the end. The table then renders empty with
 * "showing 21–20 of 20", and the empty message is suppressed because the total
 * is not zero: a nonsense state reachable by an ordinary action.
 *
 * **Steps back a page rather than resetting to the first.** A window or filter
 * change resets, because the operator asked a different question; cancelling is
 * the same question with one fewer answer, and an operator working through page
 * three does not want to be thrown to page one on every cancellation.
 *
 * Returns the same `skip` when nothing needs to move, so a caller can compare and
 * avoid a second request in the ordinary case.
 */
export function skipAfterEmptyPage(skip: number, itemCount: number, pageSize: number): number {
  if (itemCount > 0 || skip <= 0) {
    return skip;
  }

  return Math.max(0, skip - pageSize);
}

/**
 * Whether the view offers to cancel this booking.
 *
 * Only where the domain would allow it — the status machine permits cancellation
 * from `Requested` and `Confirmed` and from nothing else. Offering a control that
 * is always refused teaches an operator to ignore failures, which is a worse
 * habit than a missing button is an inconvenience.
 *
 * **This is a convenience, not the rule.** The endpoint refuses an invalid
 * transition independently, so a screen showing a stale list cannot talk the
 * domain into one. If this function and the domain ever disagree, the domain
 * wins and the operator is told.
 *
 * Compared by NAME, because that is what crosses the wire — the same reason the
 * status filter sends published names rather than labels.
 */
export function canCancel(status: string): boolean {
  return status === "Requested" || status === "Confirmed";
}

/**
 * Where paging should be after a query changes.
 *
 * Always the first page when the *query* changed — a page number counts into
 * one result set and means nothing in another. An operator on page 3 who
 * narrows the window would otherwise get page 3 of a different question:
 * plausibly empty, under a "Showing 41–60 of 12" label that cannot be true.
 *
 * Out here rather than inline in two event handlers because it is the same
 * class of failure as the rest of this module — nothing on screen says the page
 * number is stale, and a reader looking at an empty page has no way to tell it
 * from a window with no bookings in it.
 */
export function skipAfter(change: "query" | "page", currentSkip: number): number {
  return change === "query" ? 0 : currentSkip;
}

/**
 * Whether to tell the reader that nothing matched.
 *
 * Only when the view actually knows. After a failed request it does not: the
 * alert says what happened, and adding "No bookings in this window" beside it
 * answers a question nothing asked, with the answer most likely to be wrong.
 *
 * The two states render almost identically and mean opposite things — "nothing
 * is booked" versus "we could not find out" — which is exactly why this is a
 * named decision rather than a condition buried in a template.
 */
export function showsEmptyMessage(total: number, failed: boolean): boolean {
  return total === 0 && !failed;
}

/**
 * Whether the zone must be shown beside each time.
 *
 * Only when this page holds more than one — that is the case where a reader can
 * misread two rows against each other as though they shared a clock. On a
 * single-zone page, which is every ordinary site, repeating one identical label
 * on every row is noise.
 *
 * **What this cannot see:** a page where every booking shares a zone that is no
 * longer the site's. The endpoint reports each booking's zone and nothing
 * page-level, and adding a field the read port cannot supply is forbidden by
 * that capability — so the comparison available here is bookings against each
 * other, not bookings against the site. Reachable only by changing a site's
 * zone after it has taken bookings, and stated in the spec rather than implied.
 */
export function zoneLabelNeeded(bookings: readonly BookingLike[]): boolean {
  // Resolved zones, not raw ids. Deduping on the id let a page where every
  // booking carried an id the runtime rejects render UTC times with no label at
  // all — one zone by the id's reckoning, and silently not the zone the times
  // were actually formatted in. `formatInterval` promises it "says so" when it
  // falls back; this is the half that keeps that promise.
  return new Set(bookings.map((booking) => resolveZone(booking.timeZoneId))).size > 1;
}

/**
 * Whether any booking on the page is being shown in a zone that is not the one
 * it records — i.e. the fallback fired and the reader must be told.
 */
export function zoneFallbackOccurred(bookings: readonly BookingLike[]): boolean {
  return bookings.some((booking) => resolveZone(booking.timeZoneId) !== booking.timeZoneId);
}

/**
 * A booking's interval in **its own** recorded zone.
 *
 * A booking is a record of a local time that somebody agreed to. Restating it
 * in the reader's zone, or in the site's current one, reports a time that was
 * never agreed — the same class of error as showing a renamed service's new
 * name against an old booking.
 *
 * An unknown zone id is not a reason to show nothing, and not a reason to
 * silently fall back to the reader's clock: it renders in UTC and says so, so
 * the row stays readable and no time is quietly misattributed.
 */
export function formatInterval(
  booking: BookingLike,
  locale?: string,
  formatter: (options: Intl.DateTimeFormatOptions) => Intl.DateTimeFormat = (options) =>
    new Intl.DateTimeFormat(locale, options),
): { text: string; zone: string } {
  const zone = resolveZone(booking.timeZoneId);

  const day = formatter({ timeZone: zone, dateStyle: "medium" });
  const clock = formatter({ timeZone: zone, timeStyle: "short" });

  const startDay = day.format(new Date(booking.startUtc));
  const endDay = day.format(new Date(booking.endUtc));
  const start = clock.format(new Date(booking.startUtc));
  const end = clock.format(new Date(booking.endUtc));

  // A booking that runs past midnight ends on a different day, and naming only
  // the start date reports an end that appears to precede its own start —
  // "2 Sept, 23:00–02:00". Overnight bookings are ordinary for a booking
  // system, so the second date is shown exactly when it differs.
  const text =
    startDay === endDay
      ? `${startDay}, ${start}–${end}`
      : `${startDay}, ${start} – ${endDay}, ${end}`;

  return { text, zone };
}

/** The booking's zone if the runtime knows it, UTC if it does not. */
function resolveZone(timeZoneId: string): string {
  try {
    new Intl.DateTimeFormat("en", { timeZone: timeZoneId });
    return timeZoneId;
  } catch {
    return "UTC";
  }
}

/**
 * What the service column says.
 *
 * Three states, and the difference between the first two is the point of the
 * column. `null` means the booking was placed **directly** against a resource
 * offered on its own — a fact, and it is stated in words. A blank cell would
 * read as missing data, which is the one thing it is not.
 *
 * The third state is defensive: nothing produces a recorded service with no
 * name, but the store's mapper keeps an id rather than discarding an
 * attribution, so the column keeps the id rather than rendering an empty
 * string. A row that still says *which* service it was is recoverable; one that
 * says nothing is not.
 */
export function serviceLabel(
  booking: BookingLike,
  directly: string,
): string {
  if (!booking.service) {
    return directly;
  }

  return booking.service.displayName.trim() === ""
    ? booking.service.serviceId
    : booking.service.displayName;
}
