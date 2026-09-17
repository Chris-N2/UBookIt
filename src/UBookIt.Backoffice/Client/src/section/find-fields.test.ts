import { describe, expect, it } from "vitest";
import {
  REFERENCE_ALPHABET,
  REFERENCE_LENGTH,
  canonicalReference,
  classify,
  looksLikeEmail,
  looksLikeReference,
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
  ])("refuses %s", (_, typed) => {
    expect(canonicalReference(typed)).toBeNull();
    expect(looksLikeReference(typed)).toBe(false);
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
