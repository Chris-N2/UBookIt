import { describe, expect, it } from "vitest";
import {
  TEMPLATE_SET_ERROR_ID,
  TEMPLATE_SET_HINT_ID,
  TEMPLATE_SET_INVALID,
  templateSetAfterSave,
  templateSetDescribedBy,
  templateSetErrors,
  templateSetForRequest,
  templateSetFromResponse,
} from "./template-set-fields.js";
import { BOOKER_INSTRUCTIONS_TOO_LONG, instructionsErrors } from "./booker-instructions-fields.js";
import en from "../localization/en-us.js";

const invalid = {
  code: TEMPLATE_SET_INVALID,
  message: "An email template set may use only lower-case letters a-z, digits and single hyphens between them.",
  field: "templateSet",
};
const tooLong = {
  code: BOOKER_INSTRUCTIONS_TOO_LONG,
  message: "Instructions for the booker may be at most 2000 characters.",
  field: "bookerInstructions",
};
const nameRequired = { code: "service-name-required", message: "A service name is required.", field: "Name" };

describe("loading and saving the template set", () => {
  it("shows a service's set as stored", () => {
    expect(templateSetFromResponse("remote")).toBe("remote");
  });

  it.each([null, undefined])("shows %s as an empty field", (value) => {
    expect(templateSetFromResponse(value)).toBe("");
  });

  it("round-trips a loaded value through a save unchanged", () => {
    expect(templateSetForRequest(templateSetFromResponse("in-person"))).toBe("in-person");
  });

  it("sends the name as typed — lowering and trimming are the server's", () => {
    expect(templateSetForRequest(" Remote ")).toBe(" Remote ");
  });

  it("shows what the server stored after a save, so Remote is seen as remote", () => {
    // The stored value comes from the server's answer, never from the field: a function that kept
    // the typed value would leave "Remote" on screen and fail here.
    expect(templateSetAfterSave("Remote", { templateSet: "remote" })).toBe("remote");
  });

  it("shows no set after a save that removed it", () => {
    expect(templateSetAfterSave("   ", { templateSet: null })).toBe("");
    expect(templateSetAfterSave("   ", {})).toBe("");
  });

  it("keeps exactly what was typed when the save answered with no service", () => {
    expect(templateSetAfterSave("../x", undefined)).toBe("../x");
  });

  it.each(["", "   ", "\t"])("sends a blank field as null, removing the set", (value) => {
    expect(templateSetForRequest(value)).toBeNull();
  });
});

describe("the field's failures", () => {
  it("claims the invalid-set failure", () => {
    expect(templateSetErrors([nameRequired, tooLong, invalid])).toEqual([invalid]);
  });

  it("claims it whatever field the server named, so it can never go unsaid", () => {
    const unfielded = { code: TEMPLATE_SET_INVALID, message: "Invalid." };
    expect(templateSetErrors([unfielded])).toEqual([unfielded]);
  });

  it("claims nothing else", () => {
    expect(templateSetErrors([nameRequired, tooLong])).toEqual([]);
  });

  it("is not claimed by the instructions field, so it renders against this field only", () => {
    expect(instructionsErrors([invalid])).toEqual([]);
  });

  it("is described by its hint alone while it is valid", () => {
    expect(templateSetDescribedBy([nameRequired, tooLong])).toBe(TEMPLATE_SET_HINT_ID);
  });

  it("is described by its hint and its error once it has failed", () => {
    expect(templateSetDescribedBy([invalid])).toBe(`${TEMPLATE_SET_HINT_ID} ${TEMPLATE_SET_ERROR_ID}`);
  });
});

describe("the field's wording", () => {
  it("is labelled Email template set", () => {
    expect(en.ubookitServices.templateSet).toBe("Email template set");
  });

  it("says where the views live, that the name is lowered, and which characters it may use", () => {
    const hint = en.ubookitServices.templateSetHint;
    expect(hint).toContain("Views/Partials/UBookIt/Emails/");
    expect(hint).toContain("lower case");
    // a–z named, because accented letters are refused and "letters" alone would not say so.
    expect(hint).toContain("letters a–z, numbers and hyphens");
  });
});
