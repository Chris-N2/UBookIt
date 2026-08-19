import { describe, expect, it } from "vitest";
import {
  ROLE_FIELDS,
  attributedFields,
  errorsForRole,
  roleField,
  selectableHintKey,
  summaryLines,
} from "./role-fields.js";
import type { ApiError } from "./api-errors.js";

/**
 * How a server failure finds the requirement row it belongs to, and what the
 * visitor-selectable control says about itself.
 *
 * The claims under test are the two the at-most-one-selectable rule rests on: a
 * failure reported against EVERY row in conflict reaches every one of those rows
 * and no others, and the "one of several" wording appears only where a count
 * makes it true.
 */

const SELECTABLE = "service-role-multiple-selectable";

function error(field: string | undefined, code = SELECTABLE): ApiError {
  return { code, message: `${code} on ${field ?? "nothing"}`, field };
}

describe("attributing a failure to its row", () => {
  it("gives each row in conflict its own copy", () => {
    // The whole point of the server blaming both rather than one arbitrarily: an
    // editor whose second row was marked has no way to know the first one was
    // too, and a message on one row would send them to change the wrong one.
    const errors = [
      error(roleField(0, "VisitorSelectable")),
      error(roleField(2, "VisitorSelectable")),
    ];

    expect(errorsForRole(errors, 0, [SELECTABLE])).toHaveLength(1);
    expect(errorsForRole(errors, 2, [SELECTABLE])).toHaveLength(1);

    // And a row that is NOT in conflict says nothing, or the report would tell an
    // editor to correct a row that is perfectly well formed.
    expect(errorsForRole(errors, 1, [SELECTABLE])).toEqual([]);
  });

  it("does not let one row's failure land on another", () => {
    // `Roles[10]` starts with `Roles[1]` as a string. A prefix comparison would
    // put the eleventh row's message on the second.
    const errors = [error(roleField(10, "VisitorSelectable"))];

    expect(errorsForRole(errors, 1, [SELECTABLE])).toEqual([]);
    expect(errorsForRole(errors, 10, [SELECTABLE])).toHaveLength(1);
  });

  it("ignores a failure whose code the control does not render", () => {
    const errors = [error(roleField(0, "VisitorSelectable"), "type-key-invalid")];

    expect(errorsForRole(errors, 0, [SELECTABLE])).toEqual([]);
  });

  it("ignores a failure carrying no field", () => {
    // Those belong on the group, where the element renders them: a message with
    // no row cannot be shown against one.
    expect(errorsForRole([error(undefined)], 0, [SELECTABLE])).toEqual([]);
  });

  it("counts every control of every row as attributed", () => {
    // The complement of the per-row lookup, and it has to agree with it: a field
    // this set forgets would have its message dropped from the group as
    // "attributed" while no row rendered it.
    const attributed = attributedFields(2);

    for (const index of [0, 1]) {
      for (const field of ROLE_FIELDS) {
        expect(attributed.has(roleField(index, field))).toBe(true);
      }
    }

    expect(attributed.has(roleField(2, "VisitorSelectable"))).toBe(false);
    expect(attributed.size).toBe(2 * ROLE_FIELDS.length);
  });

  it("knows the visitor-selectable control by name", () => {
    // Non-vacuity for the whole file: the field the server actually sends is one
    // of the four. Mirrored from `ServiceRole.FieldFor` plus the property name.
    expect(ROLE_FIELDS).toContain("VisitorSelectable");
    expect(roleField(3, "VisitorSelectable")).toBe("Roles[3].VisitorSelectable");
  });
});

describe("what the control says it does", () => {
  it("says a visitor chooses one of several when the count is greater than one", () => {
    expect(selectableHintKey(2)).toBe("requirementSelectableManyHint");
    expect(selectableHintKey(20)).toBe("requirementSelectableManyHint");
  });

  it("says nothing about several when there is only one", () => {
    // The pair that keeps the assertion above honest: a rule that always said
    // "one of N" would satisfy it and would read as noise on every ordinary row.
    expect(selectableHintKey(1)).toBe("requirementSelectableHint");
  });
});

describe("the error summary", () => {
  it("says a rule reported against several rows once", () => {
    // The at-most-one-selectable rule blames EVERY row in conflict, so its
    // message arrives once per row. The summary is a list of problems: printing
    // the identical sentence twice tells a reader nothing the first line did not,
    // and it stutters. Found by reading the rendered summary in the browser.
    // The real shape: one failure per row, carrying the SAME sentence, because
    // the sentence names every row in conflict rather than the row it is
    // attributed to.
    const message = "Only one requirement may let a visitor choose. Requirements 1 and 2 are both marked.";

    const errors: ApiError[] = [
      { code: SELECTABLE, message, field: roleField(0, "VisitorSelectable") },
      { code: SELECTABLE, message, field: roleField(1, "VisitorSelectable") },
    ];

    expect(summaryLines(errors)).toHaveLength(1);

    // And both rows still render it against their own control — the summary's
    // deduplication must not reach the rows.
    expect(errorsForRole(errors, 0, [SELECTABLE])).toHaveLength(1);
    expect(errorsForRole(errors, 1, [SELECTABLE])).toHaveLength(1);
  });

  it("keeps two problems that say different things", () => {
    // The pair that keeps the rule honest: deduplicating by anything coarser —
    // by code, say — would swallow a second genuine failure of the same rule
    // about a different row's control.
    const errors: ApiError[] = [
      { code: "type-key-invalid", message: "Row 1's type is malformed.", field: roleField(0, "ResourceType") },
      { code: "service-name-required", message: "A service name is required.", field: "Name" },
    ];

    expect(summaryLines(errors)).toHaveLength(2);
  });

  it("preserves the order the server reported", () => {
    const errors: ApiError[] = [
      { code: "a", message: "first", field: undefined },
      { code: "b", message: "second", field: undefined },
      { code: "a", message: "first", field: undefined },
    ];

    expect(summaryLines(errors).map((e) => e.message)).toEqual(["first", "second"]);
  });
});
