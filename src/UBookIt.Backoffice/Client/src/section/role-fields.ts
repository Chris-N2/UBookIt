import type { ApiError } from "./api-errors.js";

/**
 * The failure fields the server attributes to one requirement row's controls,
 * mirrored from `ServiceRole.FieldFor`.
 *
 * A list rather than four string literals spread through the element, because
 * every one of them has to appear in two places — the per-row lookup and the
 * "attributed to no row we show" complement — and a field added to one and not
 * the other silently drops the server's message on the floor.
 */
export const ROLE_FIELDS = [
  "ResourceType",
  "RequiredCapabilities",
  "Count",
  "VisitorSelectable",
] as const;

/** The field a failure carries for one control of one row. */
export function roleField(index: number, field: string): string {
  return `Roles[${index}].${field}`;
}

/** Every field name the rows currently on screen can be blamed by. */
export function attributedFields(rowCount: number): Set<string> {
  return new Set(
    Array.from({ length: rowCount }, (_, index) => ROLE_FIELDS.map((f) => roleField(index, f))).flat(),
  );
}

/**
 * The failures belonging to one requirement row: matching code, and a field
 * naming one of that row's controls.
 *
 * The row's own index, never a position in a filtered list. A failure carries the
 * index the request supplied, and the at-most-one-selectable rule reports against
 * EVERY row in conflict — so two rows each find their own message here, which is
 * the whole point of the server blaming both rather than one arbitrarily.
 */
export function errorsForRole(errors: ApiError[], index: number, codes: string[]): ApiError[] {
  const fields = new Set(ROLE_FIELDS.map((field) => roleField(index, field)));

  return errors.filter(
    (e) => e.code !== undefined && codes.includes(e.code) && e.field !== undefined
      && e.field !== null && fields.has(e.field),
  );
}

/**
 * The error summary's lines: one per distinct sentence, in the order the server
 * reported them.
 *
 * A rule the server reports against SEVERAL rows — the at-most-one-selectable
 * rule reports against every row in conflict, deliberately, so each of them can
 * render it against its own control — otherwise prints one identical line per row
 * in the summary, which tells a reader nothing the first line did not and reads as
 * a stutter. Found by reading the rendered summary rather than by a test.
 *
 * Keyed on the sentence a reader sees, so two failures that differ only in which
 * row they blame collapse, and two that say different things both survive. The
 * per-row messages are untouched: the summary is a list of problems, the rows are
 * where the rule points at a control.
 */
export function summaryLines(errors: ApiError[]): ApiError[] {
  return [...new Map(errors.map((e) => [e.message ?? e.code, e])).values()];
}

/**
 * Whether a role's visitor-selectable control says the visitor is choosing one
 * of several.
 *
 * A role of count N with a choice means "this one, plus N−1 chosen for you", and
 * the control has to say so: an editor who believes they are offering a choice of
 * all of them would be configuring something the product does not do. Stated only
 * where it applies — for a count of 1 the plain wording is the honest one, and
 * saying "one of 1" would be noise.
 */
export function selectableHintKey(count: number): string {
  return count > 1 ? "requirementSelectableManyHint" : "requirementSelectableHint";
}
