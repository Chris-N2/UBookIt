import { describe, expect, it } from "vitest";
import { carriedRows, rowNumbers } from "./preview-rows.js";

/**
 * The seam between what a preview request carries and what the editor shows.
 *
 * This file exists because of a defect no other test could reach. The chain
 * headings took their requirement number from the response array's index, and
 * every test fed the renderer an already-filtered list — so the filter and the
 * renderer were each correct in isolation while disagreeing about what an index
 * meant. QA found it by driving the real element with a blank row on top.
 *
 * The mapping is a pure function precisely so that disagreement is assertable
 * here rather than only on screen. The cases below are therefore about
 * *misalignment between the two indices*: a fixture where every row is filled
 * makes `rowNumbers` the identity and proves nothing at all.
 */

const row = (resourceType: string) => ({ resourceType });

describe("which rows a request carries", () => {
  it("drops a row whose type is still blank", () => {
    // Unfinished, not broken. The other rows' chains are known and still true,
    // and saying nothing about this one is what "not known" looks like.
    expect(carriedRows([row("room"), row(""), row("therapist")]).map((r) => r.resourceType)).toEqual(
      ["room", "therapist"],
    );
  });

  it("treats whitespace as blank, matching what the request trims", () => {
    expect(carriedRows([row("   "), row("room")]).map((r) => r.resourceType)).toEqual(["room"]);
  });

  it("agrees with the numbering about what blank means", () => {
    // The two exports share one predicate, so they cannot disagree today — and
    // this asserts that rather than assuming it. QA proved the gap by making
    // `rowNumbers` test `!== ""` while `carriedRows` kept trimming: 57 tests
    // stayed green while a whitespace-only row would be counted by one and
    // dropped by the other, reintroducing the round-2 defect through a different
    // door. This module exists so that disagreement is assertable; leaving the
    // one predicate untested on one side would have been the whole point missed.
    expect(rowNumbers([row("   "), row("room")])).toEqual([2]);
    expect(rowNumbers([row("room"), row("	"), row("chair")])).toEqual([1, 3]);
  });
});

describe("which row each carried role came from", () => {
  it("is the identity when every row is filled", () => {
    // The degenerate case — and the reason the defect survived every fixture.
    expect(rowNumbers([row("room"), row("therapist")])).toEqual([1, 2]);
  });

  it("skips a blank row above, so later rows keep their own numbers", () => {
    // THE case. Row 1 is blank, so the response carries two roles; reading their
    // array positions would call them requirements 1 and 2, when they are the
    // rows legended 2 and 3. That is a heading pointing at the wrong control.
    expect(rowNumbers([row(""), row("therapist"), row("therapist")])).toEqual([2, 3]);
  });

  it("skips blank rows anywhere, not only at the top", () => {
    expect(rowNumbers([row("room"), row(""), row("therapist"), row(""), row("chair")])).toEqual([
      1, 3, 5,
    ]);
  });

  it("is empty when nothing is filled in yet", () => {
    // The editor sends no request at all in this state; the mapping agrees.
    expect(rowNumbers([row(""), row("")])).toEqual([]);
  });

  it("indexes the carried roles one-for-one, in order", () => {
    // The property the editor actually relies on: `rowNumbers(rows)[i]` is the
    // row of the role the response reports at index `i`. Asserted as a pairing
    // rather than as two independent lists, because the defect was exactly that
    // the two were computed from different predicates.
    const rows = [row(""), row("room"), row(""), row("therapist"), row("chair")];

    const numbers = rowNumbers(rows);
    const carried = carriedRows(rows);

    expect(numbers).toHaveLength(carried.length);
    expect(carried.map((r, i) => [numbers[i], r.resourceType])).toEqual([
      [2, "room"],
      [4, "therapist"],
      [5, "chair"],
    ]);
  });
});
