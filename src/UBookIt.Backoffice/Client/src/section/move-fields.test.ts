import { describe, expect, it } from "vitest";
import { isComplete, prefill, refusalConcernsFields, refusalTerm, refusalTermFor, toRequest } from "./move-fields.js";

describe("what the move modal opens showing", () => {
  it("is the booking's current interval, in the zone the booking records", () => {
    // 08:00Z on a BST date is 09:00 in London. The row shows 09:00; so must the modal.
    const fields = prefill({
      startUtc: "2026-09-15T08:00:00+00:00",
      endUtc: "2026-09-15T09:30:00+00:00",
      timeZoneId: "Europe/London",
    });

    expect(fields).toEqual({ date: "2026-09-15", time: "09:00", lengthMinutes: 90 });
  });

  it("uses a 24-hour clock whatever the locale would prefer", () => {
    // 14:00 must not come back as "02:00" with a dropped PM — the time input is 24-hour.
    const fields = prefill({
      startUtc: "2026-09-15T13:00:00+00:00",
      endUtc: "2026-09-15T14:00:00+00:00",
      timeZoneId: "Europe/London",
    });

    expect(fields.time).toBe("14:00");
  });

  it("falls back to UTC for a zone the runtime does not know, as the row does", () => {
    const fields = prefill({
      startUtc: "2026-09-15T08:00:00+00:00",
      endUtc: "2026-09-15T09:00:00+00:00",
      timeZoneId: "Not/AZone",
    });

    expect(fields).toEqual({ date: "2026-09-15", time: "08:00", lengthMinutes: 60 });
  });

  it("crosses a date boundary in the booking's zone, not UTC's", () => {
    // 23:30Z on the 14th is 00:30 on the 15th in London.
    const fields = prefill({
      startUtc: "2026-09-14T23:30:00+00:00",
      endUtc: "2026-09-15T00:30:00+00:00",
      timeZoneId: "Europe/London",
    });

    expect(fields.date).toBe("2026-09-15");
    expect(fields.time).toBe("00:30");
  });
});

describe("what the move modal sends", () => {
  it("sends a wall-clock start with no offset, and the length", () => {
    // No Z and no +01:00: the server reads it in the site's zone, once. An offset here
    // would make the client the second implementation of a daylight-saving rule.
    expect(toRequest({ date: "2026-09-20", time: "09:00", lengthMinutes: 60 })).toEqual({
      start: "2026-09-20T09:00:00",
      lengthMinutes: 60,
    });
  });
});

describe("whether the fields can be sent", () => {
  it("needs a date, a time and a positive length", () => {
    expect(isComplete({ date: "2026-09-20", time: "09:00", lengthMinutes: 60 })).toBe(true);
    expect(isComplete({ date: "", time: "09:00", lengthMinutes: 60 })).toBe(false);
    expect(isComplete({ date: "2026-09-20", time: "", lengthMinutes: 60 })).toBe(false);
    expect(isComplete({ date: "2026-09-20", time: "09:00", lengthMinutes: 0 })).toBe(false);
    expect(isComplete({ date: "2026-09-20", time: "09:00", lengthMinutes: -30 })).toBe(false);
    expect(isComplete({ date: "2026-09-20", time: "09:00", lengthMinutes: 1.5 })).toBe(false);
  });
});

describe("what a refusal says", () => {
  it("maps each stable code the domain can answer with to its own sentence", () => {
    expect(refusalTerm("outside-open-hours")).toBe("moveRefusedOutsideOpenHours");
    expect(refusalTerm("conflict")).toBe("moveRefusedConflict");
    expect(refusalTerm("lead-time")).toBe("moveRefusedInThePast");
    expect(refusalTerm("interval-unchanged")).toBe("moveRefusedUnchanged");
    expect(refusalTerm("invalid-status-transition")).toBe("moveRefusedStatus");
    expect(refusalTerm("granularity")).toBe("moveRefusedGranularity");
    expect(refusalTerm("duration-too-long")).toBe("moveRefusedDuration");
    expect(refusalTerm("duration-too-short")).toBe("moveRefusedDuration");
    expect(refusalTerm("interval-invalid")).toBe("moveRefusedInterval");
    expect(refusalTerm("booking-not-found")).toBe("moveRefusedNotFound");
  });

  it("says the generic sentence for a code it does not know, rather than the raw code", () => {
    // Closed rather than open: a code added to the domain later arrives as a deliberate
    // generic sentence, not as "horizon" in a dialog.
    expect(refusalTerm("horizon")).toBe("moveFailed");
    expect(refusalTerm(undefined)).toBe("moveFailed");
    expect(refusalTerm("")).toBe("moveFailed");
  });

  it("explains the first failure, which is the one the pipeline puts first", () => {
    expect(
      refusalTermFor([
        { code: "outside-open-hours", message: "..." },
        { code: "conflict", message: "..." },
      ]),
    ).toBe("moveRefusedOutsideOpenHours");
    expect(refusalTermFor([])).toBe("moveFailed");
  });

  it("marks the inputs invalid only when a field is what was refused", () => {
    // A cancelled booking, or one that no longer exists, has nothing wrong with its date;
    // aria-invalid on the inputs there would send the operator to fix a time that was fine.
    expect(refusalConcernsFields("moveRefusedOutsideOpenHours")).toBe(true);
    expect(refusalConcernsFields("moveRefusedConflict")).toBe(true);
    expect(refusalConcernsFields("moveRefusedInThePast")).toBe(true);
    expect(refusalConcernsFields("moveRefusedUnchanged")).toBe(true);
    expect(refusalConcernsFields("moveIncomplete")).toBe(true);
    expect(refusalConcernsFields("moveRefusedStatus")).toBe(false);
    expect(refusalConcernsFields("moveRefusedNotFound")).toBe(false);
    expect(refusalConcernsFields("moveFailed")).toBe(false);
  });

  it("has a string in the localisation for every term it can name", async () => {
    const { default: terms } = await import("../localization/en-us.js");
    const bookings = (terms as Record<string, Record<string, string>>).ubookitBookings;

    for (const code of [
      "outside-open-hours",
      "conflict",
      "lead-time",
      "interval-unchanged",
      "invalid-status-transition",
      "granularity",
      "duration-too-long",
      "interval-invalid",
      "booking-not-found",
      "anything-else",
    ]) {
      const key = refusalTerm(code);
      expect(typeof bookings[key], key).toBe("string");
      expect(bookings[key].length, key).toBeGreaterThan(0);
    }
  });
});
