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
