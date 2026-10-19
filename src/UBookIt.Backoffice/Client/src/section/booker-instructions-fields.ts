import type { ApiError } from "./api-errors.js";

/**
 * The "Instructions for the booker" field's decisions, as pure functions so they are tested as
 * claims — the client's established pattern, because the suite has no DOM environment.
 *
 * Three things live here: what the textarea shows for a loaded service, what a save sends, and
 * which failures belong to the field rather than to the Details group.
 */

/** The failure code the server gives instructions longer than its limit. */
export const BOOKER_INSTRUCTIONS_TOO_LONG = "service-booker-instructions-too-long";

/** The failure field the server names for the instructions. */
export const BOOKER_INSTRUCTIONS_FIELD = "bookerInstructions";

/** The ids the editor renders, so the association the tests assert is the one rendered. */
export const BOOKER_INSTRUCTIONS_ID = "service-booker-instructions";
export const BOOKER_INSTRUCTIONS_HINT_ID = "service-booker-instructions-hint";
export const BOOKER_INSTRUCTIONS_ERROR_ID = "err-booker-instructions";

/**
 * The textarea's value for a loaded service. A server that omitted the member, or sent `null`,
 * means the service has none — which every service saved before the field existed is.
 */
export function instructionsFromResponse(value: string | null | undefined): string {
  return value ?? "";
}

/**
 * What a save sends. The text goes exactly as entered, line breaks included: trimming and the
 * length rule are the server's, so they have one implementation. Blank is sent as `null`, which
 * the server reads the same way, so an emptied field removes the instructions rather than being
 * mistaken for "leave them alone".
 */
export function instructionsForRequest(value: string): string | null {
  return value.trim() === "" ? null : value;
}

/**
 * The failures to render against the field. Matched on the code, whatever field the server named
 * — the code says what is wrong and there is only one control it can mean — so a failure can never
 * fall between this field and the group and go unsaid.
 */
export function instructionsErrors(errors: readonly ApiError[]): ApiError[] {
  return errors.filter((e) => e.code === BOOKER_INSTRUCTIONS_TOO_LONG);
}

/**
 * The field's `aria-describedby`: always the hint, and the error as well while there is one, so a
 * screen reader announces both the rule and what went wrong with the control they belong to.
 */
export function instructionsDescribedBy(errors: readonly ApiError[]): string {
  return instructionsErrors(errors).length > 0
    ? `${BOOKER_INSTRUCTIONS_HINT_ID} ${BOOKER_INSTRUCTIONS_ERROR_ID}`
    : BOOKER_INSTRUCTIONS_HINT_ID;
}
