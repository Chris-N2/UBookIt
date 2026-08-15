import { describe, expect, it } from "vitest";
import {
  alignmentReport,
  type AlignmentSnapshot,
  type MisalignedRoleSnapshot,
} from "./alignment-report.js";
import type { TermResolver } from "./resolution-summary.js";

/**
 * The alignment report's phrasing (services spec, "The editor reports roles
 * whose start times can never coincide").
 *
 * As with the resolution summary, these assert the KEYS and their arguments
 * rather than the English, so a copy edit does not break them — what has to hold
 * is that every sentence the report can produce is true of the finding it
 * describes. The vocabulary test at the bottom is the exception: it reads the
 * real strings, because the ban is on the words themselves.
 */

const t: TermResolver = (key, ...args) => (args.length === 0 ? key : `${key}(${args.join(",")})`);

function role(overrides: Partial<MisalignedRoleSnapshot> = {}): MisalignedRoleSnapshot {
  return {
    resourceType: "room",
    displayName: "Red Room",
    windowStart: "09:00",
    granularityMinutes: 30,
    ...overrides,
  };
}

function finding(overrides: Partial<AlignmentSnapshot> = {}): AlignmentSnapshot {
  return {
    first: role(),
    second: role({
      resourceType: "therapist",
      displayName: "Mary",
      windowStart: "09:15",
      granularityMinutes: 20,
    }),
    ...overrides,
  };
}

describe("absence", () => {
  it("says nothing at all when there is no finding", () => {
    // The whole one-directional claim, in one assertion: no finding produces no
    // sentence — never "these roles align", never "this service can be booked".
    // Silence is also what a failed request looks like, and the two must be
    // indistinguishable, because neither knows that the service works.
    expect(alignmentReport(null, t)).toEqual([]);
  });
});

describe("presence", () => {
  it("names both roles, both resources, and each one's opening time and step", () => {
    expect(alignmentReport(finding(), t)).toEqual([
      "alignmentNever(room,therapist)",
      "alignmentWindow(Red Room,09:00,30)",
      "alignmentWindow(Mary,09:15,20)",
      "alignmentFix",
    ]);
  });

  it("describes each side from its own snapshot rather than the other's", () => {
    // The pair that makes the test above non-vacuous: swapping the two sides
    // must swap what is said about them. A report that read one side's numbers
    // twice would look right in every symmetric fixture.
    const lines = alignmentReport(
      finding({
        first: role({ resourceType: "chair", displayName: "Recliner", windowStart: "10:05", granularityMinutes: 25 }),
      }),
      t,
    );

    expect(lines[0]).toBe("alignmentNever(chair,therapist)");
    expect(lines[1]).toBe("alignmentWindow(Recliner,10:05,25)");
    expect(lines[2]).toBe("alignmentWindow(Mary,09:15,20)");
  });

  it("says which resource to change rather than only that something is wrong", () => {
    // The fix is on a RESOURCE — its opening time or its step size — so a report
    // that named only the service would send an editor to the wrong screen.
    expect(alignmentReport(finding(), t)).toContain("alignmentFix");
  });
});

describe("the report never claims the positive (design D1)", () => {
  // The forbidden-vocabulary check lives in `resolution-summary.test.ts`, which
  // now covers the `alignment*` keys as well: the ban is one rule over one set
  // of strings, and stating it twice would let the two copies drift.

  it("emits no key the report cannot support, and no key with no string", () => {
    // A missing key renders as the raw key or as an empty line, which is a
    // sentence the editor cannot act on — and, for the absence case, would be
    // indistinguishable from the silence that means something else entirely.
    return import("../localization/en-us.js").then(({ default: terms }) => {
      const services = (terms as Record<string, Record<string, string>>).ubookitServices;

      for (const key of ["alignmentReport", "alignmentNever", "alignmentWindow", "alignmentFix"]) {
        expect(typeof services[key]).toBe("string");
        expect(services[key].length).toBeGreaterThan(0);
      }
    });
  });
});
