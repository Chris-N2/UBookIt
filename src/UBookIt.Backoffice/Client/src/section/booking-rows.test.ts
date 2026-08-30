import { describe, expect, it } from "vitest";
import {
  currentWeek,
  formatInterval,
  listQuery,
  serviceLabel,
  statusesParam,
  toDateValue,
  zoneLabelNeeded,
} from "./booking-rows.js";

/**
 * The bookings list's decisions, asserted where they are visible.
 *
 * Every one of these fails *silently* on screen when it is wrong: a window sent
 * as an instant still returns bookings, an empty `statuses` array still returns
 * a page, a time formatted in the reader's zone is still a time, and a blank
 * service cell looks like data that has not loaded. A test that drove the
 * element and read the table would pass on all four defects.
 */

const booking = (overrides: Partial<Parameters<typeof serviceLabel>[0]> = {}) => ({
  startUtc: "2026-09-02T08:00:00+00:00",
  endUtc: "2026-09-02T09:00:00+00:00",
  timeZoneId: "Europe/London",
  service: null,
  ...overrides,
});

describe("the opening window", () => {
  it("runs Monday to Sunday of the week containing today", () => {
    // Wednesday 2 September 2026.
    expect(currentWeek(new Date(2026, 8, 2))).toEqual({ from: "2026-08-31", to: "2026-09-06" });
  });

  it("treats Sunday as the END of its week, not the start of the next", () => {
    // The off-by-one `getDay()` invites: Sunday is 0, so a naive subtraction
    // moves the window forward a week and an operator opening the section on a
    // Sunday sees next week instead of the one they are standing in.
    expect(currentWeek(new Date(2026, 8, 6))).toEqual({ from: "2026-08-31", to: "2026-09-06" });
  });

  it("runs Monday to Sunday when today IS Monday", () => {
    expect(currentWeek(new Date(2026, 7, 31))).toEqual({ from: "2026-08-31", to: "2026-09-06" });
  });

  it("crosses a month and a year boundary", () => {
    // Thursday 31 December 2026 — the week runs into 2027.
    expect(currentWeek(new Date(2026, 11, 31))).toEqual({ from: "2026-12-28", to: "2027-01-03" });
  });

  it("names the date in its own calendar, not in UTC", () => {
    // `toISOString().slice(0, 10)` converts to UTC first, so it reports the
    // wrong day for part of every day — for some users only, which reads as the
    // list being wrong rather than the date being wrong.
    //
    // FOUR fixtures, deliberately. Just after local midnight is wrong only for
    // a runner EAST of UTC; just before local midnight is wrong only for one
    // WEST of it; and an offset can differ by season, so each is taken in
    // January and July. Between them, every non-UTC runner sees at least one
    // failure if this converts.
    //
    // A runner ON UTC sees none — and cannot, because there the two
    // implementations are genuinely identical. That residue is stated rather
    // than hidden: the first version of this test used a single January
    // midnight fixture, which is unfalsifiable in London for half the year, and
    // the mutation was caught only incidentally by the week tests.
    expect(toDateValue(new Date(2027, 0, 1, 0, 30))).toBe("2027-01-01");
    expect(toDateValue(new Date(2027, 0, 1, 23, 30))).toBe("2027-01-01");
    expect(toDateValue(new Date(2027, 6, 1, 0, 30))).toBe("2027-07-01");
    expect(toDateValue(new Date(2027, 6, 1, 23, 30))).toBe("2027-07-01");
  });
});

describe("the status parameter", () => {
  it("is omitted entirely when nothing is selected", () => {
    // NOT an empty array. Omitting yields the endpoint's default — the statuses
    // that block time — while sending `[]` is the view stating a filter of its
    // own, and would restate a default the port already settles.
    expect(statusesParam([])).toBeUndefined();
  });

  it("carries exactly what was selected", () => {
    expect(statusesParam(["Cancelled"])).toEqual(["Cancelled"]);
    expect(statusesParam(["Confirmed", "Cancelled"])).toEqual(["Confirmed", "Cancelled"]);
  });

  it("does not alias 'all four selected' to 'none selected'", () => {
    // A tempting simplification, and a different request: the default is the
    // BLOCKING statuses, so asking for all four is asking for more than the
    // default, and collapsing them would silently drop cancelled bookings from
    // a page the operator explicitly asked to include them in.
    const all = ["Requested", "Confirmed", "Cancelled", "Declined"];

    expect(statusesParam(all)).toEqual(all);
  });
});

describe("the query the list sends", () => {
  const window = { from: "2026-08-31", to: "2026-09-06" };

  it("carries the window as DATES, with nothing resolved to an instant", () => {
    // The guarantee the whole server-side-conversion design rests on. A client
    // that "helpfully" converted would send a window shifted by its own zone
    // offset and still get a plausible list back — bookings, just not the right
    // ones, and off by a day only for some users at some times of year.
    const query = listQuery(window, [], 0, 20);

    expect(query.from).toBe("2026-08-31");
    expect(query.to).toBe("2026-09-06");

    // Not merely equal to the right date — of the right SHAPE. `Date`,
    // `toISOString()` and an epoch number would each fail this while being
    // exactly the kind of value a well-meaning refactor introduces.
    expect(query.from).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(query.to).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(JSON.stringify(query)).not.toContain("T00:00");
    expect(JSON.stringify(query)).not.toContain("Z");
  });

  it("omits statuses entirely when none is selected", () => {
    // The property must be ABSENT, not present-and-empty: the endpoint's
    // default applies only when the parameter is not sent.
    const query = listQuery(window, [], 0, 20);

    expect(query.statuses).toBeUndefined();
    expect(Object.values(query)).not.toContainEqual([]);
  });

  it("carries the selected statuses when there are some", () => {
    expect(listQuery(window, ["Cancelled"], 0, 20).statuses).toEqual(["Cancelled"]);
  });

  it("passes paging straight through", () => {
    const query = listQuery(window, [], 40, 20);

    expect(query.skip).toBe(40);
    expect(query.take).toBe(20);
  });
});

describe("when the zone is shown", () => {
  it("is not shown when every booking shares one zone", () => {
    expect(zoneLabelNeeded([booking(), booking()])).toBe(false);
  });

  it("is shown as soon as a page holds two", () => {
    expect(zoneLabelNeeded([booking(), booking({ timeZoneId: "America/New_York" })])).toBe(true);
  });

  it("is not shown for an empty page", () => {
    expect(zoneLabelNeeded([])).toBe(false);
  });
});

describe("the time a booking shows", () => {
  it("is expressed in the booking's own zone, not the reader's", () => {
    // 08:00Z on 2 September is 09:00 in London and 04:00 in New York. The same
    // instant rendered twice, and the only thing deciding which is right is the
    // booking's own recorded zone.
    const london = formatInterval(booking(), "en-GB");
    const newYork = formatInterval(booking({ timeZoneId: "America/New_York" }), "en-GB");

    expect(london.text).toContain("09:00");
    expect(newYork.text).toContain("04:00");
    expect(london.zone).toBe("Europe/London");
    expect(newYork.zone).toBe("America/New_York");
  });

  it("carries both ends of the interval", () => {
    expect(formatInterval(booking(), "en-GB").text).toContain("10:00");
  });

  it("falls back to UTC, and says so, for a zone the runtime does not know", () => {
    // Not the reader's clock: a silent fallback to local time misattributes the
    // booking to a zone nobody chose, and looks entirely normal.
    const unknown = formatInterval(booking({ timeZoneId: "Mars/Olympus_Mons" }), "en-GB");

    expect(unknown.zone).toBe("UTC");
    expect(unknown.text).toContain("08:00");
  });
});

describe("what the service column says", () => {
  it("says a booking with no service was booked directly", () => {
    // In words. A blank cell reads as data that failed to load, and this is the
    // opposite: it is a recorded fact about how the booking was placed.
    expect(serviceLabel(booking({ service: null }), "Booked directly")).toBe("Booked directly");
  });

  it("names the service a booking was placed for", () => {
    expect(
      serviceLabel(
        booking({ service: { serviceId: "s1", displayName: "Initial Consultation" } }),
        "Booked directly",
      ),
    ).toBe("Initial Consultation");
  });

  it("keeps the id rather than showing nothing when a recorded name is empty", () => {
    // Nothing produces this, but the store keeps an id rather than discarding
    // an attribution — so the column keeps it too. A row that still says which
    // service it was is recoverable; a blank one is not, and would be
    // indistinguishable from "booked directly" if it fell through to that.
    expect(
      serviceLabel(booking({ service: { serviceId: "s1", displayName: "  " } }), "Booked directly"),
    ).toBe("s1");
  });
});
