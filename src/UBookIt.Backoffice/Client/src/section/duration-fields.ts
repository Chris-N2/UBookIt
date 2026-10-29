/**
 * The services editor's Duration group decisions, as pure functions so they are tested as
 * claims — the client's established pattern, because the suite has no DOM environment.
 */

/**
 * The two duration kinds, matching the wire contract exactly. "variable" means
 * the person booking chooses the length, within whichever bounds are supplied
 * and always within what the booked resource itself allows.
 */
export type DurationMode = "variable" | "fixed";

/**
 * Whether the hint explaining a variable length is shown. Only for Variable length: the hint
 * says "the person booking chooses how long they need", which is false of a fixed-length
 * service, and it was shown for one until umbraco-floor-17-8. Fixed length needs no hint beyond
 * its field's label.
 */
export function showsVariableLengthHint(mode: DurationMode): boolean {
  return mode === "variable";
}
