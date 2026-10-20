import type { ApiError } from "./api-errors.js";

/**
 * The "Email template set" field's decisions, as pure functions so they are tested as claims — the
 * client's established pattern, because the suite has no DOM environment.
 *
 * Four things live here: what the input shows for a service the server returned (on load, and
 * again after a save, so the stored lower-case name is what the editor sees), what a save sends,
 * which failures belong to the field rather than to the Details group, and how the field is
 * described.
 */

/** The failure code the server gives a set name it cannot accept. */
export const TEMPLATE_SET_INVALID = "service-template-set-invalid";

/** The failure field the server names for the set. */
export const TEMPLATE_SET_FIELD = "templateSet";

/** The ids the editor renders, so the association the tests assert is the one rendered. */
export const TEMPLATE_SET_ID = "service-template-set";
export const TEMPLATE_SET_HINT_ID = "service-template-set-hint";
export const TEMPLATE_SET_ERROR_ID = "err-template-set";

/**
 * The input's value for a service the server returned. A server that omitted the member, or sent
 * `null`, means the service names no set — which every service saved before the field existed is.
 * The value is the STORED one, so after a save that lowered `Remote` the field shows `remote`: the
 * folder name the editor actually has to create.
 */
export function templateSetFromResponse(value: string | null | undefined): string {
  return value ?? "";
}

/**
 * What the input shows once a save has answered. A successful save answers with the service as
 * stored, so the field takes the stored name — `Remote` typed, `remote` shown, the folder that will
 * actually be used. With no service in the answer (the save failed) the field keeps exactly what was
 * typed, so nothing the editor entered is lost.
 */
export function templateSetAfterSave(
  typed: string,
  saved: { templateSet?: string | null } | undefined,
): string {
  return saved === undefined ? typed : templateSetFromResponse(saved.templateSet);
}

/**
 * What a save sends. The name goes exactly as entered: trimming, lowering and the character rule
 * are the server's, so they have one implementation. Blank is sent as `null`, which the server
 * reads the same way, so an emptied field removes the set rather than being mistaken for "leave it
 * alone".
 */
export function templateSetForRequest(value: string): string | null {
  return value.trim() === "" ? null : value;
}

/**
 * The failures to render against the field. Matched on the code, whatever field the server named
 * — the code says what is wrong and there is only one control it can mean — so a failure can never
 * fall between this field and the group and go unsaid.
 */
export function templateSetErrors(errors: readonly ApiError[]): ApiError[] {
  return errors.filter((e) => e.code === TEMPLATE_SET_INVALID);
}

/**
 * The field's `aria-describedby`: always the hint, and the error as well while there is one, so a
 * screen reader announces both the rule and what went wrong with the control they belong to.
 */
export function templateSetDescribedBy(errors: readonly ApiError[]): string {
  return templateSetErrors(errors).length > 0
    ? `${TEMPLATE_SET_HINT_ID} ${TEMPLATE_SET_ERROR_ID}`
    : TEMPLATE_SET_HINT_ID;
}
