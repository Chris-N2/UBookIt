/**
 * Which requirement rows a preview request actually carries.
 *
 * The editor omits any row whose resource type is still blank — an unfinished
 * row is not a configuration to report on, and the other rows' chains are known
 * and still true. That filter is correct and it puts a seam in the middle of the
 * editor: the response's roles are indexed over the rows that *survived*, while
 * everything on screen is indexed over the rows that *exist*.
 *
 * QA round 2 found that seam the hard way. A chain headed "Requirement 1" was
 * describing the row legended "Requirement 2" whenever a blank row sat above a
 * same-type pair, which is the state of any row an editor has just added, or
 * whose type they have cleared in order to retype it. That is worse than the
 * ambiguity the heading was introduced to remove: it points confidently at the
 * wrong control.
 *
 * So the mapping is computed once, here, and travels with the request — never
 * re-derived from an array position at render time. It is a pure function
 * because the defect was structurally invisible to the tests that existed: they
 * called the renderer with an already-filtered list and could not reach the
 * filter at all.
 */

/** The parts of a requirement row this mapping depends on. */
export type PreviewRow = {
  resourceType: string;
};

/** Whether a row is complete enough to report on. */
function carried(row: PreviewRow): boolean {
  return row.resourceType.trim() !== "";
}

/**
 * The **row numbers**, 1-based and as the fieldset legends show them, of the
 * rows a preview request carries — in the order it carries them.
 *
 * `rowNumbers(rows)[i]` is therefore the requirement number of the role the
 * response reports at index `i`. With every row filled it is simply `i + 1`,
 * which is exactly why the bug survived every fixture that had no blank row.
 */
export function rowNumbers(rows: PreviewRow[]): number[] {
  return rows
    .map((row, index) => ({ row, number: index + 1 }))
    .filter((entry) => carried(entry.row))
    .map((entry) => entry.number);
}

/** The rows a preview request carries, in order. */
export function carriedRows<T extends PreviewRow>(rows: T[]): T[] {
  return rows.filter(carried);
}
