import type { ResponsibilityAssignmentModel, ResponsibilityPartyModel } from "../api/index.js";

/**
 * The responsibility panel's decisions, kept out of the element so they can be
 * tested as claims rather than rendered output.
 *
 * The load-bearing one is {@link marksFor}: an assignment whose party no longer
 * resolves, or whose user is in a state sending skips, must be SHOWN with its
 * condition — the pickers render their selections from Umbraco's current data,
 * so a deleted party is exactly the one they cannot show, and this list is the
 * only place it remains visible.
 */

/** The party kinds as the API spells them. */
export const USER_KIND = "user";
export const GROUP_KIND = "group";

/** The user states the mail path skips (mirrors the server's state rule). */
const SKIPPED_STATES = ["Disabled", "Invited"] as const;

export type PartyMark = "missing" | "disabled" | "invited";

export interface MarkedParty {
  kind: string;
  key: string;
  /** Null exactly when the party no longer resolves. */
  displayName: string | null;
  mark: PartyMark;
}

/** The keys of one kind, for handing a picker its selection. */
export function selectionOf(parties: ResponsibilityPartyModel[], kind: string): string[] {
  return parties.filter((party) => party.kind === kind).map((party) => party.key);
}

/**
 * The parties that need their condition shown: deleted, disabled, or invited.
 * Everything else renders through the pickers and needs no annotation.
 */
export function marksFor(parties: ResponsibilityPartyModel[]): MarkedParty[] {
  return parties.flatMap((party) => {
    const mark: PartyMark | null = !party.exists
      ? "missing"
      : party.userState !== null &&
          party.userState !== undefined &&
          (SKIPPED_STATES as readonly string[]).includes(party.userState)
        ? (party.userState.toLowerCase() as PartyMark)
        : null;

    return mark === null
      ? []
      : [{ kind: party.kind, key: party.key, displayName: party.displayName ?? null, mark }];
  });
}

/**
 * The PUT body from the two pickers' selections. Wholesale by design — what is
 * saved is what the pickers show — so this is a projection, never a merge with
 * what was loaded.
 */
export function buildAssignments(userKeys: string[], groupKeys: string[]): ResponsibilityAssignmentModel[] {
  return [
    ...userKeys.map((key) => ({ kind: USER_KIND, key })),
    ...groupKeys.map((key) => ({ kind: GROUP_KIND, key })),
  ];
}
