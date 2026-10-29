import { describe, expect, it } from "vitest";
import { showsVariableLengthHint } from "./duration-fields.js";
import en from "../localization/en-us.js";

describe("the variable-length hint", () => {
  it("is shown for a variable length, which it describes", () => {
    expect(showsVariableLengthHint("variable")).toBe(true);
    // The claim the hint makes, so a reworded hint is checked against the mode it is shown for.
    expect(en.ubookitServices.durationVariableHint).toContain("chooses how long they need");
  });

  it("is not shown for a fixed length, where the person booking chooses nothing", () => {
    expect(showsVariableLengthHint("fixed")).toBe(false);
  });
});
