import type { DurationExclusionModel } from "../api/index.js";
import { chainLabels } from "./role-label.js";

/**
 * A resolution chain and the configuration it was computed for, captured
 * together.
 *
 * The pairing is the point. ⑧'s readout derived its phrasing from live form
 * state while its count was a snapshot from the previous request, so removing a
 * capability could briefly assert a sentence that was false for the number
 * beside it — into a `role="status"` region, where a screen reader announces the
 * false sentence and then the correction. Everything the summary says is read
 * from here, never from the live fields.
 */
export type ResolutionSnapshot = {
  resourceType: string;

  /**
   * The requirement row this chain describes, 1-based as its legend shows it.
   *
   * Captured with the request rather than inferred from this chain's position in
   * the response, because the two are not the same number: a row with no
   * resource type yet is omitted from the request, so every chain below it sits
   * one place earlier than its row. Inferring it headed chains with the wrong
   * requirement number for as long as such a row existed.
   */
  rowNumber: number;

  /**
   * The capabilities the configuration named for this role, as the response
   * echoed them.
   *
   * Snapshotted rather than inferred from `ofType === withCapabilities`: those
   * are equal both when no capability was required and when every resource
   * happens to carry the ones that were, and the two cases must be worded
   * differently. Inferring would make the summary's phrasing depend on the data
   * rather than on what the editor typed.
   *
   * Carried as the list rather than as a bare "any?" flag because it is also what
   * tells two roles of one resource type apart, and a second copy of the same
   * fact could drift from this one.
   */
  requiredCapabilities: string[];

  ofType: number;
  withCapabilities: number;
  canProvide: number;
  exclusions: DurationExclusionModel[];
};

/** Resolves a localization key within the `ubookitServices` area. */
export type TermResolver = (key: string, ...args: (string | number)[]) => string;

/**
 * One role's chain, ready to render: the type it describes and what the summary
 * says about it.
 *
 * Grouped per role rather than flattened into one list of lines, and labelled
 * with the type, because the roles constrain different pools. A combined count
 * would describe no filter that resolution applies, and an unlabelled list of
 * lines would leave an editor guessing which role each line belongs to.
 */
export type RoleResolutionGroup = {
  resourceType: string;

  /**
   * How this role is named above its lines.
   *
   * Not simply the resource type. Two roles may name one type, and two groups
   * headed "therapist" read as two independent pools — which is exactly the
   * misreading the sufficiency report beside them exists to correct, and this is
   * the surface it lands on. Where two roles share a type, each is headed by the
   * requirement number its row carries (design D6).
   *
   * NOT by required capabilities, which is how the collection view disambiguates
   * the same pair. This requirement forbids the report referring to capabilities
   * for a role that names none, and "therapist (no required capabilities)"
   * describes the configuration in terms the editor never entered. The row number
   * says the same thing about a surface that has rows.
   */
  label: string;

  lines: string[];
};

/** How many excluded resources are named before the rest are counted instead. */
export const MAX_NAMED_EXCLUSIONS = 3;

/**
 * The excluded resources with the bound that excluded each — the number the
 * editor has to change. Capped, because a wide pool with a low ceiling would
 * otherwise produce a list longer than the form.
 */
function excludedNames(exclusions: DurationExclusionModel[], t: TermResolver): string {
  const named = exclusions.slice(0, MAX_NAMED_EXCLUSIONS).map((exclusion) => {
    const key =
      exclusion.reason === "resource-minimum"
        ? "resolutionExcludedMinimum"
        : exclusion.reason === "granularity"
          ? "resolutionExcludedGranularity"
          : "resolutionExcludedMaximum";

    return t(key, exclusion.displayName, exclusion.boundMinutes);
  });

  const remaining = exclusions.length - named.length;
  if (remaining > 0) {
    named.push(t("resolutionExcludedMore", remaining));
  }

  return named.join(", ");
}

/**
 * What the resolution summary says, derived entirely from the snapshot — never
 * from the live form (design D7).
 *
 * A pure function over the snapshot, separate from the element, because this is
 * where the summary's truth claims actually live: every sentence it can produce
 * has to be true of the configuration in `chain`, and that is a property of the
 * string selection alone. QA found a false sentence here that live verification
 * had missed, precisely because the faulty branch needed a configuration the
 * manual pass did not happen to try.
 *
 * An empty array is silence, and silence is what "not known" looks like: no
 * resource type entered yet, a fixed duration with no length, or a request that
 * failed. Rendering a chain of zeros instead would tell an editor their
 * configuration resolves to nothing, which is the one thing a failed request
 * does not know.
 *
 * The chain stops at the stage that emptied the pool. Continuing past it would
 * print "None of those…" about a stage that had nothing to filter, which is how
 * ⑧ managed to blame the capabilities for a mistyped type key.
 */
export function resolutionGroups(
  chains: ResolutionSnapshot[] | null,
  t: TermResolver,
): RoleResolutionGroup[] {
  if (chains === null) {
    return [];
  }

  // Each role is described from its own chain alone, and each chain stays
  // independently true of the role it describes: it states what that role
  // resolves to, not what remains once another role has taken someone.
  //
  // The pools are NOT in general disjoint — two roles may name one resource type
  // — so a resource can be counted in more than one chain. That is why there is
  // no combined number here: it would double-count, and it would describe no
  // filter that resolution applies. The joint claim is a different question
  // altogether, about assignment rather than filters, and it belongs to the
  // sufficiency report beside these (design D6).
  //
  // What the labels carry is the other half of that: where two roles share a
  // type, each heading carries the requirement number of the row it describes,
  // so two groups cannot read as one pool counted twice — and so the heading
  // points at the control that changes it.
  const labels = chainLabels(chains, t);

  return chains.map((chain, index) => ({
    resourceType: chain.resourceType,
    label: labels[index],
    lines: resolutionLines(chain, t),
  }));
}

export function resolutionLines(chain: ResolutionSnapshot | null, t: TermResolver): string[] {
  if (chain === null) {
    return [];
  }

  if (chain.ofType === 0) {
    return [t("resolutionTypeNone", chain.resourceType)];
  }

  // Nothing was excluded anywhere. Three lines carrying one number say less
  // than one line does, so the healthy case collapses.
  if (chain.ofType === chain.canProvide) {
    return [
      chain.canProvide === 1
        ? t("resolutionHealthyOne")
        : t("resolutionHealthy", chain.canProvide),
    ];
  }

  const lines = [
    chain.ofType === 1
      ? t("resolutionTypeOne", chain.resourceType)
      : t("resolutionType", chain.ofType, chain.resourceType),
  ];

  // The capability stage is reported only when the configuration actually
  // requires capabilities. With none named it filtered nothing by construction —
  // an empty requirement matches every resource of the type — so its count is
  // already derivable from the line above, and stating it would refer to
  // capabilities the editor never entered. ⑧ carried separate "…have this type"
  // strings for this case; omitting the line says the same thing without
  // needing a second phrasing of every count.
  if (chain.requiredCapabilities.length > 0) {
    if (chain.withCapabilities === 0) {
      lines.push(t("resolutionCapabilitiesNone"));
      return lines;
    }

    lines.push(
      chain.withCapabilities === 1
        ? t("resolutionCapabilitiesOne")
        : t("resolutionCapabilities", chain.withCapabilities),
    );
  }

  lines.push(
    chain.canProvide === 0
      ? t("resolutionDurationNone")
      : chain.canProvide === 1
        ? t("resolutionDurationOne")
        : t("resolutionDuration", chain.canProvide),
  );

  if (chain.exclusions.length > 0) {
    lines.push(t("resolutionExcluded", excludedNames(chain.exclusions, t)));
  }

  return lines;
}
