import { describe, expect, it } from "vitest";
import {
  sufficiencyReport,
  type ShortfallRoleSnapshot,
  type ShortfallSnapshot,
} from "./sufficiency-report.js";
import type { TermResolver } from "./resolution-summary.js";

/**
 * The pool-sufficiency report's phrasing (services spec, "The editor reports
 * roles whose pools cannot be filled together").
 *
 * As with the other two reports, these assert the KEYS and their arguments
 * rather than the English, so a copy edit does not break them — what has to hold
 * is that every sentence the report can produce is true of the finding it
 * describes. The forbidden-vocabulary check lives in `resolution-summary.test.ts`
 * and now covers the `sufficiency*` and `roleLabel*` keys: the ban is one rule
 * over one set of strings, and stating it twice would let the copies drift.
 */

const t: TermResolver = (key, ...args) => (args.length === 0 ? key : `${key}(${args.join(",")})`);

function role(overrides: Partial<ShortfallRoleSnapshot> = {}): ShortfallRoleSnapshot {
  return { resourceType: "therapist", requiredCapabilities: [], count: 1, ...overrides };
}

function finding(overrides: Partial<ShortfallSnapshot> = {}): ShortfallSnapshot {
  return { roles: [role({ count: 2 })], required: 2, eligible: 1, ...overrides };
}

describe("silence", () => {
  it("says nothing when there is no finding", () => {
    // The same silence covers "the roles can be filled together", "the
    // configuration is too incomplete to resolve", and "the request failed".
    // That conflation is the one-directional stance: none of the three is a claim
    // that the service can be booked, so none may be phrased as one.
    expect(sufficiencyReport(null, t)).toEqual([]);
  });
});

describe("what the report states", () => {
  it("states required against eligible and names the requirement", () => {
    const lines = sufficiencyReport(finding(), t);

    expect(lines[0]).toBe("sufficiencyOneEligible(2)");
    expect(lines[1]).toBe("sufficiencyRole(therapist,2)");
  });

  it("distinguishes several eligible from exactly one", () => {
    // Separate phrasings rather than a plural placeholder, matching how the
    // chains handle the same problem.
    expect(sufficiencyReport(finding({ required: 4, eligible: 3 }), t)[0]).toBe(
      "sufficiencyShort(4,3)",
    );
  });

  it("phrases a pool of nothing in words rather than as a zero", () => {
    // A zero here is a genuine finding, and it must not read like the "not known"
    // zero the chains are careful never to print. Saying it in words keeps the
    // two apart on screen.
    expect(sufficiencyReport(finding({ required: 1, eligible: 0 }), t)[0]).toBe(
      "sufficiencyNoneEligible(1)",
    );
  });

  it("names both repairs, because either fixes it", () => {
    // The fault is as often a missing resource as a wrong count, so a report
    // naming only the count would send an editor to change a row that is right.
    expect(sufficiencyReport(finding(), t)).toContain("sufficiencyFix");
  });
});

describe("naming the requirements", () => {
  it("tells two roles of one type apart by what each requires", () => {
    // The clearest case the report exists for: two `therapist` rows over one
    // resource. Rendering both as "therapist" would leave an editor unable to
    // tell which row the finding is about — and it is about both.
    const lines = sufficiencyReport(
      finding({
        roles: [
          role({ requiredCapabilities: ["cert-x"] }),
          role({ requiredCapabilities: [] }),
        ],
      }),
      t,
    );

    expect(lines.slice(1, 3)).toEqual([
      "sufficiencyRole(roleLabelCapabilities(therapist,cert-x),1)",
      "sufficiencyRole(roleLabelNoCapabilities(therapist),1)",
    ]);
  });

  it("keeps the label bare where the two roles name different types", () => {
    // The common case stays short. Adding capability text everywhere would make
    // every finding noisier in order to disambiguate the few that need it.
    const lines = sufficiencyReport(
      finding({
        roles: [
          role({ resourceType: "room", requiredCapabilities: ["projector"] }),
          role({ resourceType: "therapist", requiredCapabilities: ["cert-x"] }),
        ],
        required: 2,
        eligible: 1,
      }),
      t,
    );

    expect(lines.slice(1, 3)).toEqual([
      "sufficiencyRole(room,1)",
      "sufficiencyRole(therapist,1)",
    ]);
  });

  it("states each requirement's own count, not the total", () => {
    // The total is in the headline; the rows carry what each one asks for, which
    // is the number an editor changes.
    const lines = sufficiencyReport(
      finding({
        roles: [role({ count: 3 }), role({ resourceType: "room", count: 1 })],
        required: 4,
        eligible: 2,
      }),
      t,
    );

    expect(lines[1]).toBe("sufficiencyRole(therapist,3)");
    expect(lines[2]).toBe("sufficiencyRole(room,1)");
  });
});

describe("every key it can emit has a string", () => {
  it("resolves each one", async () => {
    // A missing key renders as the raw key or as an empty line — a sentence the
    // editor cannot act on, and for the absence case indistinguishable from the
    // silence that means something else entirely.
    const { default: terms } = await import("../localization/en-us.js");
    const services = (terms as Record<string, Record<string, string>>).ubookitServices;

    for (const key of [
      "sufficiencyReport",
      "sufficiencyShort",
      "sufficiencyOneEligible",
      "sufficiencyNoneEligible",
      "sufficiencyRole",
      "sufficiencyFix",
      "roleLabelCapabilities",
      "roleLabelNoCapabilities",
    ]) {
      expect(typeof services[key]).toBe("string");
      expect(services[key].length).toBeGreaterThan(0);
    }
  });
});
