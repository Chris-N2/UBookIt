import type { TermResolver } from "./resolution-summary.js";
import { roleLabels, type RoleDescriptor } from "./role-label.js";

/** One requirement of a listed service: what it needs, and how many. */
export type RequirementDescriptor = RoleDescriptor & {
  count: number;
};

/**
 * The requirement summary for one row of the services collection view —
 * "2 × room, 1 × therapist (cert-x), 1 × therapist (no required capabilities)".
 *
 * A pure function, separate from the element, for the reason the resolution
 * summary and the alignment report are: this is where the summary's truth claims
 * live. The claim here is that two rows the domain treats as different render
 * differently, and that is a property of the string selection alone.
 *
 * The summary is one table cell, so it stays short wherever it can: capabilities
 * appear only where two roles share a resource type and nothing else tells them
 * apart (design D5). The count is always stated — a service needing two of
 * something is a different service from one needing one, whatever the types.
 */
export function requirementSummary(roles: RequirementDescriptor[], t: TermResolver): string {
  if (roles.length === 0) {
    return t("requirementNone");
  }

  const labels = roleLabels(roles, t);

  return roles.map((role, index) => t("requirementEntry", role.count, labels[index])).join(", ");
}
