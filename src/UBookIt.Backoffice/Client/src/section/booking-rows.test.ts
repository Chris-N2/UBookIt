import { describe, expect, it } from "vitest";
import {
  bookingReference,
  canCancel,
  currentWeek,
  formatInterval,
  listQuery,
  serviceLabel,
  shouldLoad,
  showsEmptyMessage,
  skipAfter,
  skipAfterEmptyPage,
  statusesParam,
  toDateValue,
  zoneFallbackOccurred,
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
  reference: "7QX4M2NP",
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
    // Omitting yields the endpoint's default: the statuses that block time.
    //
    // Not because `[]` would return nothing — QA checked the server and it
    // treats an empty set exactly as an absent one — but because the port
    // settles this default and a view that states one of its own creates a
    // second. The genuinely different request is the one below.
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

  it("passes the window through untouched, whatever it holds", () => {
    // The shape assertions above are NOT enough, and QA proved it: a mutant
    // that round-trips each date through `new Date(...T00:00:00Z)` and back
    // passes every one of them, because on a UTC runner that round-trip is the
    // identity — while on a runner west of UTC it silently shifts both ends
    // back a day.
    //
    // A value that is not a date at all cannot survive any conversion, so this
    // is the assertion that holds on every runner: the mutant yields
    // "NaN-NaN-NaN". The window is data the view carries, not data it
    // interprets, and this says exactly that.
    const query = listQuery({ from: "not-a-date", to: "also-not-a-date" }, [], 0, 20);

    expect(query.from).toBe("not-a-date");
    expect(query.to).toBe("also-not-a-date");
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

  it("compares RESOLVED zones, so two unresolvable ids are one zone", () => {
    // Both render in UTC, so on screen they ARE one zone, and this function's
    // question is "can two rows be misread against each other" — which they
    // cannot when they show the same clock.
    //
    // This is the only case where resolved and raw dedupe disagree, and it is
    // asserted here precisely because the element's behaviour does NOT
    // distinguish them: `zoneFallbackOccurred` labels this page either way. A
    // mutation back to raw ids is invisible at the element and visible here,
    // which is what the function's own contract is for.
    expect(
      zoneLabelNeeded([
        booking({ timeZoneId: "Mars/Olympus_Mons" }),
        booking({ timeZoneId: "Jupiter/Io" }),
      ]),
    ).toBe(false);
  });

  it("still separates an unresolvable zone from a real one", () => {
    expect(
      zoneLabelNeeded([
        booking({ timeZoneId: "Mars/Olympus_Mons" }),
        booking({ timeZoneId: "Europe/London" }),
      ]),
    ).toBe(true);
  });

  it("reports the fallback so a single-zone unresolvable page still says UTC", () => {
    // The case zoneLabelNeeded alone still cannot see: every row unresolvable,
    // so every row is UTC and they genuinely all agree. formatInterval promises
    // it "says so" when it falls back; this is what keeps that promise.
    expect(zoneFallbackOccurred([booking({ timeZoneId: "Mars/Olympus_Mons" })])).toBe(true);
    expect(zoneFallbackOccurred([booking(), booking()])).toBe(false);
  });
});

describe("where paging lands when a page empties under the operator", () => {
  it("steps back a page when the current one is now empty", () => {
    // Cancelling the only row on page two leaves skip pointing past the end, and the
    // table then renders empty under "showing 21–20 of 20" — with the empty message
    // suppressed, because the total is not zero. A nonsense state from an ordinary action.
    expect(skipAfterEmptyPage(20, 0, 20)).toBe(0);
    expect(skipAfterEmptyPage(60, 0, 20)).toBe(40);
  });

  it("stays put while the page still has rows", () => {
    // The common case, and the one that must not cost a second request.
    expect(skipAfterEmptyPage(20, 5, 20)).toBe(20);
  });

  it("does not step below the first page", () => {
    expect(skipAfterEmptyPage(0, 0, 20)).toBe(0);
  });

  it("steps back rather than resetting to the first page", () => {
    // A window or status change resets, because the operator asked a different question.
    // Cancelling is the same question with one fewer answer, and throwing someone working
    // through page four back to page one on every cancellation is its own annoyance.
    expect(skipAfterEmptyPage(60, 0, 20)).not.toBe(0);
  });
});

describe("what the cancel confirmation says", () => {
  it("tells the operator that the person who booked will not be told", async () => {
    // The sentence the whole notify half of this change exists to make true, said at the
    // moment of deciding rather than in documentation nobody is reading just then. An
    // operator who assumes uBookIt emails the customer finds out when somebody arrives for
    // a booking that no longer exists.
    //
    // Pinned here because it is the one view scenario that needs no DOM — it is a string.
    // The equivalent sentence in docs/backoffice.md is pinned on the .NET side; this is the
    // one an operator actually reads, and it was the unpinned half.
    const { default: terms } = await import("../localization/en-us.js");
    const bookings = (terms as Record<string, Record<string, string>>).ubookitBookings;

    expect(bookings.confirmCancelContent).toContain("does not tell the person who booked");

    // And that the consequence is named rather than left to be worked out.
    expect(bookings.confirmCancelContent).toContain("contact them");
  });

  it("has a string for every key the cancel flow can emit", async () => {
    // A missing key renders as the raw key or as nothing — and for a confirmation dialog,
    // "nothing" is a destructive action with no explanation attached to it.
    const { default: terms } = await import("../localization/en-us.js");
    const bookings = (terms as Record<string, Record<string, string>>).ubookitBookings;

    for (const key of [
      "cancel",
      "confirmCancelHeadline",
      "confirmCancelContent",
      "confirmCancel",
      "confirmFailed",
      "cancelFailed",
      "actions",
    ]) {
      expect(typeof bookings[key]).toBe("string");
      expect(bookings[key].length).toBeGreaterThan(0);
    }
  });
});

describe("which bookings offer cancellation", () => {
  it("offers it for the statuses the domain can cancel from", () => {
    expect(canCancel("Requested")).toBe(true);
    expect(canCancel("Confirmed")).toBe(true);
  });

  it("does not offer it where the domain would refuse", () => {
    // A control that is always refused teaches an operator to ignore failures,
    // which is a worse habit than a missing button is an inconvenience.
    expect(canCancel("Cancelled")).toBe(false);
    expect(canCancel("Declined")).toBe(false);
  });

  it("does not offer it for a status it does not recognise", () => {
    // Closed rather than open: a status added to the domain later should arrive
    // here as "no button" and a deliberate decision, not as a button that
    // happens to work because the check was a denylist.
    expect(canCancel("Rescheduled")).toBe(false);
    expect(canCancel("")).toBe(false);
  });
});

describe("where paging lands after a change", () => {
  it("returns to the first page when the query changes", () => {
    // Page 3 counts into one result set and means nothing in another. Without
    // this an operator who narrows the window gets page 3 of a different
    // question — plausibly empty, under a "Showing 41–60 of 12" label that
    // cannot be true, and nothing on screen says the page number is stale.
    expect(skipAfter("query", 40)).toBe(0);
  });

  it("stays put when only the page changes", () => {
    // The other direction matters: a reset here would pin the list to page one
    // and make Next do nothing, which is the kind of defect that looks like the
    // button being broken.
    expect(skipAfter("page", 40)).toBe(40);
  });
});

describe("whether to ask at all", () => {
  it("does not ask while a date is half-edited", () => {
    // A date input reads as empty while a segment is being changed, and asking
    // with it earns a 400 the operator did not cause and cannot act on — an
    // alert interrupting them mid-edit, worded by the framework.
    expect(shouldLoad({ from: "", to: "2026-09-06" })).toBe(false);
    expect(shouldLoad({ from: "2026-08-31", to: "" })).toBe(false);
    expect(shouldLoad({ from: "", to: "" })).toBe(false);
  });

  it("asks as soon as both ends are present", () => {
    // The other direction matters as much: a guard that never let the view ask
    // would leave a permanently empty screen, and would look like the endpoint
    // being down.
    expect(shouldLoad({ from: "2026-08-31", to: "2026-09-06" })).toBe(true);
  });

  it("does not judge whether the dates are sensible", () => {
    // Only whether they are there. Whether a window is backwards or too wide is
    // the endpoint's answer to give — it has the site's guardrail and its zone,
    // and a second opinion here would be a rule the view invented.
    expect(shouldLoad({ from: "2026-09-06", to: "2026-08-31" })).toBe(true);
  });
});

describe("whether the empty message is shown", () => {
  it("is shown when the query genuinely matched nothing", () => {
    expect(showsEmptyMessage(0, false)).toBe(true);
  });

  it("is NOT shown after a failed load", () => {
    // "No bookings in this window" is a claim about the site, and after a
    // failure the view does not know whether it is true. Saying it beside the
    // error contradicts the error, and is the answer most likely to be wrong.
    expect(showsEmptyMessage(0, true)).toBe(false);
  });

  it("is not shown when there are results", () => {
    expect(showsEmptyMessage(3, false)).toBe(false);
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

  it("names both days when a booking runs past midnight", () => {
    // Naming only the start date reports an end that appears to precede its own
    // start — "2 Sept, 23:00–02:00" — and an overnight booking is ordinary for
    // a booking system rather than an edge case.
    const overnight = formatInterval(
      booking({ startUtc: "2026-09-02T21:00:00+00:00", endUtc: "2026-09-03T00:30:00+00:00" }),
      "en-GB",
    );

    expect(overnight.text).toContain("2 Sept");
    expect(overnight.text).toContain("3 Sept");
  });

  it("names one day when a booking does not cross midnight", () => {
    // The other direction: repeating the date on every ordinary row is noise.
    expect(formatInterval(booking(), "en-GB").text.match(/Sept/g)).toHaveLength(1);
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

describe("bookingReference", () => {
  it("groups a canonical reference for reading", () => {
    expect(bookingReference({ reference: "7QX4M2NP" })).toBe("7QX4-M2NP");
  });

  it("passes through anything that is not the expected length", () => {
    // Rather than slicing it into something that looks authoritative and is not.
    expect(bookingReference({ reference: "7QX4" })).toBe("7QX4");
    expect(bookingReference({ reference: "" })).toBe("");
  });

  it("survives a row that carries no reference at all", () => {
    // The field is optional on BookingLike, so a caller with an older payload renders a
    // blank cell rather than "undefined".
    expect(bookingReference({})).toBe("");
  });
});
