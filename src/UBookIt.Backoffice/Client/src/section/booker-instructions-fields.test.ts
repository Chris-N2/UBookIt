import { describe, expect, it } from "vitest";
import {
  BOOKER_INSTRUCTIONS_ERROR_ID,
  BOOKER_INSTRUCTIONS_HINT_ID,
  BOOKER_INSTRUCTIONS_TOO_LONG,
  instructionsDescribedBy,
  instructionsErrors,
  instructionsForRequest,
  instructionsFromResponse,
} from "./booker-instructions-fields.js";

const tooLong = {
  code: BOOKER_INSTRUCTIONS_TOO_LONG,
  message: "Instructions for the booker may be at most 2000 characters.",
  field: "bookerInstructions",
};
const nameRequired = { code: "service-name-required", message: "A service name is required.", field: "Name" };

describe("loading and saving the instructions", () => {
  it("shows a service's instructions as stored", () => {
    expect(instructionsFromResponse("Join on Teams:\nhttps://example.test/meet")).toBe(
      "Join on Teams:\nhttps://example.test/meet",
    );
  });

  it.each([null, undefined])("shows %s as an empty field", (value) => {
    expect(instructionsFromResponse(value)).toBe("");
  });

  it("sends the text as entered, line breaks included", () => {
    // CRLF as a textarea may hand it over, and LF: both reach the server untouched.
    expect(instructionsForRequest("Join:\r\nhttps://example.test/meet\n\nParking at the rear")).toBe(
      "Join:\r\nhttps://example.test/meet\n\nParking at the rear",
    );
  });

  it("round-trips a loaded value through a save unchanged", () => {
    const stored = "Line one\nLine two";
    expect(instructionsForRequest(instructionsFromResponse(stored))).toBe(stored);
  });

  it.each(["", "   ", "\n\t \r\n"])("sends a blank field as null, removing the instructions", (value) => {
    expect(instructionsForRequest(value)).toBeNull();
  });

  it("does not trim — that rule is the server's", () => {
    expect(instructionsForRequest("  padded  ")).toBe("  padded  ");
  });
});

describe("the field's failures", () => {
  it("claims the too-long failure", () => {
    expect(instructionsErrors([nameRequired, tooLong])).toEqual([tooLong]);
  });

  it("claims it whatever field the server named, so it can never go unsaid", () => {
    const unfielded = { code: BOOKER_INSTRUCTIONS_TOO_LONG, message: "Too long." };
    expect(instructionsErrors([unfielded])).toEqual([unfielded]);
  });

  it("claims nothing else", () => {
    expect(instructionsErrors([nameRequired])).toEqual([]);
  });

  it("is described by its hint alone while it is valid", () => {
    expect(instructionsDescribedBy([nameRequired])).toBe(BOOKER_INSTRUCTIONS_HINT_ID);
  });

  it("is described by its hint and its error once it has failed", () => {
    expect(instructionsDescribedBy([tooLong])).toBe(`${BOOKER_INSTRUCTIONS_HINT_ID} ${BOOKER_INSTRUCTIONS_ERROR_ID}`);
  });
});
