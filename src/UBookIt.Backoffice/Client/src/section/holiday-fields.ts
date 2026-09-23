import type { HolidayRowModel } from "../api/index.js";

/**
 * The holiday import's decisions, as pure functions so they are tested as claims — the
 * client's established pattern.
 *
 * The one that matters most is {@link isSelectable}. The preview is a SELECTION, not a
 * confirmation: an operator unticks the dates their organisation is not closed on, and only
 * the ticked rows are created. A row that is offered when it should not be produces a closure
 * nobody asked for; a row withheld when it should be offered silently loses a date.
 */

/** A row an operator may tick. */
export const SELECTABLE_STATE = "new";

/**
 * Whether the operator may choose this row.
 *
 * Only `new` is selectable. `alreadyClosed` has nothing to create — at most one closure may
 * exist per date, and the existing one is not this import's to replace. `cannotImport` would be
 * refused by the server, and offering it would be a control that looks live and is not.
 */
export function isSelectable(row: Pick<HolidayRowModel, "state">): boolean {
  return row.state === SELECTABLE_STATE;
}

/**
 * The dates ticked when a preview first arrives: every selectable row.
 *
 * Selected by default because the common case is "the organisation is closed on the public
 * holidays", and the operator's work is unticking the exceptions — which is the shape of the
 * one system this was modelled on.
 */
export function initialSelection(rows: ReadonlyArray<HolidayRowModel>): string[] {
  return rows.filter(isSelectable).map((row) => row.date);
}

/**
 * What an import sends: the ticked rows, and nothing else.
 *
 * Derived from the whole list every time rather than accumulated, so unticking genuinely
 * removes a date — the same full-replacement reasoning as a resource's closure opt-outs. A row
 * that is not selectable can never be sent, even if a stale selection still names its date.
 */
export function chosenRows(
  rows: ReadonlyArray<HolidayRowModel>,
  selected: ReadonlyArray<string>,
): Array<{ date: string; name: string }> {
  return rows
    .filter((row) => isSelectable(row) && selected.includes(row.date))
    .map((row) => ({ date: row.date, name: row.name }));
}

/** Toggling one date, as a new selection rather than a mutation. */
export function toggleDate(selected: ReadonlyArray<string>, date: string, ticked: boolean): string[] {
  const without = selected.filter((value) => value !== date);
  return ticked ? [...without, date] : without;
}

/**
 * The window a preview opens on: today through the end of NEXT year.
 *
 * Wide enough to cover the case an operator actually has — "the rest of this year and all of
 * next" — without asking them to work out a range, and both ends are editable for anyone who
 * wants a specific year. Taken from a clock passed in, so the default is testable at an instant
 * where the two years genuinely differ.
 */
export function defaultWindow(now: Date): { from: string; to: string } {
  const iso = (date: Date) => date.toISOString().slice(0, 10);

  return {
    from: iso(now),
    to: `${now.getUTCFullYear() + 1}-12-31`,
  };
}

/**
 * What the screen says about a preview that returned no rows.
 *
 * **An empty result and a failed source are different facts and must never render the same.**
 * A window with no holidays in it is a correct answer; a source that threw is not an answer at
 * all, and showing it as "no holidays" would tell an operator their calendar is clear when it
 * is unknown.
 */
export type PreviewOutcome = "rows" | "empty" | "failed";

export function previewOutcome(
  rows: ReadonlyArray<HolidayRowModel> | undefined,
  failed: boolean,
): PreviewOutcome {
  if (failed) {
    return "failed";
  }

  return rows && rows.length > 0 ? "rows" : "empty";
}
