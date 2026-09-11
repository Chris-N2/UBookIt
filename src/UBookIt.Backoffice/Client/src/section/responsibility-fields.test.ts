import { describe, expect, it } from "vitest";
import { buildAssignments, marksFor, selectionOf } from "./responsibility-fields.js";
import type { ResponsibilityPartyModel } from "../api/index.js";

/**
 * The panel's decisions: which parties each picker shows, which assignments the
 * save writes, and — the load-bearing one — which assignments are listed with a
 * condition instead of vanishing. The pickers render from Umbraco's current
 * data, so a deleted party is exactly the one they cannot show; the marks list
 * is its only remaining visibility.
 */

function party(overrides: Partial<ResponsibilityPartyModel>): ResponsibilityPartyModel {
  return {
    kind: "user",
    key: "0f000000-0000-0000-0000-000000000001",
    exists: true,
    displayName: "Ada Lovelace",
    userState: "Active",
    ...overrides,
  };
}

describe("splitting parties between the pickers", () => {
  it("hands each picker only its own kind", () => {
    const parties = [
      party({ kind: "user", key: "u1" }),
      party({ kind: "group", key: "g1", userState: null }),
      party({ kind: "user", key: "u2" }),
    ];

    expect(selectionOf(parties, "user")).toEqual(["u1", "u2"]);
    expect(selectionOf(parties, "group")).toEqual(["g1"]);
  });
});

describe("marking assignments the pickers cannot speak for", () => {
  it("marks a party that no longer exists", () => {
    const marks = marksFor([party({ exists: false, displayName: null, userState: null })]);

    expect(marks).toHaveLength(1);
    expect(marks[0].mark).toBe("missing");
    expect(marks[0].displayName).toBeNull();
  });

  it("marks the states sending skips, each as itself", () => {
    // Disabled and Invited are distinct conditions with distinct fixes — one
    // is an ended account, the other an unaccepted one — so they must not
    // collapse into a single "inactive" mark.
    const marks = marksFor([
      party({ key: "d", userState: "Disabled" }),
      party({ key: "i", userState: "Invited" }),
    ]);

    expect(marks.map((mark) => mark.mark)).toEqual(["disabled", "invited"]);
  });

  it("does not mark parties sending writes to", () => {
    // Every state the mail path resolves, plus a group (whose state is null):
    // none needs attention, so none is listed.
    const marks = marksFor([
      party({ key: "a", userState: "Active" }),
      party({ key: "n", userState: "Inactive" }),
      party({ key: "l", userState: "LockedOut" }),
      party({ kind: "group", key: "g", userState: null }),
    ]);

    expect(marks).toEqual([]);
  });

  it("keeps the marked party's name when it has one", () => {
    const marks = marksFor([party({ userState: "Disabled" })]);

    expect(marks[0].displayName).toBe("Ada Lovelace");
  });
});

describe("building the save body", () => {
  it("projects both pickers with their kinds, users first", () => {
    expect(buildAssignments(["u1", "u2"], ["g1"])).toEqual([
      { kind: "user", key: "u1" },
      { kind: "user", key: "u2" },
      { kind: "group", key: "g1" },
    ]);
  });

  it("projects empty pickers as an empty set, not a skipped save", () => {
    // Deliberate: removing the last assignee is a real edit, and the write is
    // wholesale. A helper that returned nothing here would silently make
    // "remove everyone" unsaveable.
    expect(buildAssignments([], [])).toEqual([]);
  });
});
