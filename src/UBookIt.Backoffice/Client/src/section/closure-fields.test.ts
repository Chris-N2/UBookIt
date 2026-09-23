import { describe, expect, it } from "vitest";
import {
  exceptionDescribedByIds,
  isSuperseded,
  listQuery,
  optOutIds,
  showsEditingControls,
  supersededId,
} from "./closure-fields.js";
import { canManageClosures, canReadClosures } from "./permission-verbs.js";

const CONFIGURE = "UBookIt.Configure";
const SETTINGS = "UBookIt.Settings";
const BOOKINGS_READ = "UBookIt.Bookings.Read";

function closure(overrides: Partial<Parameters<typeof optOutIds>[0][number]> = {}) {
  return {
    id: "11111111-1111-1111-1111-111111111111",
    date: "2026-12-25",
    label: "Christmas Day",
    excluded: false,
    ...overrides,
  };
}

describe("which exemptions a save carries", () => {
  it("sends the ids the editor marked as excluded", () => {
    const ids = optOutIds([
      closure({ id: "a", excluded: true }),
      closure({ id: "b", excluded: false }),
      closure({ id: "c", excluded: true }),
    ]);

    expect(ids).toEqual(["a", "c"]);
  });

  it("sends nothing when the resource inherits every closure", () => {
    expect(optOutIds([closure(), closure({ id: "b" })])).toEqual([]);
  });

  it("is derived from the whole list every time, so un-ticking withdraws an exemption", () => {
    // Full replacement, like the capability set. Accumulating instead would make an
    // exemption impossible to remove — the failure this asserts against is a save that
    // keeps sending an id the editor has just cleared.
    const before = optOutIds([closure({ id: "a", excluded: true })]);
    const after = optOutIds([closure({ id: "a", excluded: false })]);

    expect(before).toEqual(["a"]);
    expect(after).toEqual([]);
  });
});

describe("whether an exception is stated as superseded", () => {
  it("reports what the server said", () => {
    expect(isSuperseded({ superseded: true })).toBe(true);
    expect(isSuperseded({ superseded: false })).toBe(false);
  });

  it("treats an absent flag as not superseded", () => {
    // The member is optional in the contract because a REQUEST does not carry it. An
    // undefined value means "the server did not state it", and must never read as true —
    // a resource would then be told its exception is inert when it is in force.
    expect(isSuperseded({})).toBe(false);
    expect(isSuperseded({ superseded: undefined })).toBe(false);

    // `null` joined the contract when the C# member became nullable and the client was
    // regenerated (`superseded?: boolean | null`). Pinned because the obvious "simplification"
    // — `superseded !== false` — passes every other assertion here while telling an operator
    // that an exception in force has no effect.
    expect(isSuperseded({ superseded: null })).toBe(false);
  });

  it("never derives the answer from a closure list", () => {
    // The claim this module exists for: precedence has ONE implementation, in the domain.
    // Given a flag that says false, nothing here may decide otherwise — whatever closures
    // the screen happens to be holding.
    expect(isSuperseded({ superseded: false })).toBe(false);
  });
});

describe("what describes an exception's fieldset", () => {
  it("references the superseded statement when one is rendered", () => {
    const ids = exceptionDescribedByIds(2, { hasGroupErrors: false, superseded: true });

    expect(ids).toContain(supersededId(2));
  });

  it("does not reference a statement the screen did not render", () => {
    // An id pointing at an absent element sends assistive technology to nothing, which is
    // its own defect rather than a harmless extra.
    const ids = exceptionDescribedByIds(2, { hasGroupErrors: false, superseded: false });

    expect(ids).not.toContain(supersededId(2));
    expect(ids).toEqual([]);
  });

  it("keeps the group errors when both apply", () => {
    const ids = exceptionDescribedByIds(0, { hasGroupErrors: true, superseded: true });

    expect(ids).toEqual(["err-exceptions", supersededId(0)]);
  });

  it("gives each exception its own statement id", () => {
    expect(supersededId(0)).not.toEqual(supersededId(1));
  });
});

describe("which closures are fetched", () => {
  it("asks the server for upcoming only by default", () => {
    expect(listQuery(false)).toEqual({ includePast: false });
  });

  it("asks the server for everything when past closures are revealed", () => {
    // The filter is the SERVER's. Fetching everything and hiding rows here would make the
    // view's default cost grow with every year the site has been running.
    expect(listQuery(true)).toEqual({ includePast: true });
  });
});

describe("who may do what with closures", () => {
  it("lets either verb read the list", () => {
    expect(canReadClosures([CONFIGURE])).toBe(true);
    expect(canReadClosures([SETTINGS])).toBe(true);
  });

  it("lets only the settings verb change it", () => {
    // Configure means "may add a meeting room". One closure shuts every resource the site
    // has, including those created after it.
    expect(canManageClosures([CONFIGURE])).toBe(false);
    expect(canManageClosures([SETTINGS])).toBe(true);
  });

  it("refuses a user holding neither", () => {
    expect(canReadClosures([BOOKINGS_READ])).toBe(false);
    expect(canManageClosures([BOOKINGS_READ])).toBe(false);
    expect(canReadClosures(undefined)).toBe(false);
  });

  it("offers the editing controls only to a writer", () => {
    // The other branch is not an absence: a reader is TOLD what the grant is and where it
    // is given, because the settings verb is never seeded and everybody starts here.
    expect(showsEditingControls(canManageClosures([SETTINGS]))).toBe(true);
    expect(showsEditingControls(canManageClosures([CONFIGURE]))).toBe(false);
  });
});
