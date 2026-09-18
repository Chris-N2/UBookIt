import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_CURRENT_USER_CONTEXT } from "@umbraco-cms/backoffice/current-user";
import { canManageSettings } from "./permission-verbs.js";
import { UBookItBackofficeService } from "../api/index.js";
import type { SettingResponseModel } from "../api/index.js";
import { controlId, describedByIds, hasConsequence, settingSlug } from "./settings-fields.js";

/**
 * The site settings screen.
 *
 * Three tiers, declared by the SERVER and merely rendered here: editable, editable with a stated
 * consequence, and read-only. The server refuses a write to a read-only key regardless of what
 * this renders — a boundary held only by the client is reachable by anyone who can call the
 * endpoint, and the boundary exists to keep irreversible erasure out of reach.
 */
@customElement("ubookit-settings-view")
export class UBookItSettingsViewElement extends UmbLitElement {
  @state()
  private _settings: SettingResponseModel[] = [];

  @state()
  private _loading = true;

  /** Undefined until the current user is known — neither granted nor refused yet. */
  @state()
  private _permitted?: boolean;

  @state()
  private _error?: string;

  /** Per-key validation failures reported by the server. */
  @state()
  private _fieldErrors: Record<string, string> = {};

  /** Keys currently being saved, so a control can be disabled without freezing the screen. */
  @state()
  private _busy: Set<string> = new Set();

  #term(key: string) {
    return this.localize.term(`ubookitSettings_${key}`);
  }

  constructor() {
    super();

    this.consumeContext(UMB_CURRENT_USER_CONTEXT, (context) => {
      this.observe(context?.currentUser, (currentUser) => {
        const permitted = canManageSettings(currentUser?.fallbackPermissions);
        this._permitted = permitted;

        if (permitted && this._settings.length === 0) {
          void this.#load();
        } else if (!permitted) {
          this._loading = false;
        }
      });
    });
  }

  async #load() {
    this._loading = true;
    this._error = undefined;

    try {
      const { data, error } = await UBookItBackofficeService.getSettings();

      if (error || !data) {
        this._error = this.#term("loadFailed");
      } else {
        this._settings = data.settings;
      }
    } catch {
      this._error = this.#term("loadFailed");
    }

    this._loading = false;
  }

  #setBusy(key: string, busy: boolean) {
    const next = new Set(this._busy);
    if (busy) {
      next.add(key);
    } else {
      next.delete(key);
    }
    this._busy = next;
  }

  #clearFieldError(key: string) {
    if (this._fieldErrors[key] === undefined) {
      return;
    }
    const next = { ...this._fieldErrors };
    delete next[key];
    this._fieldErrors = next;
  }

  async #save(setting: SettingResponseModel, value: string) {
    this.#setBusy(setting.key, true);
    this.#clearFieldError(setting.key);

    try {
      const { error } = await UBookItBackofficeService.putSetting({
        path: { key: setting.key },
        body: { value },
      });

      if (error) {
        // The server's message names what is wrong with THIS value — it is the only place that
        // knows, since validation lives beside the resolver's own reading of the setting.
        this._fieldErrors = {
          ...this._fieldErrors,
          [setting.key]: this.#detail(error) ?? this.#term("saveFailed"),
        };
      } else {
        await this.#load();
      }
    } catch {
      this._fieldErrors = { ...this._fieldErrors, [setting.key]: this.#term("saveFailed") };
    }

    this.#setBusy(setting.key, false);
  }

  async #reset(setting: SettingResponseModel) {
    this.#setBusy(setting.key, true);
    this.#clearFieldError(setting.key);

    try {
      const { error } = await UBookItBackofficeService.resetSetting({
        path: { key: setting.key },
      });

      if (error) {
        this._fieldErrors = { ...this._fieldErrors, [setting.key]: this.#term("resetFailed") };
      } else {
        await this.#load();
      }
    } catch {
      this._fieldErrors = { ...this._fieldErrors, [setting.key]: this.#term("resetFailed") };
    }

    this.#setBusy(setting.key, false);
  }

  #detail(error: unknown): string | undefined {
    const detail = (error as { detail?: unknown })?.detail;
    return typeof detail === "string" ? detail : undefined;
  }

  /** The label and help text keys follow the setting key, so a new setting needs no code here. */
  #labelFor(setting: SettingResponseModel): string {
    return this.localize.term(`ubookitSettings_${this.#slug(setting.key)}Label`);
  }

  #descriptionFor(setting: SettingResponseModel): string {
    return this.localize.term(`ubookitSettings_${this.#slug(setting.key)}Description`);
  }

  #slug(key: string): string {
    return settingSlug(key);
  }

  #onInput(event: Event, setting: SettingResponseModel) {
    const target = event.target as HTMLInputElement & { checked?: boolean; value?: string };
    const value =
      setting.valueKind === "boolean"
        ? String(target.checked === true)
        : String(target.value ?? "");

    void this.#save(setting, value);
  }

  override render() {
    if (this._permitted === false) {
      // NOT an error and not an empty screen. The verb is granted by hand, deliberately, so the
      // only useful thing to say is that it exists and where it is turned on.
      return html`
        <div class="header">
          <h2>${this.localize.term("ubookitSettings_label")}</h2>
        </div>
        <p class="intro">${this.#term("notPermitted")}</p>
      `;
    }

    if (this._loading || this._permitted === undefined) {
      return html`<uui-loader></uui-loader>`;
    }

    return html`
      <div class="header">
        <h2>${this.localize.term("ubookitSettings_label")}</h2>
      </div>

      ${this._error
        ? html`<div class="error" role="alert">${this._error}</div>`
        : nothing}

      <p class="intro">${this.#term("intro")}</p>

      <div class="settings">
        ${this._settings.map((setting) => this.#renderSetting(setting))}
      </div>
    `;
  }

  #renderSetting(setting: SettingResponseModel) {
    const id = controlId(setting);
    const descriptionId = `${id}-description`;
    const consequenceId = `${id}-consequence`;
    const errorId = `${id}-error`;
    const overriddenId = `${id}-overridden`;

    const fieldError = this._fieldErrors[setting.key];
    const carriesConsequence = hasConsequence(setting);

    // EVERY id that describes this control, gathered into ONE aria-describedby — composed by
    // settings-fields.ts so the decision is tested as a claim rather than asserted by looking at
    // the screen. uui-* controls carry no aria-describedby of their own and uui-label is not a
    // <label>, so text rendered NEXT TO a control is visually present and programmatically
    // unrelated to it. The consequence statement in particular is useless if a screen reader
    // never reaches it from the control it warns about.
    const describedBy = describedByIds(setting, { hasError: fieldError !== undefined }).join(" ");

    return html`
      <div
        class="setting ${setting.tier === "readOnly" ? "read-only" : ""}"
        role=${setting.tier === "readOnly" ? "group" : nothing}
        aria-labelledby=${setting.tier === "readOnly" ? `${id}-label` : nothing}
        aria-describedby=${setting.tier === "readOnly" ? describedBy : nothing}
      >
        ${setting.tier === "readOnly"
          ? // NOT a <label>. A label's `for` must reference a labelable element, and these rows
            // render their value as text rather than as a control — so the association has to be
            // made on a group instead, or the name and description are simply not exposed.
            html`<span class="setting-label" id="${id}-label">${this.#labelFor(setting)}</span>`
          : html`<label class="setting-label" for=${id}>${this.#labelFor(setting)}</label>`}

        <p class="description" id=${descriptionId}>${this.#descriptionFor(setting)}</p>

        ${carriesConsequence
          ? html`<p class="consequence" id=${consequenceId}>${this.#term("timeZoneConsequence")}</p>`
          : nothing}

        ${this.#renderControl(setting, id, describedBy)}

        <!--
          WHY THIS SETTING IS NOT DOING ANYTHING, in the server's words. A setting that reports
          itself as on while the rest of the configuration stops it working describes a state the
          site does not have — and an administrator who enabled it deserves to be told, rather
          than left to discover it from a customer.

          The sentence is the server's because the reason involves another setting; a client that
          assembled it would be a second place for the explanation to drift.
        -->
        ${setting.unmetDependency
          ? html`<p class="dependency" id="${id}-dependency">${setting.unmetDependency}</p>`
          : nothing}

        ${setting.isOverridden
          ? html`
              <p class="overridden" id=${overriddenId}>
                ${setting.isConfigured !== true
                  ? this.#term("overriddenNothingConfigured")
                  : this.localize.term(
                      "ubookitSettings_overriddenConfiguredValue",
                      setting.configuredValue,
                    )}
              </p>
            `
          : nothing}

        ${fieldError
          ? html`<p class="field-error" id=${errorId} role="alert">${fieldError}</p>`
          : nothing}

        ${setting.requiresRestart
          ? html`<p class="restart">${this.#term("requiresRestart")}</p>`
          : nothing}

        ${setting.tier !== "readOnly" && setting.isOverridden
          ? html`
              <uui-button
                look="secondary"
                label=${this.#term("reset")}
                ?disabled=${this._busy.has(setting.key)}
                @click=${() => void this.#reset(setting)}
              ></uui-button>
            `
          : nothing}
      </div>
    `;
  }

  #renderControl(setting: SettingResponseModel, elementId: string, describedBy: string) {
    const value = setting.effectiveValue ?? "";

    // READ-ONLY IS NOT A DISABLED INPUT. A disabled control is skipped by keyboard navigation and
    // often unreadable to assistive technology, so a value a site is meant to be able to READ
    // would become one it could not reach. It is rendered as text instead.
    if (setting.tier === "readOnly") {
      // The describing text is associated on the surrounding group (see #renderSetting), not
      // here: aria-describedby on a non-interactive <p> is not exposed to assistive technology.
      return html`
        <p class="value" id=${elementId}>
          ${value === "" ? this.#term("notSet") : value}
        </p>
      `;
    }

    if (setting.valueKind === "boolean") {
      return html`
        <input
          type="checkbox"
          id=${elementId}
          aria-describedby=${describedBy}
          .checked=${value === "true"}
          ?disabled=${this._busy.has(setting.key)}
          @change=${(event: Event) => this.#onInput(event, setting)}
        />
      `;
    }

    return html`
      <input
        type="text"
        id=${elementId}
        aria-describedby=${describedBy}
        .value=${value}
        ?disabled=${this._busy.has(setting.key)}
        @change=${(event: Event) => this.#onInput(event, setting)}
      />
    `;
  }

  static override styles = [
    css`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
      }

      .header {
        display: flex;
        align-items: center;
        justify-content: space-between;
      }

      .intro {
        max-width: 60ch;
      }

      .settings {
        display: flex;
        flex-direction: column;
        gap: var(--uui-size-layout-1);
      }

      .setting {
        border-bottom: 1px solid var(--uui-color-divider);
        padding-bottom: var(--uui-size-space-4);
      }

      .setting-label {
        display: block;
        font-weight: bold;
      }

      .description,
      .overridden,
      .restart,
      .consequence {
        max-width: 60ch;
        margin: var(--uui-size-space-2) 0;
      }

      .consequence {
        border-left: 3px solid var(--uui-color-warning-standalone);
        padding-left: var(--uui-size-space-3);
      }

      .value {
        margin: var(--uui-size-space-2) 0;
      }

      .error,
      .field-error {
        color: var(--uui-color-danger);
      }
    `,
  ];
}

export default UBookItSettingsViewElement;

declare global {
  interface HTMLElementTagNameMap {
    "ubookit-settings-view": UBookItSettingsViewElement;
  }
}
