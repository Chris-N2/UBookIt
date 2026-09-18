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
 * (`BookingReferenceAlphabetTests`), because the whole point of `canonicalReference` is to agree
 * with `TryParse` — and two copies of an alphabet drift the day somebody edits one. The guard is
 * what turns "these agree" from a claim into a measurement.
 */
export const REFERENCE_ALPHABET = "BCDFGHJKMNPQRSTVWXZ23456789";

/** Canonical references are exactly this long. */
export const REFERENCE_LENGTH = 8;

/**
 * The characters `TryParse` skips as whitespace — <b>.NET's `char.IsWhiteSpace` set, verbatim.</b>
 *
 * **Not JavaScript's `\s`, which is a different set**, and the difference was measurable rather
 * than theoretical: `\s` omits U+0085 (NEL) and includes U+FEFF, so a reference pasted with a NEL
 * in it was refused in place by this client while the server would have found the booking, and
 * one containing a BOM was canonicalised here and rejected there. Two whitespace definitions are
 * two parsers, exactly as two alphabets would be two alphabets.
 *
 * Held equal to `char.IsWhiteSpace` by `BookingReferenceAlphabetTests`, which enumerates every
 * BMP code point rather than trusting this comment.
 */
export const REFERENCE_WHITESPACE =
  "\u0009\u000A\u000B\u000C\u000D\u0020\u0085\u00A0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000";

/**
 * The canonical form of a typed reference, or `null` if it is not one.
 *
 * A port of `BookingReference.TryParse` and nothing looser: skip dashes and whitespace, upper-case
 * everything else, refuse any character outside the alphabet, and require exactly eight. A prefix
 * is not a reference; neither is anything with a vowel in it, and the alphabet has none.
 *
 * "Whitespace" means {@link REFERENCE_WHITESPACE} — .NET's set, not JavaScript's. The two differ,
 * and while they differed this function disagreed with the parser it claims to port.
 */
export function canonicalReference(text: string): string | null {
  let out = "";

  for (const ch of text) {
    if (ch === "-" || REFERENCE_WHITESPACE.includes(ch)) {
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

/**
 * A canonical reference as a person reads it — grouped, as `BookingReference.Display` groups it.
 *
 * **The screen must not show one reference two ways.** The row, the action buttons and the move
 * dialog all render the grouped form; a status line saying `BJQ4ZP5C` beside a row saying
 * `BJQ4-ZP5C` reads as two different bookings, which a live check caught.
 */
export function displayReference(canonical: string): string {
  return canonical.length === REFERENCE_LENGTH
    ? `${canonical.slice(0, REFERENCE_LENGTH / 2)}-${canonical.slice(REFERENCE_LENGTH / 2)}`
    : canonical;
}

/**
 * Whether a refusal means "no booking has that reference" rather than "the lookup failed".
 *
 * **Keyed on the 404 status, because the domain's code does not survive the journey** — which was
 * MEASURED against the running backoffice, not reasoned about. An earlier version of this function
 * read `booking-not-found` out of the problem body and was justified in this comment by the claim
 * that "the code travels in the problem body either way". That claim was false, and it shipped a
 * miss reported as a failure.
 *
 * What the backoffice's HTTP client actually does with our problem details, by status:
 *
 * | Refusal              | Status | `errors` extension | `title`                     |
 * | -------------------- | ------ | ------------------ | --------------------------- |
 * | `reference-invalid`  | 400    | **kept**, code intact | ours                     |
 * | `booking-not-found`  | 404    | **discarded**      | replaced by Umbraco's canned |
 *
 * So every other refusal on this screen is still read by its domain code — see {@link refusalTerm}
 * — and this one cannot be. It also THROWS rather than returning, so both call sites must catch.
 *
 * **The status is unambiguous here only because this endpoint maps exactly one failure to 404.**
 * That is a property of `FindBookingByReference`, not a general rule: a second 404-mapped failure
 * added to that endpoint would silently join this branch, and a routing 404 (the package not
 * installed) would read as a miss. Both are stated rather than guarded, because neither is
 * distinguishable once the body has been stripped.
 */
export function isMiss(thrown: unknown): boolean {
  return (thrown as { status?: number } | undefined)?.status === 404;
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

/** The two lookup modes — everything that is not the window. */
export type LookupMode = Exclude<FindMode, { mode: "window" }>;

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
 *
 * **This is the decision `#fetch` actually takes**, not a restatement of it. It was briefly the
 * latter — exported, tested, and called by nothing — which made its test a covering test that
 * could not fail: `#fetch` could have been rewritten to reload the window and the suite would
 * have stayed green. It is a type predicate so the switch that follows it stays exhaustive over
 * the two lookup modes, which is what keeps the call a real one rather than a decoration.
 */
export function reloadsLookup(mode: FindMode): mode is LookupMode {
  return mode.mode !== "window";
}

/**
 * The page to remember as the window's, given the mode the view is in **before** a lookup starts.
 *
 * <b>The parameter is named `modeBeforeFind` because reading it after the mode has changed is a
 * defect that shipped.</b> The first version of this decision was written inline, guarded by
 * `windowControlsApply(this._mode)`, and placed after the statement that reassigns `_mode` — so
 * the test was necessarily false, the field was never written, and "Back to dates" went on
 * restoring page 1 while a comment above it said otherwise. A guard placed where it cannot fire
 * is worse than no guard, because the next reader believes it.
 *
 * `kept` is returned unchanged when a lookup is already running: the second of two lookups must
 * not overwrite the window's page with a lookup's own.
 */
export function windowPageToKeep(modeBeforeFind: FindMode, skip: number, kept: number): number {
  return windowControlsApply(modeBeforeFind) ? skip : kept;
}

/** Whether the window and filter controls apply, and so should be shown. */
export function windowControlsApply(mode: FindMode): mode is { mode: "window" } {
  return mode.mode === "window";
}
