import { describe, expect, it } from "vitest";
import {
  chosenRows,
  defaultWindow,
  initialSelection,
  isSelectable,
  previewFailure,
  previewOutcome,
  shouldAskForSource,
  showsImport,
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

describe("whether the import belongs on the screen", () => {
  it("shows it to a writer on a site with a source", () => {
    expect(showsImport(true, true)).toBe(true);
  });

  it("hides it where no source is registered", () => {
    // THE guarantee the whole 'absence is total' design rests on. A site whose developer
    // registered no source does not have this feature, and must see no trace of it.
    expect(showsImport(true, false)).toBe(false);
  });

  it("hides it before the question has been answered", () => {
    // Fails closed: an unanswered probe renders nothing rather than flashing a panel that may
    // not belong on this site at all.
    expect(showsImport(true, undefined)).toBe(false);
  });

  it("hides it from a user who may not change closures", () => {
    // Importing is a way of changing closures, so the verb that gates changing them gates this.
    expect(showsImport(false, true)).toBe(false);
  });

  it("hides it when neither holds", () => {
    expect(showsImport(false, false)).toBe(false);
    expect(showsImport(false, undefined)).toBe(false);
  });
});

describe("whether to ask the server if a source is registered", () => {
  it("asks once, for somebody who could act on the answer", () => {
    expect(shouldAskForSource(true, undefined)).toBe(true);
  });

  it("does not ask again once answered, either way", () => {
    expect(shouldAskForSource(true, true)).toBe(false);
    expect(shouldAskForSource(true, false)).toBe(false);
  });

  it("never asks on behalf of a user who could not act on it", () => {
    // Not merely tidiness: the probe requires the settings verb, so asking would be refused.
    // A Configure-only session must never call it.
    expect(shouldAskForSource(false, undefined)).toBe(false);
  });
});

describe("what a failed preview means", () => {
  it("treats a 404 as the source being gone, not broken", () => {
    // The server answers 404 when no source is registered. A screen opened while one still was
    // must withdraw the panel, not report a feed that failed.
    expect(previewFailure(404)).toBe("absent");
  });

  it("treats anything else as the source having failed", () => {
    expect(previewFailure(500)).toBe("failed");
    expect(previewFailure(502)).toBe("failed");
    expect(previewFailure(undefined)).toBe("failed");
  });

  it("does not call a refusal an absence", () => {
    // 401/403 mean this user may not, which is neither "no source" nor "source broken" — it
    // must not silently withdraw the feature from a site that has it.
    expect(previewFailure(401)).toBe("failed");
    expect(previewFailure(403)).toBe("failed");
  });
});
