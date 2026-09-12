/**
 * The permission verbs, exactly as the server's `Constants.Verbs` spells them — one
 * vocabulary, and a server-side guard fails when this file and those constants disagree,
 * so a typo cannot split the system into two verb sets.
 *
 * The decisions live here as pure functions so they are tested as claims (the client's
 * established pattern): which verbs satisfy which capability, including the one
 * implication — Manage implies Read — which mirrors the server's authorization rule and,
 * like it, never requires a group's stored verbs to restate it.
 */

export const BOOKINGS_READ_VERB = "UBookIt.Bookings.Read";
export const BOOKINGS_MANAGE_VERB = "UBookIt.Bookings.Manage";
export const CONFIGURE_VERB = "UBookIt.Configure";

/** Whether the fallback-permission set contains any of the given verbs. */
export function hasAnyVerb(permissions: Array<string> | undefined, ...anyOf: Array<string>): boolean {
  return permissions !== undefined && anyOf.some((verb) => permissions.includes(verb));
}

/** The bookings list and its reads. Satisfied by Manage too — managing what you cannot see is incoherent. */
export function canReadBookings(permissions: Array<string> | undefined): boolean {
  return hasAnyVerb(permissions, BOOKINGS_READ_VERB, BOOKINGS_MANAGE_VERB);
}

/** Cancel, confirm, decline. */
export function canManageBookings(permissions: Array<string> | undefined): boolean {
  return hasAnyVerb(permissions, BOOKINGS_MANAGE_VERB);
}

/** Resources, services, and responsibility assignment. */
export function canConfigure(permissions: Array<string> | undefined): boolean {
  return hasAnyVerb(permissions, CONFIGURE_VERB);
}
