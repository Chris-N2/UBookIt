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

/**
 * Whether the import belongs on the screen at all.
 *
 * **Two independent conditions, and the absence they produce is indistinguishable.** A user who
 * may not change closures does not get it because importing is changing them; a site whose
 * developer registered no source does not get it because the site does not have the feature. In
 * neither case is anything rendered — no control, and no explanation of one — so an operator is
 * never shown a thing that would fail when pressed.
 *
 * `hasSource` is deliberately `boolean | undefined`: undefined means the question has not been
 * answered yet, and an unanswered question renders nothing rather than briefly flashing a panel
 * that may not belong. **It fails closed**, which is the same choice {@link isSelectable} makes
 * about a state it has never heard of.
 */
export function showsImport(canWrite: boolean, hasSource: boolean | undefined): boolean {
  return canWrite && hasSource === true;
}

/**
 * Whether to ask the server if a source is registered.
 *
 * Asked once, and only of somebody who could act on the answer — which is also what keeps a
 * Configure-only session from calling an endpoint it would be refused. `undefined` is the
 * unanswered state; once answered either way, it is not asked again.
 */
export function shouldAskForSource(canWrite: boolean, hasSource: boolean | undefined): boolean {
  return canWrite && hasSource === undefined;
}

/**
 * What a failed preview means, from the status the server answered with.
 *
 * **A 404 is not a failure of the source — it is the absence of one.** The server answers 404
 * when no source is registered, which can happen to a screen that was opened while one still
 * was: a developer deregistered it, or the site restarted with the composer removed. Reporting
 * that as "the source could not be reached" would send somebody to debug a feed that is not
 * there, and would leave a panel on screen for a feature the site no longer has.
 *
 * So the two are kept apart at exactly the point the rest of this capability keeps them apart.
 * `absent` withdraws the panel, as though the probe had said no in the first place; `failed`
 * says the source was asked and did not answer.
 */
export type PreviewFailure = "absent" | "failed";

export function previewFailure(status: number | undefined): PreviewFailure {
  return status === 404 ? "absent" : "failed";
}
