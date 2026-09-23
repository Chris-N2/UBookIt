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
export const SETTINGS_VERB = "UBookIt.Settings";

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

/**
 * The site's own settings.
 *
 * Deliberately satisfied by NOTHING else — not by Configure, which is the verb for adding a
 * meeting room, while these settings reach the site's retention posture, its anonymous API
 * exposure and where bookers' details are emailed. The server's policy takes the same single
 * verb, so the two cannot disagree about it.
 */
export function canManageSettings(permissions: Array<string> | undefined): boolean {
  return hasAnyVerb(permissions, SETTINGS_VERB);
}

/**
 * Reading the site's closure list.
 *
 * Satisfied by EITHER verb, mirroring the server's `UBookItClosuresRead` policy — and, like
 * it, this is not an implication between them: each reaches this read on its own account and
 * gains nothing else the other holds. An operator editing a resource must see what it is
 * inheriting in order to exempt it, and whoever decides the site's closures must be able to
 * see them.
 */
export function canReadClosures(permissions: Array<string> | undefined): boolean {
  return hasAnyVerb(permissions, CONFIGURE_VERB, SETTINGS_VERB);
}

/**
 * Changing the closure list — the settings verb alone, exactly as writing a setting is.
 *
 * One entry shuts every resource the site has, including those created after it, so a grant
 * meaning "may add a meeting room" does not carry it. Reading is deliberately wider: see
 * `canReadClosures`.
 */
export function canManageClosures(permissions: Array<string> | undefined): boolean {
  return canManageSettings(permissions);
}
