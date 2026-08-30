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
 * **`undefined` and `[]` are different requests, and only one of them is the
 * endpoint's default.** Omitting the parameter yields the statuses that block
 * time; sending an empty array is a caller stating a filter, and sending all
 * four names is a third request again. The port settles this default and the
 * view must not restate it, so "the operator has chosen nothing" has to mean
 * "do not send the parameter".
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
  return new Set(bookings.map((booking) => booking.timeZoneId)).size > 1;
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
  const zone = supportedZone(booking.timeZoneId);

  const date = formatter({ timeZone: zone, dateStyle: "medium" }).format(new Date(booking.startUtc));
  const start = formatter({ timeZone: zone, timeStyle: "short" }).format(new Date(booking.startUtc));
  const end = formatter({ timeZone: zone, timeStyle: "short" }).format(new Date(booking.endUtc));

  return { text: `${date}, ${start}–${end}`, zone };
}

/** The booking's zone if the runtime knows it, UTC if it does not. */
function supportedZone(timeZoneId: string): string {
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
