import { describe, expect, it } from "vitest";
import {
  BOOKINGS_MANAGE_VERB,
  BOOKINGS_READ_VERB,
  CONFIGURE_VERB,
  canConfigure,
  canManageBookings,
  canReadBookings,
  hasAnyVerb,
} from "./permission-verbs.js";

/**
 * The client's verb decisions, mirroring the server's rules — including the one
 * implication (Manage implies Read) — tested per capability with its own direction, so
 * a rule collapsed to a majority answer fails a named test (the email-templates
 * parameterised-dimension lesson).
 */

describe("the implication", () => {
  it("read is satisfied by the read verb", () => {
    expect(canReadBookings([BOOKINGS_READ_VERB])).toBe(true);
  });

  it("read is satisfied by the manage verb alone", () => {
    // The implication as a rule: a group holding only Manage reads, without the read
    // verb being stored anywhere.
    expect(canReadBookings([BOOKINGS_MANAGE_VERB])).toBe(true);
  });

  it("manage is NOT satisfied by the read verb", () => {
    expect(canManageBookings([BOOKINGS_READ_VERB])).toBe(false);
  });
});

describe("capability boundaries", () => {
  it("configure reaches neither booking capability", () => {
    expect(canReadBookings([CONFIGURE_VERB])).toBe(false);
    expect(canManageBookings([CONFIGURE_VERB])).toBe(false);
    expect(canConfigure([CONFIGURE_VERB])).toBe(true);
  });

  it("booking verbs do not configure", () => {
    expect(canConfigure([BOOKINGS_READ_VERB, BOOKINGS_MANAGE_VERB])).toBe(false);
  });
});

describe("edges", () => {
  it("an undefined permission set grants nothing", () => {
    // The current user may not have loaded yet; nothing may flicker visible on the
    // strength of an absent answer.
    expect(canReadBookings(undefined)).toBe(false);
    expect(canManageBookings(undefined)).toBe(false);
    expect(canConfigure(undefined)).toBe(false);
  });

  it("an empty set grants nothing", () => {
    expect(hasAnyVerb([], BOOKINGS_READ_VERB)).toBe(false);
  });

  it("foreign permissions grant nothing", () => {
    expect(canReadBookings(["Umb.Document.Read"])).toBe(false);
  });
});
