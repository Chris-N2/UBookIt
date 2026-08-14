import type { DurationExclusionModel } from "../api/index.js";

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
   * Whether the configuration named any required capability.
   *
   * Snapshotted rather than inferred from `ofType === withCapabilities`: those
   * are equal both when no capability was required and when every resource
   * happens to carry the ones that were, and the two cases must be worded
   * differently. Inferring would make the summary's phrasing depend on the data
   * rather than on what the editor typed.
   */
  requiresCapabilities: boolean;

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

  // Each role is described from its own chain alone. Because every role of a
  // saveable service names a distinct resource type, the pools are disjoint and
  // each chain stays independently true: no role can consume a resource another
  // role's chain counted.
  return chains.map((chain) => ({
    resourceType: chain.resourceType,
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
  if (chain.requiresCapabilities) {
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
