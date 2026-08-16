import type { TermResolver } from "./resolution-summary.js";
import { findingLabels, type PositionedRole } from "./role-label.js";

/** One requirement a shortfall names, captured with the response that named it. */
export type ShortfallRoleSnapshot = PositionedRole & {
  count: number;
};

/**
 * The roles that cannot be filled at once, captured with the response that
 * reported them.
 *
 * Held as its own type rather than read from the live form, for the reason the
 * chains and the alignment report are: by the time the answer arrives the form
 * may already describe something else, and a sentence stating two numbers has to
 * be true of the configuration those numbers were computed for.
 */
export type ShortfallSnapshot = {
  roles: ShortfallRoleSnapshot[];

  /** How many distinct resources those roles need between them. */
  required: number;

  /** How many resources are eligible for any of them. Always below `required`. */
  eligible: number;
};

/**
 * What the pool-sufficiency report says, derived entirely from the snapshot.
 *
 * An empty array is silence, and silence is what BOTH "not known" and "the roles
 * can be filled together" look like. That conflation is deliberate and is the
 * one-directional stance: the absence of this report means only that no
 * structural impossibility was found. It must never be phrased — or inferred —
 * as reassurance that the service can be booked, because nothing here evaluates
 * opening hours, lead time, booking horizon, or the booking calendar.
 *
 * Nothing it can say uses the words *available*, *free* or *bookable*, and the
 * only claim it makes is that the resources that exist cannot fill these
 * requirements at once (⑧a design D5).
 *
 * It reports required against eligible and names the roles, because those are
 * the two different repairs: a count that is too high is corrected on a
 * requirement row, and a pool that is too small is corrected by adding or
 * re-configuring a resource. A report carrying only one of the numbers would send
 * an editor to guess which.
 */
export function sufficiencyReport(finding: ShortfallSnapshot | null, t: TermResolver): string[] {
  if (finding === null) {
    return [];
  }

  // Named by row, exactly as the chains above are — and unconditionally, because
  // this list is a SUBSET of the configuration. Deciding by whether two named
  // roles share a type would ask the wrong question: a service with two
  // `therapist` rows can produce a finding naming one of them, which then reads
  // as unambiguous while leaving the editor unable to tell which row is short.
  const labels = findingLabels(finding.roles, t);

  return [
    headline(finding, t),
    ...finding.roles.map((role, index) => t("sufficiencyRole", labels[index], role.count)),
    t("sufficiencyFix"),
  ];
}

/**
 * The claim itself. Zero eligible gets its own phrasing rather than "0 resources
 * are eligible", which reads as the "not known" zero the chains are careful never
 * to print — here it is a genuine finding, and saying so in words keeps the two
 * apart.
 */
function headline(finding: ShortfallSnapshot, t: TermResolver): string {
  if (finding.eligible === 0) {
    return t("sufficiencyNoneEligible", finding.required);
  }

  return finding.eligible === 1
    ? t("sufficiencyOneEligible", finding.required)
    : t("sufficiencyShort", finding.required, finding.eligible);
}
