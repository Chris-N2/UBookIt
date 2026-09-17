import type { ApiError } from "./api-errors.js";

/**
 * The Find control's decisions, as pure functions so they are tested as claims — the client's
 * established pattern, and the only one available: the suite has no DOM environment, so the
 * element renders these and asserts nothing.
 *
 * Three things live here. What kind of thing the operator typed (a reference, an email address,
 * or neither), decided from its SHAPE so the operator never chooses a mode. What the status line
 * and a refusal say, by stable code. And what a row action should reload afterwards — the lookup
 * that found the row, never the window it happened to be opened from.
 */

/**
 * The reference alphabet, verbatim from `BookingReference.Alphabet` in `UBookIt.Core`.
 *
 * **A server-side guard asserts this string is identical to the C# constant**
 * (`BookingReferenceAlphabetTests`), because the whole point of `looksLikeReference` is to agree
 * with `TryParse` — and two copies of an alphabet drift the day somebody edits one. The guard is
 * what turns "these agree" from a claim into a measurement.
 */
export const REFERENCE_ALPHABET = "BCDFGHJKMNPQRSTVWXZ23456789";

/** Canonical references are exactly this long. */
export const REFERENCE_LENGTH = 8;

/**
 * The canonical form of a typed reference, or `null` if it is not one.
 *
 * A port of `BookingReference.TryParse` and nothing looser: skip dashes and whitespace, upper-case
 * everything else, refuse any character outside the alphabet, and require exactly eight. A prefix
 * is not a reference; neither is anything with a vowel in it, and the alphabet has none.
 */
export function canonicalReference(text: string): string | null {
  let out = "";

  for (const ch of text) {
    if (ch === "-" || /\s/.test(ch)) {
      continue;
    }

    if (out.length === REFERENCE_LENGTH) {
      return null;
    }

    const upper = ch.toUpperCase();

    if (!REFERENCE_ALPHABET.includes(upper)) {
      return null;
    }

    out += upper;
  }

  return out.length === REFERENCE_LENGTH ? out : null;
}

export function looksLikeReference(text: string): boolean {
  return canonicalReference(text) !== null;
}

/**
 * Whether the text is an email address, for the purpose of choosing a lookup.
 *
 * Deliberately no stricter than "has an @ with something either side": the server's own
 * validation decides whether it is a real address, and a client that second-guessed it would
 * refuse addresses the server accepts. This only has to tell an address from a reference, and a
 * reference never contains an @.
 */
export function looksLikeEmail(text: string): boolean {
  const trimmed = text.trim();
  const at = trimmed.indexOf("@");

  return at > 0 && at < trimmed.length - 1 && !/\s/.test(trimmed);
}

/** What the operator typed, decided from its shape. */
export type FindKind =
  | { kind: "reference"; canonical: string }
  | { kind: "email"; email: string }
  /** An email address, typed by somebody the email route is not offered to. */
  | { kind: "email-not-offered"; email: string }
  | { kind: "neither" };

/**
 * Which lookup to run — or why none can.
 *
 * A reference wins over an email only in the sense that they cannot collide: a reference has no
 * `@`, an address always does. `emailOffered` is whether this user holds sensitive-data access; a
 * user who does not is told so rather than shown a control that is always refused.
 */
export function classify(text: string, emailOffered: boolean): FindKind {
  const canonical = canonicalReference(text);

  if (canonical !== null) {
    return { kind: "reference", canonical };
  }

  if (looksLikeEmail(text)) {
    const email = text.trim();
    return emailOffered ? { kind: "email", email } : { kind: "email-not-offered", email };
  }

  return { kind: "neither" };
}

/** The view's modes: the windowed list, or one of the two lookups. */
export type FindMode = { mode: "window" } | { mode: "reference"; canonical: string } | { mode: "email"; email: string };

/**
 * The localisation key for a refusal, by stable code — or the generic one for a code this client
 * does not know. A closed map, on the other dialogs' terms.
 */
export function refusalTerm(code: string | undefined): string {
  switch (code) {
    case "reference-invalid":
      return "findRefusedReferenceInvalid";
    case "booking-not-found":
      return "findNotFoundReference";
    case "email-invalid":
      return "findRefusedEmailInvalid";
    default:
      return "findFailed";
  }
}

export function refusalTermFor(errors: ApiError[]): string {
  return refusalTerm(errors[0]?.code);
}

/**
 * Whether a row action in this mode should reload the lookup rather than the window.
 *
 * **The lookup, always, in a lookup mode.** A booking found by its reference and then moved to
 * another date is still the booking that was found; reloading the window instead would make it
 * vanish from under the operator, which is the failure the outside-the-window notice on the
 * placement dialog exists to prevent — arriving here by the other door.
 */
export function reloadsLookup(mode: FindMode): boolean {
  return mode.mode !== "window";
}

/** Whether the window and filter controls apply, and so should be shown. */
export function windowControlsApply(mode: FindMode): boolean {
  return mode.mode === "window";
}
