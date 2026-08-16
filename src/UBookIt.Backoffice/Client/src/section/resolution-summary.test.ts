import { describe, expect, it } from "vitest";
import {
  resolutionGroups,
  resolutionLines,
  type ResolutionSnapshot,
  type TermResolver,
} from "./resolution-summary.js";
import type { DurationExclusionModel } from "../api/index.js";

/**
 * The resolution summary's phrasing (services spec, "The requirement row reports
 * how many resources match", and design D5/D6/D7).
 *
 * These assert the KEYS chosen, not the English, so a copy edit does not break
 * them — what has to hold is that every sentence the summary can produce is true
 * of the configuration it describes, and the key selection is where that lives.
 * The one exception is the availability-vocabulary test at the bottom, which
 * reads the real strings because the ban is on the words themselves.
 */

/** Renders `key(arg, arg)` so a test can assert both the key and its arguments. */
const t: TermResolver = (key, ...args) => (args.length === 0 ? key : `${key}(${args.join(",")})`);

function snapshot(overrides: Partial<ResolutionSnapshot> = {}): ResolutionSnapshot {
  return {
    resourceType: "room",
    requiredCapabilities: [],
    ofType: 10,
    withCapabilities: 10,
    canProvide: 10,
    exclusions: [],
    ...overrides,
  };
}

function excluded(
  displayName: string,
  reason: string = "resource-maximum",
  boundMinutes = 120,
): DurationExclusionModel {
  return { id: `id-${displayName}`, displayName, reason, boundMinutes };
}

describe("not known", () => {
  it("says nothing at all when there is no snapshot", () => {
    // Design D7: silence, never a chain of zeros. Zero is the answer that tells
    // an editor their configuration is broken, so reporting it because a request
    // failed sends them to fix something that is correct.
    expect(resolutionLines(null, t)).toEqual([]);
  });
});

describe("the stage that emptied the pool", () => {
  it("attributes an unused type to the type stage and stops there", () => {
    const lines = resolutionLines(
      snapshot({ resourceType: "rooom", requiredCapabilities: ["projector"], ofType: 0, withCapabilities: 0, canProvide: 0 }),
      t,
    );

    // The ⑧ defect: this configuration also requires capabilities, and must NOT
    // be reported as a capability problem.
    expect(lines).toEqual(["resolutionTypeNone(rooom)"]);
    expect(lines.join(" ")).not.toContain("Capabilities");
  });

  it("attributes an over-narrow capability set to the capability stage and stops there", () => {
    const lines = resolutionLines(
      snapshot({ requiredCapabilities: ["projector"], withCapabilities: 0, canProvide: 0 }),
      t,
    );

    expect(lines).toEqual(["resolutionType(10,room)", "resolutionCapabilitiesNone"]);
    // Continuing to the duration stage would print "None of those…" about a
    // stage that had nothing to filter.
    expect(lines.join(" ")).not.toContain("resolutionDuration");
  });

  it("attributes a duration nothing can provide to the duration stage, and names the exclusions", () => {
    const lines = resolutionLines(
      snapshot({
        requiredCapabilities: ["projector"],
        withCapabilities: 3,
        canProvide: 0,
        exclusions: [excluded("Red Room"), excluded("Blue Room")],
      }),
      t,
    );

    expect(lines).toEqual([
      "resolutionType(10,room)",
      "resolutionCapabilities(3)",
      "resolutionDurationNone",
      "resolutionExcluded(resolutionExcludedMaximum(Red Room,120), resolutionExcludedMaximum(Blue Room,120))",
    ]);
  });
});

describe("the healthy case", () => {
  it("collapses to a single line when nothing was excluded anywhere", () => {
    expect(resolutionLines(snapshot(), t)).toEqual(["resolutionHealthy(10)"]);
  });

  it("uses the singular form for one resource", () => {
    expect(resolutionLines(snapshot({ ofType: 1, withCapabilities: 1, canProvide: 1 }), t)).toEqual([
      "resolutionHealthyOne",
    ]);
  });
});

describe("capabilities that were never named", () => {
  // The QA MAJOR. With no capability required the stage filtered nothing by
  // construction, so reporting it would describe the configuration in terms the
  // editor never entered. The healthy-collapse branch hid this in the all-equal
  // case, which is why live verification missed it.
  it("omits the capability line entirely when none are required", () => {
    const lines = resolutionLines(
      snapshot({ requiredCapabilities: [], canProvide: 8, exclusions: [excluded("Red Room")] }),
      t,
    );

    expect(lines).toEqual([
      "resolutionType(10,room)",
      "resolutionDuration(8)",
      "resolutionExcluded(resolutionExcludedMaximum(Red Room,120))",
    ]);
    expect(lines.join(" ")).not.toContain("resolutionCapabilities");
  });

  it("emits no exclusion line when capabilities narrow the pool but the duration excludes nothing", () => {
    // The commonest narrowed configuration — 10 → 3 → 3 — and the one the
    // suite was missing: every other non-healthy case either returns early or
    // supplies exclusions, so the `exclusions.length > 0` guard was never
    // reached with an empty list. Without it, relaxing that guard to `>= 0`
    // leaves the suite green while the summary emits a dangling
    // "Excluded by the length: " with nothing after it.
    const lines = resolutionLines(
      snapshot({ requiredCapabilities: ["projector"], withCapabilities: 3, canProvide: 3, exclusions: [] }),
      t,
    );

    expect(lines).toEqual([
      "resolutionType(10,room)",
      "resolutionCapabilities(3)",
      "resolutionDuration(3)",
    ]);
    expect(lines.join(" ")).not.toContain("resolutionExcluded");
  });

  it("reports the capability line when they ARE required, on the same counts", () => {
    // Same numbers, different configuration: the phrasing must follow what the
    // editor typed, not what the data happens to look like. This is the pair
    // that makes the test above non-vacuous.
    const lines = resolutionLines(
      snapshot({ requiredCapabilities: ["projector"], canProvide: 8, exclusions: [excluded("Red Room")] }),
      t,
    );

    expect(lines).toContain("resolutionCapabilities(10)");
  });
});

describe("singular and plural", () => {
  it("uses the singular type form for one resource of the type", () => {
    const lines = resolutionLines(
      snapshot({ requiredCapabilities: ["projector"], ofType: 1, withCapabilities: 1, canProvide: 0, exclusions: [excluded("Red Room")] }),
      t,
    );

    expect(lines[0]).toBe("resolutionTypeOne(room)");
    expect(lines[1]).toBe("resolutionCapabilitiesOne");
  });

  it("uses the singular duration form for one surviving resource", () => {
    const lines = resolutionLines(
      snapshot({ requiredCapabilities: ["projector"], withCapabilities: 3, canProvide: 1, exclusions: [excluded("Red Room")] }),
      t,
    );

    expect(lines[2]).toBe("resolutionDurationOne");
  });
});

describe("exclusion reasons", () => {
  it.each([
    ["resource-maximum", "resolutionExcludedMaximum"],
    ["resource-minimum", "resolutionExcludedMinimum"],
    ["granularity", "resolutionExcludedGranularity"],
  ])("maps %s to %s", (reason, expected) => {
    const lines = resolutionLines(
      snapshot({ canProvide: 9, exclusions: [excluded("Red Room", reason, 30)] }),
      t,
    );

    expect(lines[lines.length - 1]).toBe(`resolutionExcluded(${expected}(Red Room,30))`);
  });

  it("falls back to the maximum phrasing for an unrecognised reason", () => {
    // A reason the server adds later must not render as `undefined`.
    const lines = resolutionLines(
      snapshot({ canProvide: 9, exclusions: [excluded("Red Room", "something-new", 45)] }),
      t,
    );

    expect(lines[lines.length - 1]).toBe("resolutionExcluded(resolutionExcludedMaximum(Red Room,45))");
  });

  it("names at most three and counts the rest", () => {
    const lines = resolutionLines(
      snapshot({
        canProvide: 5,
        exclusions: ["A", "B", "C", "D", "E"].map((n) => excluded(n)),
      }),
      t,
    );

    const last = lines[lines.length - 1];
    expect(last).toContain("(A,120)");
    expect(last).toContain("(C,120)");
    expect(last).not.toContain("(D,120)");
    expect(last).toContain("resolutionExcludedMore(2)");
  });
});

describe("a chain per role", () => {
  it("says nothing at all when the chains are not known", () => {
    expect(resolutionGroups(null, t)).toEqual([]);
  });

  it("groups each role's lines under its own resource type", () => {
    const groups = resolutionGroups(
      [
        snapshot({ resourceType: "room", requiredCapabilities: ["projector"], withCapabilities: 3, canProvide: 3 }),
        snapshot({ resourceType: "therapist", ofType: 2, withCapabilities: 2, canProvide: 2 }),
      ],
      t,
    );

    expect(groups).toEqual([
      {
        resourceType: "room",
        label: "room",
        lines: ["resolutionType(10,room)", "resolutionCapabilities(3)", "resolutionDuration(3)"],
      },
      { resourceType: "therapist", label: "therapist", lines: ["resolutionHealthy(2)"] },
    ]);
  });

  it("labels two roles of one type by what distinguishes them", () => {
    // Two headings reading "therapist" present two overlapping pools as two
    // independent ones — the misreading the sufficiency report beside them
    // exists to correct, and this is the surface it lands on (design D6).
    const groups = resolutionGroups(
      [
        snapshot({ resourceType: "therapist", requiredCapabilities: ["cert-x"] }),
        snapshot({ resourceType: "therapist", requiredCapabilities: [] }),
      ],
      t,
    );

    expect(groups.map((g) => g.label)).toEqual([
      "roleLabelCapabilities(therapist,cert-x)",
      "roleLabelNoCapabilities(therapist)",
    ]);
  });

  it("leaves the label bare where no two roles share a type", () => {
    // The common case stays short: a `room` and a `therapist` are unambiguous
    // already, and stating capabilities everywhere would make every service
    // noisier in order to disambiguate the few (design D5).
    const groups = resolutionGroups(
      [
        snapshot({ resourceType: "room", requiredCapabilities: ["projector"] }),
        snapshot({ resourceType: "therapist", requiredCapabilities: ["cert-x"] }),
      ],
      t,
    );

    expect(groups.map((g) => g.label)).toEqual(["room", "therapist"]);
  });

  it("describes a role requiring no capabilities by its type alone, beside one that does", () => {
    // The ⑧a MAJOR, now with a second role next to it: the capability line must
    // follow what each role required, not what the other role required or what
    // the numbers happen to look like.
    const groups = resolutionGroups(
      [
        snapshot({ resourceType: "room", requiredCapabilities: ["projector"], withCapabilities: 3, canProvide: 3 }),
        snapshot({
          resourceType: "therapist",
          requiredCapabilities: [],
          ofType: 4,
          withCapabilities: 4,
          canProvide: 2,
          exclusions: [excluded("Mary")],
        }),
      ],
      t,
    );

    expect(groups[1].lines.join(" ")).not.toContain("resolutionCapabilities");
    expect(groups[1].lines).toEqual([
      "resolutionType(4,therapist)",
      "resolutionDuration(2)",
      "resolutionExcluded(resolutionExcludedMaximum(Mary,120))",
    ]);

    // And the role that DID require capabilities still reports them, so the
    // omission above is about that role's configuration rather than a blanket
    // rule.
    expect(groups[0].lines).toContain("resolutionCapabilities(3)");
  });

  it("attributes a type matching nothing to that role alone", () => {
    const groups = resolutionGroups(
      [
        snapshot({ resourceType: "rooom", requiredCapabilities: ["projector"], ofType: 0, withCapabilities: 0, canProvide: 0 }),
        snapshot({ resourceType: "therapist", ofType: 2, withCapabilities: 2, canProvide: 2 }),
      ],
      t,
    );

    expect(groups[0].lines).toEqual(["resolutionTypeNone(rooom)"]);
    expect(groups[1].lines).toEqual(["resolutionHealthy(2)"]);
  });

  it("keeps two roles of the same type separate rather than merging them", () => {
    // A configuration that cannot be saved but can be previewed. Each chain is
    // reported as supplied, so an editor can see what each row resolves to while
    // correcting the duplicate.
    const groups = resolutionGroups(
      [
        snapshot({ resourceType: "room", requiredCapabilities: ["projector"], withCapabilities: 3, canProvide: 3 }),
        snapshot({ resourceType: "room" }),
      ],
      t,
    );

    expect(groups.map((g) => g.resourceType)).toEqual(["room", "room"]);
    expect(groups[0].lines).not.toEqual(groups[1].lines);
  });

  it("preserves the order the roles were supplied in", () => {
    const groups = resolutionGroups(
      [snapshot({ resourceType: "therapist" }), snapshot({ resourceType: "room" })],
      t,
    );

    expect(groups.map((g) => g.resourceType)).toEqual(["therapist", "room"]);
  });
});

describe("eligibility is not availability (design D5)", () => {
  it("never emits the availability vocabulary, in any reachable state", async () => {
    // Reads the REAL strings, because the ban is on the words themselves. If a
    // future copy edit reintroduces "bookable", this fails.
    //
    // Covers the alignment report and the pool-sufficiency report as well as the
    // chains. Both say something the chains do not — whether two roles' start
    // times can ever coincide, and whether the roles can be filled at once — but
    // both are under the same ban: each may state that they never can, and
    // neither may state that a service is available, free or bookable. Neither
    // absence may read as reassurance either, which is why there is no "these
    // roles align" or "the pool is sufficient" string for this test to exempt.
    //
    // One constraint stated in one place over all three, deliberately: the
    // easiest way to get this subtly wrong is to add a fourth surface with its
    // own vocabulary rule.
    const { default: terms } = await import("../localization/en-us.js");
    const services = (terms as Record<string, Record<string, string>>).ubookitServices;

    const forbidden = /\b(available|availability|free|bookable)\b/i;

    const covered = Object.keys(services).filter(
      (key) =>
        key.startsWith("resolution") ||
        key.startsWith("alignment") ||
        key.startsWith("sufficiency") ||
        key.startsWith("roleLabel"),
    );

    const offenders = covered
      .map((key) => [key, services[key]] as const)
      .filter(([, value]) => typeof value === "string" && forbidden.test(value));

    expect(offenders).toEqual([]);

    // The filters have to actually match: a prefix typo would leave this test
    // green while covering nothing.
    expect(covered).toContain("alignmentNever");
    expect(covered).toContain("sufficiencyShort");
    expect(covered).toContain("roleLabelNoCapabilities");
  });
});
