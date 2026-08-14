import { describe, expect, it } from "vitest";
import { resolutionLines, type ResolutionSnapshot, type TermResolver } from "./resolution-summary.js";
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
    requiresCapabilities: false,
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
      snapshot({ resourceType: "rooom", requiresCapabilities: true, ofType: 0, withCapabilities: 0, canProvide: 0 }),
      t,
    );

    // The ⑧ defect: this configuration also requires capabilities, and must NOT
    // be reported as a capability problem.
    expect(lines).toEqual(["resolutionTypeNone(rooom)"]);
    expect(lines.join(" ")).not.toContain("Capabilities");
  });

  it("attributes an over-narrow capability set to the capability stage and stops there", () => {
    const lines = resolutionLines(
      snapshot({ requiresCapabilities: true, withCapabilities: 0, canProvide: 0 }),
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
        requiresCapabilities: true,
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
      snapshot({ requiresCapabilities: false, canProvide: 8, exclusions: [excluded("Red Room")] }),
      t,
    );

    expect(lines).toEqual([
      "resolutionType(10,room)",
      "resolutionDuration(8)",
      "resolutionExcluded(resolutionExcludedMaximum(Red Room,120))",
    ]);
    expect(lines.join(" ")).not.toContain("resolutionCapabilities");
  });

  it("reports the capability line when they ARE required, on the same counts", () => {
    // Same numbers, different configuration: the phrasing must follow what the
    // editor typed, not what the data happens to look like. This is the pair
    // that makes the test above non-vacuous.
    const lines = resolutionLines(
      snapshot({ requiresCapabilities: true, canProvide: 8, exclusions: [excluded("Red Room")] }),
      t,
    );

    expect(lines).toContain("resolutionCapabilities(10)");
  });
});

describe("singular and plural", () => {
  it("uses the singular type form for one resource of the type", () => {
    const lines = resolutionLines(
      snapshot({ requiresCapabilities: true, ofType: 1, withCapabilities: 1, canProvide: 0, exclusions: [excluded("Red Room")] }),
      t,
    );

    expect(lines[0]).toBe("resolutionTypeOne(room)");
    expect(lines[1]).toBe("resolutionCapabilitiesOne");
  });

  it("uses the singular duration form for one surviving resource", () => {
    const lines = resolutionLines(
      snapshot({ requiresCapabilities: true, withCapabilities: 3, canProvide: 1, exclusions: [excluded("Red Room")] }),
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

describe("eligibility is not availability (design D5)", () => {
  it("never emits the availability vocabulary, in any reachable state", async () => {
    // Reads the REAL strings, because the ban is on the words themselves. If a
    // future copy edit reintroduces "bookable", this fails.
    const { default: terms } = await import("../localization/en-us.js");
    const services = (terms as Record<string, Record<string, string>>).ubookitServices;

    const forbidden = /\b(available|availability|free|bookable)\b/i;

    const offenders = Object.entries(services)
      .filter(([key]) => key.startsWith("resolution"))
      .filter(([, value]) => typeof value === "string" && forbidden.test(value));

    expect(offenders).toEqual([]);
  });
});
