import { describe, expect, it } from "vitest";
import {
  closedPeriodMissingStart,
  closedPeriodsForSave,
  rangeEnd,
  rangeText,
} from "./closure-fields.js";

describe("a range's end", () => {
  it("is the start when To is left empty, so one day needs one date", () => {
    expect(rangeEnd("2027-02-01", "")).toBe("2027-02-01");
  });

  it("is what was entered otherwise", () => {
    expect(rangeEnd("2027-02-01", "2027-03-31")).toBe("2027-03-31");
  });
});

describe("how a range reads", () => {
  it("shows a single day as that date, not as a range from a date to itself", () => {
    expect(rangeText("2026-12-25", "2026-12-25")).toBe("2026-12-25");
    expect(rangeText("2026-12-25", undefined)).toBe("2026-12-25");
  });

  it("shows a range as start – end", () => {
    expect(rangeText("2027-02-01", "2027-03-31")).toBe("2027-02-01 – 2027-03-31");
  });
});

describe("the closed periods a save carries", () => {
  it("sends every period, with an empty To meaning the one day and a blank note meaning none", () => {
    expect(
      closedPeriodsForSave([
        { startDate: "2027-03-01", endDate: "", note: "  " },
        { startDate: "2027-04-01", endDate: "2027-04-14", note: " Being painted " },
      ]),
    ).toEqual([
      { startDate: "2027-03-01", endDate: "2027-03-01", note: null },
      { startDate: "2027-04-01", endDate: "2027-04-14", note: "Being painted" },
    ]);
  });

  it("sends an empty list, not nothing, when the section is empty — so clearing it clears them", () => {
    // The server reads an ABSENT collection as "leave them as they are". This editor must
    // never send that, or removing the last period would silently keep it.
    expect(closedPeriodsForSave([])).toEqual([]);
  });
});

describe("a period with no start", () => {
  it("is caught before saving", () => {
    expect(closedPeriodMissingStart([{ startDate: "", endDate: "", note: "" }])).toBe(true);
    expect(closedPeriodMissingStart([{ startDate: "2027-03-01", endDate: "", note: "" }])).toBe(false);
  });
});
