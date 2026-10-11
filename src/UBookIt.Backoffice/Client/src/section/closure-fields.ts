import type { AvailabilityExceptionModel, ResourceClosureModel } from "../api/index.js";

/**
 * The closure UI's decisions, as pure functions so they are tested as claims — the client's
 * established pattern.
 *
 * The one that matters most is {@link isSuperseded}. Whether a site closure overrides a
 * resource's own exception is a PRECEDENCE question, and precedence has exactly one
 * implementation, in the domain. This module reads the server's answer; it never compares a
 * closure list against an exception set, because a second implementation here would be free
 * to disagree with the availability a visitor is actually offered.
 */

/**
 * The closure ids a resource save carries: those the editor has marked as exempt.
 *
 * Full replacement, like the capability set — an exemption left out of this list is an
 * exemption withdrawn — so this is derived from the whole list every time rather than
 * accumulated.
 */
export function optOutIds(closures: ReadonlyArray<ResourceClosureModel>): string[] {
  return closures.filter((closure) => closure.excluded).map((closure) => closure.id);
}

/**
 * Whether the editor states that an exception is currently superseded.
 *
 * Read from the server's flag, and `=== true` deliberately: the member is optional in the
 * contract (it is absent on a request, where it would be a control that looks live and is
 * not), so an undefined value means "not stated" and must not read as true.
 */
export function isSuperseded(exception: Pick<AvailabilityExceptionModel, "superseded">): boolean {
  return exception.superseded === true;
}

/**
 * The ids describing an exception's fieldset, in reading order, for one `aria-describedby`.
 *
 * Built from what is actually rendered: an id naming an element the screen did not render
 * sends assistive technology to nothing, which is its own defect rather than a harmless
 * extra. The superseded statement is the reason this exists — text placed inside a fieldset
 * is visually present and programmatically unrelated to it until something references it.
 */
export function exceptionDescribedByIds(
  index: number,
  { hasGroupErrors, superseded }: { hasGroupErrors: boolean; superseded: boolean },
): string[] {
  const ids: string[] = [];

  if (hasGroupErrors) {
    ids.push("err-exceptions");
  }

  if (superseded) {
    ids.push(supersededId(index));
  }

  return ids;
}

/** The id of one exception's superseded statement. */
export function supersededId(index: number): string {
  return `ex-${index}-superseded`;
}

/**
 * The query the closures list is fetched with.
 *
 * The filter is the SERVER's: the view's default is what the server returns, not a full list
 * with rows hidden here — so a site with twenty years of closures does not fetch all of them
 * to show the next two.
 */
export function listQuery(includePast: boolean): { includePast: boolean } {
  return { includePast };
}

/**
 * Whether the closures view offers the editing controls at all.
 *
 * A viewer who may read but not write is told so instead. The settings verb is never seeded,
 * so on an upgraded site this is the state everybody starts in, including administrators —
 * which is why the explanation is a rendered statement rather than an absence.
 */
export function showsEditingControls(canWrite: boolean): boolean {
  return canWrite;
}

/**
 * The end date a closure or closed period is sent with. A To left empty means the one day From
 * names, so closing a single day asks for one date.
 */
export function rangeEnd(from: string, to: string): string {
  return to === "" ? from : to;
}

/**
 * How a closure's (or a closed period's) dates read in a list: one date for a single day, and
 * "start – end" for a range. A single day is not shown as a range from a date to itself.
 */
export function rangeText(start: string, end: string | undefined | null): string {
  return !end || end === start ? start : `${start} – ${end}`;
}

/** The closure form's inputs, by id. */
export type ClosureControl = "closure-date" | "closure-end-date" | "closure-label";

/**
 * Which input a closure save's failure concerns, read from the failure's `field` — so the
 * failure is associated with that control (`aria-invalid`, and `aria-describedby` naming the
 * error) rather than only announced. Unknown or absent fields concern no single control.
 */
export function closureErrorControl(field: string | null | undefined): ClosureControl | undefined {
  switch (field?.toLowerCase()) {
    case "date":
      return "closure-date";
    case "enddate":
      return "closure-end-date";
    case "label":
      return "closure-label";
    default:
      return undefined;
  }
}

/** The `aria-describedby` of one closure input: its own hint, then the error when it concerns it. */
export function closureDescribedBy(
  control: ClosureControl,
  failed: ClosureControl | undefined,
  hintId?: string,
): string | undefined {
  const ids = [hintId, failed === control ? "closure-error" : undefined].filter((id): id is string => !!id);
  return ids.length > 0 ? ids.join(" ") : undefined;
}

/** A closed period as the editor holds it, before it is sent. */
export interface ClosedPeriodDraft {
  startDate: string;
  endDate: string;
  note: string;
}

/**
 * The closed periods a resource save carries — ALL of them, every time.
 *
 * Always sent, even when the section was not touched. The server reads an ABSENT collection as
 * "leave them as they are", which exists for callers written before closed periods did; this
 * editor knows about them, so it says exactly what it holds and an emptied section clears them.
 */
export function closedPeriodsForSave(
  drafts: ReadonlyArray<ClosedPeriodDraft>,
): { startDate: string; endDate: string; note: string | null }[] {
  return drafts.map((draft) => ({
    startDate: draft.startDate,
    endDate: rangeEnd(draft.startDate, draft.endDate),
    note: draft.note.trim() === "" ? null : draft.note.trim(),
  }));
}

/** Whether any closed period has no start date yet — a state only the editor can create. */
export function closedPeriodMissingStart(drafts: ReadonlyArray<ClosedPeriodDraft>): boolean {
  return drafts.some((draft) => draft.startDate === "");
}
