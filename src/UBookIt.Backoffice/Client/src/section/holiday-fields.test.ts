import { describe, expect, it } from "vitest";
import {
  chosenRows,
  defaultWindow,
  initialSelection,
  isSelectable,
  previewOutcome,
  toggleDate,
} from "./holiday-fields.js";

function row(overrides: Partial<Parameters<typeof isSelectable>[0] & { date: string; name: string }> = {}) {
  return {
    date: "2027-01-01",
    name: "New Year's Day",
    state: "new",
    reason: null,
    collapsedDuplicate: false,
    ...overrides,
  } as Parameters<typeof initialSelection>[0][number];
}

describe("which rows an operator may tick", () => {
  it("offers a new date", () => {
    expect(isSelectable(row())).toBe(true);
  });

  it("does not offer a date that already has a closure", () => {
    // Nothing to create: one closure per date, and the existing one is not the import's to
    // replace — it may carry a label somebody chose.
    expect(isSelectable(row({ state: "alreadyClosed" }))).toBe(false);
  });

  it("does not offer a row the server would refuse", () => {
    // Offering it would be a control that looks live and is not.
    expect(isSelectable(row({ state: "cannotImport" }))).toBe(false);
  });

  it("does not offer a state it has never heard of", () => {
    // A state added server-side and not taught here must fail CLOSED. Defaulting to selectable
    // would send rows whose meaning this client does not know.
    expect(isSelectable(row({ state: "somethingNew" }))).toBe(false);
  });
});

describe("what is ticked when a preview arrives", () => {
  it("ticks every selectable row", () => {
    const selected = initialSelection([
      row({ date: "2027-01-01" }),
      row({ date: "2027-04-02" }),
    ]);

    expect(selected).toEqual(["2027-01-01", "2027-04-02"]);
  });

  it("ticks nothing that cannot be chosen", () => {
    const selected = initialSelection([
      row({ date: "2027-01-01" }),
      row({ date: "2027-12-25", state: "alreadyClosed" }),
      row({ date: "2027-06-01", state: "cannotImport" }),
    ]);

    expect(selected).toEqual(["2027-01-01"]);
  });
});

describe("what an import sends", () => {
  it("sends the ticked rows with their names", () => {
    const rows = [row({ date: "2027-01-01" }), row({ date: "2027-04-02", name: "Good Friday" })];

    expect(chosenRows(rows, ["2027-01-01", "2027-04-02"])).toEqual([
      { date: "2027-01-01", name: "New Year's Day" },
      { date: "2027-04-02", name: "Good Friday" },
    ]);
  });

  it("does not send an unticked row", () => {
    // THE point of the whole screen: unticking May Day means May Day is not created.
    const rows = [row({ date: "2027-01-01" }), row({ date: "2027-05-03", name: "Early May" })];

    expect(chosenRows(rows, ["2027-01-01"])).toEqual([
      { date: "2027-01-01", name: "New Year's Day" },
    ]);
  });

  it("is derived from the whole list every time, so unticking withdraws a date", () => {
    const rows = [row({ date: "2027-01-01" })];

    expect(chosenRows(rows, ["2027-01-01"])).toHaveLength(1);
    expect(chosenRows(rows, [])).toHaveLength(0);
  });

  it("never sends an unselectable row, even if a stale selection names it", () => {
    // The selection survives a re-preview in which a date has since been closed by somebody
    // else. Sending it would ask the server to create something it has already refused.
    const rows = [row({ date: "2027-12-25", state: "alreadyClosed" })];

    expect(chosenRows(rows, ["2027-12-25"])).toEqual([]);
  });

  it("sends nothing when nothing is ticked", () => {
    expect(chosenRows([row()], [])).toEqual([]);
  });
});

describe("toggling a date", () => {
  it("adds and removes without duplicating", () => {
    let selected = toggleDate([], "2027-01-01", true);
    expect(selected).toEqual(["2027-01-01"]);

    selected = toggleDate(selected, "2027-01-01", true);
    expect(selected).toEqual(["2027-01-01"]);

    selected = toggleDate(selected, "2027-01-01", false);
    expect(selected).toEqual([]);
  });

  it("leaves the other dates alone", () => {
    expect(toggleDate(["2027-01-01", "2027-04-02"], "2027-01-01", false)).toEqual(["2027-04-02"]);
  });
});

describe("the window a preview opens on", () => {
  it("runs from today to the end of next year", () => {
    // A fixed clock in DECEMBER, where "this year" and "next year" genuinely differ — a test
    // run in January would pass against an implementation that returned the wrong one.
    expect(defaultWindow(new Date("2026-12-30T12:00:00Z"))).toEqual({
      from: "2026-12-30",
      to: "2027-12-31",
    });
  });

  it("still spans into next year from January", () => {
    expect(defaultWindow(new Date("2027-01-02T12:00:00Z"))).toEqual({
      from: "2027-01-02",
      to: "2028-12-31",
    });
  });
});

describe("what the screen says about a preview", () => {
  it("distinguishes rows, emptiness, and failure", () => {
    expect(previewOutcome([row()], false)).toBe("rows");
    expect(previewOutcome([], false)).toBe("empty");
    expect(previewOutcome(undefined, true)).toBe("failed");
  });

  it("calls a failure a failure even when rows are somehow present", () => {
    // Failure wins. Conflating a broken source with an empty window tells an operator their
    // calendar is clear when it is unknown — the distinction the server keeps, kept here too.
    expect(previewOutcome([row()], true)).toBe("failed");
  });

  it("calls an empty window empty rather than failed", () => {
    // The other direction: a site with no holidays in the window is a correct answer, and
    // reporting it as a broken source would send somebody to debug a feed that is fine.
    expect(previewOutcome([], false)).toBe("empty");
  });
});
