import type { TermResolver } from "./resolution-summary.js";

/**
 * The parts of a role that name it on screen.
 *
 * Structurally typed rather than tied to one API model, because three surfaces
 * label roles — the collection view's requirement summary, the resolution
 * chains, and the pool-sufficiency report — and each receives the role from a
 * different response. What they must agree on is the *rule*, not the type.
 */
export type RoleDescriptor = {
  resourceType: string;
  requiredCapabilities: string[];
};

/**
 * A role together with the requirement row it came from, 1-based as the fieldset
 * legends show it.
 *
 * Only the editor can supply this — it is the surface that has rows — which is
 * also why the collection view labels by capabilities instead.
 */
export type PositionedRole = RoleDescriptor & {
  rowNumber: number;
};

/**
 * For each role, whether another role of the same configuration names the same
 * resource type.
 *
 * This is the whole of design D5's condition. Two roles of one type are legal
 * exactly when their required capabilities differ, so a summary reporting only
 * type and count renders a configuration the domain accepts identically to one
 * it rejects — and the rejection's own message tells the editor to use a count
 * instead, which a reader of that summary has no way to understand.
 *
 * Where no two roles share a type the capabilities are left out, deliberately.
 * A `room` and a `therapist` need no capability text to be unambiguous, and
 * stating it everywhere would make every service noisier in order to disambiguate
 * the few.
 */
export function sharedTypes(roles: RoleDescriptor[]): boolean[] {
  const counts = new Map<string, number>();

  for (const role of roles) {
    counts.set(role.resourceType, (counts.get(role.resourceType) ?? 0) + 1);
  }

  return roles.map((role) => (counts.get(role.resourceType) ?? 0) > 1);
}

/**
 * How one role is named, given whether it has to be told apart from a sibling.
 *
 * A role sharing its type with another is named by what distinguishes it, and
 * "requires nothing" is one of those answers — it is precisely what makes the
 * pair legal, so it is stated rather than left as an empty parenthesis.
 */
export function roleLabel(role: RoleDescriptor, shared: boolean, t: TermResolver): string {
  if (!shared) {
    return role.resourceType;
  }

  return role.requiredCapabilities.length === 0
    ? t("roleLabelNoCapabilities", role.resourceType)
    : t("roleLabelCapabilities", role.resourceType, role.requiredCapabilities.join(", "));
}

/**
 * Every role a *finding* names, always headed by its row.
 *
 * Unconditionally, where {@link chainLabels} decides by whether two roles share a
 * type — and the difference matters because a finding is a **subset** of the
 * configuration. Judging "do two roles share a type" over the subset asks the
 * wrong question: a configuration with two `therapist` rows can produce a finding
 * naming only one of them, which then looks unambiguous and is not. The editor
 * would be told a `therapist` row is short without being told which.
 *
 * The brevity argument that shapes the other two rules does not apply here
 * either. A chain heads every role of every configuration and the collection view
 * a cell for every service; this renders only when something is wrong, and when
 * something is wrong the row is the whole point.
 */
export function findingLabels(roles: PositionedRole[], t: TermResolver): string[] {
  return roles.map((role) => t("roleLabelOrdinal", role.rowNumber, role.resourceType));
}

/** Every role of a configuration, labelled under the same rule. */
export function roleLabels(roles: RoleDescriptor[], t: TermResolver): string[] {
  const shared = sharedTypes(roles);

  return roles.map((role, index) => roleLabel(role, shared[index], t));
}

/**
 * How the resolution chains head each role — by **row**, never by capabilities.
 *
 * A different rule from {@link roleLabels}, and deliberately so. The chain
 * readout is governed by a requirement that says in as many words: "The report
 * SHALL NOT refer to required capabilities for a role that names none." A
 * heading of "therapist (no required capabilities)" describes the configuration
 * in terms the editor never entered, which is exactly the ⑧a defect that
 * sentence was written to prevent — so the chains disambiguate two same-type
 * roles by the requirement number their rows already carry instead.
 *
 * The ordinal is also the better answer here, not merely the permitted one: the
 * fix for whatever a chain reports is on that row, and the row is legended
 * "Requirement N" a few inches below. The collection view cannot use it, because
 * it has no rows to point at — which is why the two surfaces label differently
 * and why the rule lives in two functions rather than one with a flag.
 */
export function chainLabels(roles: PositionedRole[], t: TermResolver): string[] {
  const shared = sharedTypes(roles);

  // `role.rowNumber`, never the array index. The two differ whenever a row above
  // was omitted from the request for having no resource type yet, and an index
  // would then head a chain with the number of a different row — the fault QA
  // found in the first attempt at this. The number is computed once, where the
  // request is built, and carried.
  return roles.map((role, index) =>
    shared[index] ? t("roleLabelOrdinal", role.rowNumber, role.resourceType) : role.resourceType,
  );
}
