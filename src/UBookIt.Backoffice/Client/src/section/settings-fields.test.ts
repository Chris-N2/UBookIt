import { describe, expect, it } from "vitest";
import {
  controlId,
  describedByIds,
  hasConsequence,
  isEditable,
  settingSlug,
} from "./settings-fields.js";

function setting(overrides: Partial<Parameters<typeof describedByIds>[0]> = {}) {
  return {
    key: "UBookIt:AutoConfirm",
    tier: "editable",
    isOverridden: false,
    ...overrides,
  };
}

describe("which ids describe a control", () => {
  it("always includes the description", () => {
    expect(describedByIds(setting(), { hasError: false })).toEqual([
      "setting-autoConfirm-description",
    ]);
  });

  it("includes the consequence statement for the tier that carries one", () => {
    // THE claim this module exists for. uui-* controls carry no aria-describedby and uui-label is
    // not a <label>, so a consequence rendered near the time zone control and not referenced FROM
    // it is invisible to anyone who is not looking at the screen — and the statement's whole
    // purpose is to be read before the change is made.
    const ids = describedByIds(
      setting({ key: "UBookIt:TimeZoneId", tier: "editableWithConsequence" }),
      { hasError: false },
    );

    expect(ids).toContain("setting-timeZoneId-consequence");
  });

  it("does not claim a consequence for a setting that renders none", () => {
    // The other direction: an id pointing at an element the screen did not render sends
    // assistive technology to nothing, which is its own defect rather than a harmless extra.
    expect(describedByIds(setting(), { hasError: false })).not.toContain(
      "setting-autoConfirm-consequence",
    );
  });

  it("references the overridden note only when the setting is overridden", () => {
    expect(describedByIds(setting({ isOverridden: true }), { hasError: false })).toContain(
      "setting-autoConfirm-overridden",
    );
    expect(describedByIds(setting({ isOverridden: false }), { hasError: false })).not.toContain(
      "setting-autoConfirm-overridden",
    );
  });

  it("references the error only when there is one", () => {
    expect(describedByIds(setting(), { hasError: true })).toContain("setting-autoConfirm-error");
    expect(describedByIds(setting(), { hasError: false })).not.toContain(
      "setting-autoConfirm-error",
    );
  });

  it("gathers everything into one list, in reading order", () => {
    const ids = describedByIds(
      setting({ key: "UBookIt:TimeZoneId", tier: "editableWithConsequence", isOverridden: true }),
      { hasError: true },
    );

    expect(ids).toEqual([
      "setting-timeZoneId-description",
      "setting-timeZoneId-consequence",
      "setting-timeZoneId-overridden",
      "setting-timeZoneId-error",
    ]);
  });

  it("describes every id against the control's own id", () => {
    // The label's `for` and every describing id share one base, so a mismatch is impossible to
    // introduce on one side alone.
    const s = setting({ key: "UBookIt:Notifications:InternalRecipients" });

    for (const id of describedByIds(s, { hasError: false })) {
      expect(id.startsWith(`${controlId(s)}-`)).toBe(true);
    }
  });
});

describe("turning a configuration key into a slug", () => {
  it("drops the package prefix and camel-cases the first segment", () => {
    expect(settingSlug("UBookIt:AutoConfirm")).toBe("autoConfirm");
    expect(settingSlug("UBookIt:TimeZoneId")).toBe("timeZoneId");
  });

  it("keeps nested sections distinct", () => {
    // Two keys under the same section must not collapse to one slug, or two settings would share
    // a control id and the label of one would point at the other.
    expect(settingSlug("UBookIt:Notifications:SendBookerEmails")).toBe(
      "notificationsSendBookerEmails",
    );
    expect(settingSlug("UBookIt:Notifications:InternalRecipients")).toBe(
      "notificationsInternalRecipients",
    );
    expect(settingSlug("UBookIt:DeliveryApi:EnableReads")).toBe("deliveryApiEnableReads");
  });
});

describe("what the tier permits", () => {
  it("treats both editable tiers as editable", () => {
    expect(isEditable({ tier: "editable" })).toBe(true);
    expect(isEditable({ tier: "editableWithConsequence" })).toBe(true);
  });

  it("renders no editable control for a read-only setting", () => {
    // The client half of the boundary. The server refuses the write regardless — this only keeps
    // the screen from offering an action it knows will be refused.
    expect(isEditable({ tier: "readOnly" })).toBe(false);
    expect(hasConsequence({ tier: "readOnly" })).toBe(false);
  });
});
