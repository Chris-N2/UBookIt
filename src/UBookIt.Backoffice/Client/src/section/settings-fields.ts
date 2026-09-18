import type { SettingResponseModel } from "../api/index.js";

/**
 * The settings screen's decisions, as pure functions so they are tested as claims — the
 * client's established pattern.
 *
 * The one that matters most here is {@link describedByIds}. `uui-*` controls carry no
 * `aria-describedby` of their own and `uui-label` is not a `<label>`, so text rendered NEXT TO a
 * control is visually present and programmatically unrelated to it. That is not a styling nicety:
 * the time-zone consequence statement exists to be read before the change is made, and a screen
 * reader that never reaches it from the control has not been told.
 */

/** `UBookIt:Notifications:SendBookerEmails` -> `notificationsSendBookerEmails`. */
export function settingSlug(key: string): string {
  return key
    .split(":")
    .slice(1)
    .map((part, index) => (index === 0 ? part.charAt(0).toLowerCase() + part.slice(1) : part))
    .join("");
}

/** Whether the screen renders an editable control for this setting. */
export function isEditable(setting: Pick<SettingResponseModel, "tier">): boolean {
  return setting.tier === "editable" || setting.tier === "editableWithConsequence";
}

/** Whether this setting carries the consequence statement. */
export function hasConsequence(setting: Pick<SettingResponseModel, "tier">): boolean {
  return setting.tier === "editableWithConsequence";
}

/**
 * Every id that describes a setting's control, in reading order, for one `aria-describedby`.
 *
 * Built from what is actually rendered rather than from a fixed list: an id naming an element the
 * screen did not render points assistive technology at nothing, which is its own defect.
 */
export function describedByIds(
  // `unmetDependency` is spelled structurally rather than taken from the generated model, so this
  // function is usable before the client is regenerated against a running site — and so a caller
  // that has not yet gained the member is a compile error about ITS type rather than about this
  // signature.
  setting: Pick<SettingResponseModel, "key" | "tier" | "isOverridden"> & {
    unmetDependency?: string | null;
  },
  options: { hasError: boolean },
): string[] {
  const base = `setting-${settingSlug(setting.key)}`;

  return [
    `${base}-description`,
    hasConsequence(setting) ? `${base}-consequence` : undefined,
    // BEFORE the override note and the error, because it is the one that explains why the value
    // shown is not the behaviour the site has. A reader who never reaches it is left believing a
    // setting is doing something it is not.
    setting.unmetDependency ? `${base}-dependency` : undefined,
    setting.isOverridden ? `${base}-overridden` : undefined,
    options.hasError ? `${base}-error` : undefined,
  ].filter((id): id is string => id !== undefined);
}

/**
 * The control's own id, which the label's `for` must match.
 */
export function controlId(setting: Pick<SettingResponseModel, "key">): string {
  return `setting-${settingSlug(setting.key)}`;
}
