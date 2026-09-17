import { describe, expect, it } from "vitest";
import {
  emptyFields,
  isComplete,
  placedInsideWindow,
  placedLocalDate,
  refusalConcernsBooker,
  refusalTerm,
  refusalTermFor,
  toRequest,
  type PlaceOnBehalfFields,
} from "./place-on-behalf-fields.js";

const complete: PlaceOnBehalfFields = {
  subject: { kind: "resource", id: "r1" },
  date: "2026-09-15",
  time: "10:00",
  lengthMinutes: 60,
  bookerName: "Ada Lovelace",
  bookerEmail: "ada@example.com",
  bookerPhone: "",
};

describe("emptyFields", () => {
  it("opens on the date the operator is already looking at", () => {
    // Not today: a default of today on a screen showing another week produces a booking that
    // vanishes from the table the moment it is made.
    expect(emptyFields("2026-10-05").date).toBe("2026-10-05");
  });

  it("chooses nothing to book", () => {
    expect(emptyFields("2026-10-05").subject).toBeUndefined();
  });
});

describe("toRequest", () => {
  it("sends a wall-clock start with no offset", () => {
    // No Z and no +01:00. The server resolves the start against the site's zone, once; an
    // offset here would make the client a second implementation of a daylight-saving rule.
    expect(toRequest(complete).start).toBe("2026-09-15T10:00:00");
  });

  it("names a resource, and no service, when a resource was chosen", () => {
    const body = toRequest(complete);

    expect(body.resourceId).toBe("r1");
    expect("serviceId" in body).toBe(false);
  });

  it("names a service, and no resource, when a service was chosen", () => {
    const body = toRequest({ ...complete, subject: { kind: "service", id: "s1" } });

    expect(body.serviceId).toBe("s1");
    expect("resourceId" in body).toBe(false);
  });

  it("cannot produce a body naming both, whatever the fields hold", () => {
    // The server refuses a request naming both. Building the body from ONE chosen subject is
    // what stops the client producing that request by forgetting to clear the other field —
    // which is the shape two nullable fields would have.
    const asService = toRequest({ ...complete, subject: { kind: "service", id: "s1" } });
    const asResource = toRequest({ ...complete, subject: { kind: "resource", id: "r1" } });

    expect(Number("serviceId" in asService) + Number("resourceId" in asService)).toBe(1);
    expect(Number("serviceId" in asResource) + Number("resourceId" in asResource)).toBe(1);
  });

  it("omits a blank telephone number rather than sending an empty string", () => {
    expect("bookerPhone" in toRequest({ ...complete, bookerPhone: "   " })).toBe(false);
  });

  it("sends a telephone number that was given, trimmed", () => {
    expect(toRequest({ ...complete, bookerPhone: " 07700 900123 " }).bookerPhone).toBe("07700 900123");
  });

  it("trims the name and the address", () => {
    const body = toRequest({ ...complete, bookerName: " Ada ", bookerEmail: " ada@example.com " });

    expect(body.bookerName).toBe("Ada");
    expect(body.bookerEmail).toBe("ada@example.com");
  });
});

describe("isComplete", () => {
  it("accepts a fully filled form", () => {
    expect(isComplete(complete)).toBe(true);
  });

  it.each([
    ["nothing to book", { subject: undefined }],
    ["an unchosen subject", { subject: { kind: "resource", id: "" } as const }],
    ["no date", { date: "" }],
    ["no time", { time: "" }],
    ["a zero length", { lengthMinutes: 0 }],
    ["a negative length", { lengthMinutes: -30 }],
    ["a fractional length", { lengthMinutes: 12.5 }],
    ["no booker name", { bookerName: "  " }],
    ["no booker address", { bookerEmail: "  " }],
  ])("refuses %s", (_, patch) => {
    expect(isComplete({ ...complete, ...(patch as Partial<PlaceOnBehalfFields>) })).toBe(false);
  });

  it("does not judge whether an address is deliverable", () => {
    // Shape only. Whether an address is valid is the server's answer, and a client that
    // second-guessed it would refuse addresses the domain accepts.
    expect(isComplete({ ...complete, bookerEmail: "ada@example.com" })).toBe(true);
  });
});

describe("refusalTerm", () => {
  it.each([
    ["outside-open-hours", "placeRefusedOutsideOpenHours"],
    ["conflict", "placeRefusedConflict"],
    ["lead-time", "placeRefusedInThePast"],
    ["granularity", "placeRefusedGranularity"],
    ["duration-too-short", "placeRefusedDuration"],
    ["duration-too-long", "placeRefusedDuration"],
    ["interval-invalid", "placeRefusedInterval"],
    ["resource-not-found", "placeRefusedSubjectNotFound"],
    ["service-not-found", "placeRefusedSubjectNotFound"],
    ["service-unavailable", "placeRefusedServiceUnavailable"],
    ["resource-not-eligible", "placeRefusedResourceNotEligible"],

    // Found live: the dialog marked the email invalid and focused it while saying only the
    // generic sentence, so an operator got an outlined box and no reason.
    ["email-invalid", "placeRefusedEmail"],
    ["name-required", "placeRefusedName"],
  ])("explains %s", (code, term) => {
    expect(refusalTerm(code)).toBe(term);
  });

  it("falls back for a code it does not know", () => {
    // A code added to the domain later arrives as the generic sentence and a deliberate
    // decision, never as a raw code in a dialog.
    expect(refusalTerm("some-future-code")).toBe("placeFailed");
    expect(refusalTerm(undefined)).toBe("placeFailed");
  });

  it("reports the FIRST error, because the domain orders failures by its pipeline", () => {
    expect(
      refusalTermFor([
        { code: "conflict", message: "" },
        { code: "outside-open-hours", message: "" },
      ]),
    ).toBe("placeRefusedConflict");
  });
});

describe("refusalConcernsBooker", () => {
  it("is true when a booker field was named", () => {
    expect(refusalConcernsBooker([{ code: "invalid", message: "", field: "BookerEmail" }])).toBe(true);
  });

  it("is false for a refusal about the time", () => {
    // Marking the time inputs invalid because an address was malformed would send an operator
    // to change a time that was fine.
    expect(refusalConcernsBooker([{ code: "outside-open-hours", message: "" }])).toBe(false);
  });
});

describe("placedInsideWindow", () => {
  it("is true on the window's first and last day", () => {
    // Inclusive at both edges, because the list's window is.
    expect(placedInsideWindow("2026-09-01", "2026-09-01", "2026-09-07")).toBe(true);
    expect(placedInsideWindow("2026-09-07", "2026-09-01", "2026-09-07")).toBe(true);
  });

  it("is false either side of it", () => {
    expect(placedInsideWindow("2026-08-31", "2026-09-01", "2026-09-07")).toBe(false);
    expect(placedInsideWindow("2026-09-08", "2026-09-01", "2026-09-07")).toBe(false);
  });
});

describe("placedLocalDate", () => {
  it("reads the date in the zone the response reports", () => {
    // 23:30 UTC is already the next day in Auckland. Taking the date from the instant alone
    // would tell an operator the booking fell outside a window it is in.
    expect(placedLocalDate("2026-09-15T23:30:00Z", "Pacific/Auckland")).toBe("2026-09-16");
    expect(placedLocalDate("2026-09-15T23:30:00Z", "UTC")).toBe("2026-09-15");
  });

  it("falls back to UTC for a zone the runtime does not know", () => {
    expect(placedLocalDate("2026-09-15T23:30:00Z", "Mars/Olympus_Mons")).toBe("2026-09-15");
  });
});
