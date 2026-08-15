import type { TermResolver } from "./resolution-summary.js";

/**
 * One side of a reported clash, captured with the response that reported it.
 *
 * Held as its own type rather than read from the live form for the same reason
 * the resolution chains are: by the time the answer arrives the form may already
 * describe something else, and a sentence naming a resource and an opening time
 * has to be true of the configuration it was computed for. Everything the report
 * says is read from here.
 */
export type MisalignedRoleSnapshot = {
  resourceType: string;
  displayName: string;

  /** The opening time as the server phrased it, `HH:mm`. */
  windowStart: string;

  granularityMinutes: number;
};

/** Two roles whose start times can never coincide. */
export type AlignmentSnapshot = {
  first: MisalignedRoleSnapshot;
  second: MisalignedRoleSnapshot;
};

/**
 * What the alignment report says, derived entirely from the snapshot.
 *
 * A pure function, separate from the element, because this is where the report's
 * truth claims live — exactly as for the resolution summary, and for the same
 * reason: QA found a false sentence in that phrasing which live verification had
 * missed, because the faulty branch needed a configuration the manual pass did
 * not happen to try.
 *
 * An empty array is silence, and silence is what BOTH "not known" and "no
 * misalignment found" look like. That conflation is deliberate: the absence of
 * this report means only that no permanent misalignment was found, and it must
 * never be phrased — or inferred — as reassurance that the service can be
 * booked. A shared start is necessary for one and nowhere near sufficient.
 *
 * The report names what an editor acts on: the two roles, the two resources, and
 * each one's opening time and step size. The fix is to edit a *resource*, so a
 * report naming only the service would send an editor to the wrong screen.
 */
export function alignmentReport(finding: AlignmentSnapshot | null, t: TermResolver): string[] {
  if (finding === null) {
    return [];
  }

  return [
    t("alignmentNever", finding.first.resourceType, finding.second.resourceType),
    line(finding.first, t),
    line(finding.second, t),
    t("alignmentFix"),
  ];
}

/**
 * One resource's two numbers. Both are named because either can be changed to
 * fix the clash, and neither alone identifies it.
 */
function line(role: MisalignedRoleSnapshot, t: TermResolver): string {
  return t("alignmentWindow", role.displayName, role.windowStart, role.granularityMinutes);
}
