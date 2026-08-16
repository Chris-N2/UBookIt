import { describe, expect, it } from "vitest";
import { requirementSummary, type RequirementDescriptor } from "./requirement-summary.js";
import type { TermResolver } from "./resolution-summary.js";

/**
 * The collection view's requirement summary (services spec, "Backoffice
 * collection view for services").
 *
 * The claim under test is that two configurations the domain treats differently
 * render differently — and that the common case stays short, which is the half a
 * rule of "always show everything" would silently lose.
 */

const t: TermResolver = (key, ...args) => (args.length === 0 ? key : `${key}(${args.join(",")})`);

function role(overrides: Partial<RequirementDescriptor> = {}): RequirementDescriptor {
  return { resourceType: "therapist", requiredCapabilities: [], count: 1, ...overrides };
}

describe("distinguishing two roles of one type", () => {
  it("names what each requires when two roles share a resource type", () => {
    // Legal exactly because the capabilities differ. A summary of type and count
    // alone renders this identically to the configuration the domain REJECTS —
    // and that rejection's message tells the editor to use a count instead, which
    // a reader of the summary would have no way to understand.
    const summary = requirementSummary(
      [role({ requiredCapabilities: ["cert-x"] }), role()],
      t,
    );

    expect(summary).toBe(
      "requirementEntry(1,roleLabelCapabilities(therapist,cert-x)), " +
        "requirementEntry(1,roleLabelNoCapabilities(therapist))",
    );
  });

  it("renders two same-type roles differently from each other", () => {
    // The property behind the assertion above, stated without the key shapes: the
    // two entries must not be the same string.
    const [first, second] = requirementSummary(
      [role({ requiredCapabilities: ["cert-x"] }), role()],
      t,
    ).split(", ");

    expect(first).not.toBe(second);
  });
});

describe("keeping the common case short", () => {
  it("states no capabilities when every role names a distinct type", () => {
    // Without this the rule collapses into "always show everything", which makes
    // every service noisier in order to disambiguate the few (design D5).
    expect(
      requirementSummary(
        [
          role({ resourceType: "room", requiredCapabilities: ["projector"] }),
          role({ requiredCapabilities: ["cert-x"] }),
        ],
        t,
      ),
    ).toBe("requirementEntry(1,room), requirementEntry(1,therapist)");
  });
});

describe("counts", () => {
  it("states a counted role's count", () => {
    expect(requirementSummary([role({ count: 2 })], t)).toBe("requirementEntry(2,therapist)");
  });

  it("says so when a service requires nothing", () => {
    expect(requirementSummary([], t)).toBe("requirementNone");
  });

  it("keeps the order the service declares", () => {
    expect(
      requirementSummary([role({ resourceType: "room" }), role({ count: 3 })], t),
    ).toBe("requirementEntry(1,room), requirementEntry(3,therapist)");
  });
});
