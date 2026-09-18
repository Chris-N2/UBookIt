import { describe, expect, it } from "vitest";
import {
  REFERENCE_ALPHABET,
  REFERENCE_LENGTH,
  canonicalReference,
  classify,
  displayReference,
  isMiss,
  looksLikeEmail,
  refusalTerm,
  refusalTermFor,
  reloadsLookup,
  windowControlsApply,
} from "./find-fields.js";

describe("canonicalReference — a port of BookingReference.TryParse", () => {
  // The vectors below mirror the C# suite's. The alphabet constant itself is held equal to the
  // C# one by a server-side guard; these prove the RULE over it is the same rule.

  it.each([
    ["BJQ4ZP5C", "BJQ4ZP5C"],
    ["BJQ4-ZP5C", "BJQ4ZP5C"],
    ["bjq4-zp5c", "BJQ4ZP5C"],
    ["  bjq4 zp5c  ", "BJQ4ZP5C"],
    ["b-j-q-4-z-p-5-c", "BJQ4ZP5C"],
    // U+0085 (NEL) is whitespace to .NET and NOT to JavaScript's \s. While this client used
    // \s it refused this in place, without a request, for a booking the server would have
    // found. QA measured it; the separator set is now .NET's, held equal by a C# guard.
    ["BJQ4ZP5C", "BJQ4ZP5C"],
  ])("accepts %s as %s", (typed, canonical) => {
    expect(canonicalReference(typed)).toBe(canonical);
  });

  it.each([
    ["a prefix", "BJQ4"],
    ["seven characters", "BJQ4ZP5"],
    ["nine characters", "BJQ4ZP5CX"],
    ["a vowel", "BJQ4ZP5A"],
    ["a Y, which is a vowel", "BJQ4ZP5Y"],
    ["a zero", "BJQ4ZP50"],
    ["a one", "BJQ4ZP51"],
    ["an L", "BJQ4ZP5L"],
    ["an O", "BJQ4ZP5O"],
    ["an I", "BJQ4ZP5I"],
    ["a U", "BJQ4ZP5U"],
    ["punctuation", "BJQ4.ZP5C"],
    ["an @, which makes it an address instead", "BJQ4@ZP5C"],
    ["nothing", ""],
    ["only separators", "- - -"],
    // The converse of the NEL case: U+FEFF IS whitespace to JavaScript and is not to .NET, so
    // while this client used \s it canonicalised this and the server then refused it.
    ["a BOM, which .NET does not treat as whitespace", "BJQ4﻿ZP5C"],
  ])("refuses %s", (_, typed) => {
    expect(canonicalReference(typed)).toBeNull();
  });

  it("uses an alphabet with no vowels and none of the transcription confusions", () => {
    // Restating the two properties the C# type documents, so a drift in THIS copy is caught
    // here even before the server-side equality guard runs.
    for (const forbidden of "AEIOUY01LO") {
      expect(REFERENCE_ALPHABET).not.toContain(forbidden);
    }
    expect(REFERENCE_LENGTH).toBe(8);
  });
});

describe("displayReference", () => {
  it("groups a canonical reference the way a person reads it", () => {
    // FOUND LIVE: the status line showed BJQ4ZP5C beside a row showing BJQ4-ZP5C. One reference,
    // two forms, adjacent on screen — which reads as two different bookings.
    expect(displayReference("BJQ4ZP5C")).toBe("BJQ4-ZP5C");
  });

  it("leaves anything that is not a canonical reference alone", () => {
    // Defensive: the caller always holds a canonical value, and a formatter that invented a dash
    // in the middle of something else would make a wrong value look official.
    expect(displayReference("BJQ4")).toBe("BJQ4");
  });
});

describe("isMiss", () => {
  // THESE TWO OBJECTS WERE CAPTURED FROM THE RUNNING BACKOFFICE, not composed here — by calling
  // the generated client against the live endpoint and printing what it threw. That matters:
  // the first version of `isMiss` read `booking-not-found` out of an `errors` array, and passed a
  // test whose fixture was an `errors` array somebody had typed. The fixture agreed with the
  // assertion and both disagreed with the interceptor, so the suite stayed green while the screen
  // reported every miss as a failure. Re-capture these if Umbraco's client changes; do not edit
  // them to make a test pass.
  const thrownFor404 = { status: 404, title: "The requested resource was not found.", type: "NotFound" };
  const thrownFor400 = {
    status: 400,
    title: "Validation failed",
    type: "ValidationFailed",
    errors: [
      {
        code: "reference-invalid",
        message: "That is not a booking reference: eight letters and digits, with or without a dash.",
        field: "reference",
      },
    ],
  };

  it("is true for the 404 the miss arrives as", () => {
    expect(isMiss(thrownFor404)).toBe(true);
  });

  it("is false for a refusal that is not a miss", () => {
    expect(isMiss(thrownFor400)).toBe(false);
  });

  it("is false for anything with no status at all", () => {
    expect(isMiss(undefined)).toBe(false);
    expect(isMiss(new Error("network down"))).toBe(false);
    expect(isMiss({})).toBe(false);
  });

  it("records that the 404 arrives with its errors extension STRIPPED", () => {
    // This is the measurement the implementation rests on, asserted so a change in Umbraco's
    // interceptor that restores the body breaks a test rather than passing silently — at which
    // point keying on the domain code becomes available again, and preferable.
    expect(thrownFor404).not.toHaveProperty("errors");
    expect(thrownFor400.errors[0].code).toBe("reference-invalid");
  });
});

describe("looksLikeEmail", () => {
  it.each(["ada@example.com", " ada@example.com ", "a@b"])("accepts %s", (text) => {
    expect(looksLikeEmail(text)).toBe(true);
  });

  it.each(["@example.com", "ada@", "ada", "ada @example.com", ""])("refuses %s", (text) => {
    expect(looksLikeEmail(text)).toBe(false);
  });
});

describe("classify", () => {
  it("chooses the reference lookup for a reference", () => {
    expect(classify("bjq4-zp5c", true)).toEqual({ kind: "reference", canonical: "BJQ4ZP5C" });
  });

  it("chooses the email lookup for an address, when offered", () => {
    expect(classify(" ada@example.com ", true)).toEqual({ kind: "email", email: "ada@example.com" });
  });

  it("names the reason when an address is typed by somebody not offered the email route", () => {
    // Not "neither": the operator typed something recognisable, and the honest answer is that
    // finding by email needs the Sensitive data group — which tells them who to ask.
    expect(classify("ada@example.com", false)).toEqual({ kind: "email-not-offered", email: "ada@example.com" });
  });

  it("is neither for anything else", () => {
    expect(classify("not-a-thing", true)).toEqual({ kind: "neither" });
    expect(classify("", true)).toEqual({ kind: "neither" });
  });

  it("cannot mistake one for the other", () => {
    // A reference never contains an @, and an address always does — so no input classifies as
    // both, and the order of the two tests inside classify cannot matter.
    expect(classify("BJQ4ZP5C", true).kind).toBe("reference");
    expect(classify("BJQ4ZP5C@x.y", true).kind).toBe("email");
  });
});

describe("refusalTerm", () => {
  it.each([
    ["reference-invalid", "findRefusedReferenceInvalid"],
    ["booking-not-found", "findNotFoundReference"],
    ["email-invalid", "findRefusedEmailInvalid"],
  ])("explains %s", (code, term) => {
    expect(refusalTerm(code)).toBe(term);
  });

  it("falls back for a code it does not know", () => {
    expect(refusalTerm("some-future-code")).toBe("findFailed");
    expect(refusalTermFor([])).toBe("findFailed");
  });

  it("reports the first error", () => {
    expect(refusalTermFor([{ code: "reference-invalid", message: "" }, { code: "x", message: "" }])).toBe(
      "findRefusedReferenceInvalid",
    );
  });
});

describe("after a row action", () => {
  // THIS GUARDS PRODUCTION because `#fetch` takes its window-vs-lookup branch by calling
  // `reloadsLookup`, rather than restating the rule with its own comparison. It briefly did the
  // latter — the function was exported, tested here, and imported by nothing — which made this
  // suite a covering test that could not fail: `#fetch` could have been rewritten to reload the
  // window and these assertions would have stayed green. QA found it.
  //
  // Verified by mutation: inverting the comparison in `reloadsLookup` fails these tests AND sends
  // every lookup to the window read. A guard that stays green through the regression it names is
  // not a guard.
  it("reloads the lookup in a lookup mode, never the window", () => {
    expect(reloadsLookup({ mode: "reference", canonical: "BJQ4ZP5C" })).toBe(true);
    expect(reloadsLookup({ mode: "email", email: "ada@example.com" })).toBe(true);
  });

  it("reloads the window in the window mode", () => {
    expect(reloadsLookup({ mode: "window" })).toBe(false);
  });
});

describe("the window controls", () => {
  it("apply only in the window mode", () => {
    expect(windowControlsApply({ mode: "window" })).toBe(true);
    expect(windowControlsApply({ mode: "reference", canonical: "BJQ4ZP5C" })).toBe(false);
    expect(windowControlsApply({ mode: "email", email: "ada@example.com" })).toBe(false);
  });
});
