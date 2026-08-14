import { css, html, customElement, property, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UBookItBackofficeService } from "../api/index.js";
import type {
  CapabilityUsageModel,
  DurationExclusionModel,
  ResourceTypeUsageModel,
  ServiceDurationModel,
  ServicePreviewRequestModel,
  ServiceRequestModel,
} from "../api/index.js";
import { toApiErrors, type ApiError } from "./api-errors.js";
import "./capability-input.element.js";

/**
 * The two duration kinds, matching the wire contract exactly. "variable" means
 * the person booking chooses the length, within whichever bounds are supplied
 * and always within what the booked resource itself allows.
 */
type DurationMode = "variable" | "fixed";

/**
 * A resolution chain and the configuration it was computed for, captured
 * together.
 *
 * The pairing is the point. ⑧'s readout derived its phrasing from live form
 * state while its count was a snapshot from the previous request, so removing a
 * capability could briefly assert a sentence that was false for the number
 * beside it — into a `role="status"` region, where a screen reader announces the
 * false sentence and then the correction. Everything the summary says is read
 * from here, never from the live fields.
 */
type ResolutionSnapshot = {
  resourceType: string;
  ofType: number;
  withCapabilities: number;
  canProvide: number;
  exclusions: DurationExclusionModel[];
};

/** How many excluded resources are named before the rest are counted instead. */
const MAX_NAMED_EXCLUSIONS = 3;

/** Client-only code for the empty fixed-duration guard; rendered in the Duration group. */
const DURATION_REQUIRED = "duration-required";

/**
 * The failure fields the domain uses for duration inputs, mirrored from
 * `ServiceDuration`. Each has its own control here, so a failure carrying one
 * is rendered against that control instead of on the group.
 */
const DURATION_FIELDS = ["Duration", "Duration.Min", "Duration.Max"];

/**
 * Workspace editor for one service: Details, Service Requirements, and
 * Duration. Saves the full service; failures surface as an announced error
 * summary plus group-associated messages, and never lose form state.
 *
 * Shape notes:
 * - Requirements render as a list of exactly one role with no add/remove
 *   control, so multi-role support is an addition rather than a rewrite
 *   (design D5). `count` is not surfaced and is always sent as 1.
 * - Duration is an explicit choice, never an empty box meaning "inherit"
 *   (design D4).
 *
 * Accessibility notes follow the resource editor: uui form controls receive
 * their programmatic name via the `label` property (a visible uui-label with
 * `for` cannot pierce the component's shadow root); native inputs are labelled
 * with `label[for]` in the same shadow root; the error summary is focused on
 * failed save and group errors are associated via aria-describedby.
 */
@customElement("ubookit-service-editor")
export class UBookItServiceEditorElement extends UmbLitElement {
  @property({ type: String })
  serviceId?: string;

  @state()
  private _loading = true;

  @state()
  private _saving = false;

  @state()
  private _errors: ApiError[] = [];

  @state()
  private _name = "";

  @state()
  private _resourceType = "";

  @state()
  private _durationMode: DurationMode = "variable";

  /**
   * Null means the field is empty, not zero. Modelling empty explicitly is what
   * keeps the displayed value and the submitted value in step: coercing empty
   * to 0 submits a duration the server rejects, and silently retaining the
   * previous number submits one the user believes they cleared.
   *
   * For the variable bounds, null carries additional meaning the server acts
   * on: an empty bound defers to the booked resource's own limit.
   */
  @state()
  private _durationMinutes: number | null = 60;

  @state()
  private _durationMin: number | null = null;

  @state()
  private _durationMax: number | null = null;

  @state()
  private _knownTypes: ResourceTypeUsageModel[] = [];

  @state()
  private _requiredCapabilities: string[] = [];

  @state()
  private _knownCapabilities: CapabilityUsageModel[] = [];

  /**
   * The resolution chain for the configuration on screen, or null when that is
   * not currently known.
   *
   * Null is not a chain of zeros, and the distinction is the whole point: zero
   * is the number that says "your configuration resolves to nothing, go and fix
   * it", so showing it because a request failed would send someone to correct a
   * configuration that is fine. This summary exists precisely to be believed, so
   * it must be silent rather than wrong (design D7).
   */
  @state()
  private _resolution: ResolutionSnapshot | null = null;

  /** Guards against an earlier in-flight preview overwriting a later one. */
  #previewToken = 0;

  /** Pending debounce for the fields that change a character at a time. */
  #previewDebounce?: ReturnType<typeof setTimeout>;

  #term(key: string) {
    return this.localize.term(`ubookitServices_${key}`);
  }

  override connectedCallback() {
    super.connectedCallback();
    void this.#load();
  }

  async #load() {
    // The picker is a convenience: if the type list cannot be fetched the
    // editor still works as a free-text field, so its failure is not surfaced
    // as a save-blocking error.
    void this.#loadTypes();
    void this.#loadCapabilities();

    if (!this.serviceId) {
      this._loading = false;
      // A new service starts with an empty configuration, and the summary should
      // describe it from the outset rather than after the first keystroke. With
      // no resource type yet it will say nothing, which is the correct answer.
      void this.#refreshResolution();
      return;
    }

    let data;
    try {
      const response = await UBookItBackofficeService.getService({ path: { id: this.serviceId } });
      data = response.data;
      if (response.error || !data) {
        this._errors = toApiErrors(response.error, this.#term("serviceLoadFailed"));
        this._loading = false;
        return;
      }
    } catch (thrown) {
      this._errors = toApiErrors(thrown, this.#term("serviceLoadFailed"));
      this._loading = false;
      return;
    }

    this._name = data.name;
    this._resourceType = data.roles[0]?.resourceType ?? "";
    this._requiredCapabilities = [...(data.roles[0]?.requiredCapabilities ?? [])];

    if (data.duration?.kind === "fixed") {
      this._durationMode = "fixed";
      this._durationMinutes = data.duration.minutes ?? null;
    } else {
      this._durationMode = "variable";
      this._durationMin = data.duration?.minMinutes ?? null;
      this._durationMax = data.duration?.maxMinutes ?? null;
    }

    // After the duration is applied, not before: the duration is now one of the
    // summary's inputs, so resolving mid-load would report the chain for the
    // default duration rather than the saved one.
    void this.#refreshResolution();

    this._loading = false;
  }

  async #loadTypes() {
    try {
      const { data, error } = await UBookItBackofficeService.listResourceTypes();
      if (error || !data) {
        return;
      }

      this._knownTypes = data;
    } catch {
      // The field degrades to plain free text with no suggestions. Whether a
      // type matches anything is answered by the requirement readout, which
      // reports "not known" rather than "none" when its own lookup fails.
    }
  }

  async #loadCapabilities() {
    try {
      const { data, error } = await UBookItBackofficeService.listCapabilities();
      if (error || !data) {
        return;
      }

      this._knownCapabilities = data;
    } catch {
      // Suggestions only; the control still accepts free text.
    }
  }

  /**
   * Debounced entry point for the free-text fields — the resource type and the
   * duration numbers, which change a character at a time. Typing a
   * ten-character key would otherwise be ten requests, most of them describing
   * a prefix nobody asked about. Discrete choices (adding a capability,
   * switching duration mode) call {@link #refreshResolution} directly.
   */
  #scheduleResolutionRefresh() {
    clearTimeout(this.#previewDebounce);
    this.#previewDebounce = setTimeout(() => void this.#refreshResolution(), 250);
  }

  override disconnectedCallback() {
    clearTimeout(this.#previewDebounce);
    super.disconnectedCallback();
  }

  /**
   * The configuration as currently entered, or null when it is too incomplete
   * to resolve — no resource type, or a fixed duration with no length. Those are
   * unfinished, not broken, and a chain of zeros would say the opposite.
   */
  #buildPreviewRequest(): ServicePreviewRequestModel | null {
    const resourceType = this._resourceType.trim();
    if (resourceType === "") {
      return null;
    }

    if (this._durationMode === "fixed" && this._durationMinutes === null) {
      return null;
    }

    return {
      resourceType,
      requiredCapabilities: [...this._requiredCapabilities],
      duration: this.#buildDuration(),
    };
  }

  /**
   * Recomputes the resolution chain for the configuration as currently entered.
   *
   * Server-side rather than filtered from a local resource list, because the
   * answer must come from the same resolution the booking path runs. A summary
   * computed by a second, browser-side implementation of eligibility could
   * disagree with the booker — and it would disagree precisely in the case it
   * exists to detect, which is worse than showing nothing.
   */
  async #refreshResolution() {
    const token = ++this.#previewToken;

    // Captured with the request, not read at render: by the time the answer
    // arrives the live form may already describe something else, and the chain
    // must be phrased for the configuration it actually resolved.
    const body = this.#buildPreviewRequest();

    if (body === null) {
      this._resolution = null;
      return;
    }

    try {
      const { data, error } = await UBookItBackofficeService.previewServiceConfiguration({ body });

      // A later edit has already superseded this request; its answer describes a
      // configuration that is no longer on screen.
      if (token !== this.#previewToken) {
        return;
      }

      this._resolution =
        error || !data
          ? null
          : {
              resourceType: body.resourceType,
              ofType: data.ofType.total,
              withCapabilities: data.withCapabilities.total,
              canProvide: data.canProvide.total,
              exclusions: data.durationExclusions,
            };
    } catch {
      if (token === this.#previewToken) {
        this._resolution = null;
      }
    }
  }

  #buildDuration(): ServiceDurationModel {
    return this._durationMode === "fixed"
      ? { kind: "fixed", minutes: this._durationMinutes }
      : { kind: "variable", minMinutes: this._durationMin, maxMinutes: this._durationMax };
  }

  #buildRequest(): ServiceRequestModel {
    return {
      name: this._name,
      // The kind is always explicit: the server rejects an absent or unknown
      // one rather than guessing, so the stored duration is always the one
      // chosen on screen. Shared with the preview so the summary can never
      // describe a different duration from the one a save would send.
      duration: this.#buildDuration(),
      // Exactly one role, count fixed at 1 in v1 (design D5). Trimmed to match
      // what the hint evaluates: otherwise " room " looks known (hint hidden)
      // but is rejected server-side as an invalid type key.
      roles: [
        {
          resourceType: this._resourceType.trim(),
          requiredCapabilities: [...this._requiredCapabilities],
          count: 1,
        },
      ],
    };
  }

  async #save(event: Event) {
    event.preventDefault();
    this._saving = true;
    this._errors = [];

    // Client-side guard for a state only the UI can express: "fixed duration"
    // chosen with the field emptied. There is no server code for it — sending
    // null would silently mean "inherit", quietly contradicting the choice on
    // screen — so it is caught here, mirroring the resource editor's
    // empty-exception-date guard.
    if (this._durationMode === "fixed" && this._durationMinutes === null) {
      this._errors = [{ code: DURATION_REQUIRED, message: this.#term("durationRequired") }];
      this._saving = false;
      await this.updateComplete;
      this.shadowRoot?.querySelector<HTMLElement>("#error-summary")?.focus();
      return;
    }

    const body = this.#buildRequest();
    try {
      const result = this.serviceId
        ? await UBookItBackofficeService.updateService({ path: { id: this.serviceId }, body })
        : await UBookItBackofficeService.createService({ body });

      if (result.error) {
        this._errors = toApiErrors(result.error, this.#term("serviceSaveFailed"));
        return;
      }
    } catch (thrown) {
      this._errors = toApiErrors(thrown, this.#term("serviceSaveFailed"));
      return;
    } finally {
      this._saving = false;
      if (this._errors.length > 0) {
        await this.updateComplete;
        this.shadowRoot?.querySelector<HTMLElement>("#error-summary")?.focus();
      }
    }

    this.dispatchEvent(new CustomEvent("ubookit-saved"));
  }

  #errorsFor(...codes: string[]): ApiError[] {
    return this._errors.filter((e) => e.code !== undefined && codes.includes(e.code));
  }

  override render() {
    if (this._loading) {
      return html`<uui-loader-bar aria-label=${this.#term("loadingService")}></uui-loader-bar>`;
    }

    return html`
      <uui-button
        look="secondary"
        label=${this.#term("back")}
        @click=${() => this.dispatchEvent(new CustomEvent("ubookit-close"))}
      ></uui-button>

      <h2>${this.serviceId ? this._name || this.#term("edit") : this.#term("newService")}</h2>

      ${this.#renderErrorSummary()}

      <form @submit=${this.#save} novalidate>
        ${this.#renderResolutionSummary()} ${this.#renderDetails()} ${this.#renderRequirements()}
        ${this.#renderDuration()}

        <div class="actions">
          <uui-button
            type="submit"
            look="primary"
            label=${this.#term("save")}
            state=${this._saving ? "waiting" : nothing}
          ></uui-button>
          <uui-button
            look="secondary"
            label=${this.#term("cancel")}
            @click=${() => this.dispatchEvent(new CustomEvent("ubookit-close"))}
          ></uui-button>
        </div>
      </form>
    `;
  }

  #renderErrorSummary() {
    if (this._errors.length === 0) return nothing;

    return html`
      <div id="error-summary" role="alert" tabindex="-1" class="error-summary">
        <strong>${this.#term("errorSummary")}</strong>
        <ul>
          ${this._errors.map((e) => html`<li>${e.message ?? e.code}</li>`)}
        </ul>
      </div>
    `;
  }

  #renderGroupErrors(groupId: string, ...codes: string[]) {
    const errors = this.#errorsFor(...codes);
    return errors.length === 0
      ? nothing
      : html`<p class="group-error" id=${groupId}>${errors.map((e) => e.message).join(" ")}</p>`;
  }

  #renderDetails() {
    // The fieldset carries the association: a group error is announced with the
    // control it belongs to, rather than only appearing in the summary. Without
    // it the rendered id has no referrer and the association silently does not
    // exist (QA finding).
    const described = this.#errorsFor("service-name-required").length > 0;

    return html`
      <uui-box headline=${this.#term("details")}>
        ${this.#renderGroupErrors("err-details", "service-name-required")}
        <fieldset aria-describedby=${described ? "err-details" : nothing}>
          <legend class="visually-hidden">${this.#term("details")}</legend>
          <div class="field">
            <uui-label for="service-name" required>${this.#term("name")}</uui-label>
            <uui-input
              id="service-name"
              label=${this.#term("name")}
              .value=${this._name}
              @input=${(e: InputEvent) => (this._name = (e.target as HTMLInputElement).value)}
            ></uui-input>
          </div>
        </fieldset>
      </uui-box>
    `;
  }

  #renderRequirements() {
    const hasGroupError = this.#errorsFor("service-role-invalid", "type-key-invalid").length > 0;

    // A native input with a datalist, not uui-combobox: the uui control's
    // value must match one of its options, which would block naming a type
    // before any resource has it — a legitimate setup order (design D2).
    return html`
      <uui-box headline=${this.#term("requirements")}>
        ${this.#renderGroupErrors("err-requirements", "service-role-invalid", "type-key-invalid")}
        <fieldset aria-describedby=${hasGroupError ? "err-requirements" : nothing}>
          <legend class="visually-hidden">${this.#term("requirements")}</legend>
          <div class="field">
            <label for="service-resource-type">${this.#term("resourceType")}</label>
            <input
              id="service-resource-type"
              list="ubookit-resource-types"
              .value=${this._resourceType}
              aria-describedby="resource-type-hint"
              @input=${(e: InputEvent) => {
                this._resourceType = (e.target as HTMLInputElement).value;
                this.#scheduleResolutionRefresh();
              }}
            />
            <datalist id="ubookit-resource-types">
              ${this._knownTypes.map((t) => html`<option value=${t.type}></option>`)}
            </datalist>
            <p id="resource-type-hint" class="hint">${this.#term("resourceTypeHint")}</p>
          </div>

          <ubookit-capability-input
            controlId="role-capabilities"
            .capabilities=${this._requiredCapabilities}
            .known=${this._knownCapabilities}
            label=${this.#term("requiredCapabilities")}
            addLabel=${this.#term("capabilityAdd")}
            hint=${this.#term("requiredCapabilitiesHint")}
            removeLabel=${this.#term("capabilityRemove")}
            emptyLabel=${this.#term("requiredCapabilitiesNone")}
            error=${this.#errorsFor("capability-key-invalid")
              .map((e) => e.message)
              .join(" ")}
            @ubookit-capabilities-changed=${(e: CustomEvent<{ capabilities: string[] }>) => {
              this._requiredCapabilities = e.detail.capabilities;
              // A discrete choice, not a keystroke: no debounce.
              void this.#refreshResolution();
            }}
          ></ubookit-capability-input>
        </fieldset>
      </uui-box>
    `;
  }

  /**
   * The resolution summary, at form level above the groups (design D6).
   *
   * It is above rather than inside Service Requirements because its inputs now
   * span both that group and Duration: an editor who sets a four-hour duration
   * must not have to scroll back to Requirements to discover that doing so
   * emptied the pool.
   *
   * The live region is always present and only its content changes. A
   * `role="status"` element inserted at the same moment as its text is
   * frequently not announced, because the region has to already be observed when
   * the change happens.
   */
  #renderResolutionSummary() {
    const lines = this.#resolutionLines();

    return html`
      <div class="resolution" role="status" aria-label=${this.#term("resolutionSummary")}>
        ${lines.length === 0
          ? nothing
          : html`<ul>
              ${lines.map((line) => html`<li>${line}</li>`)}
            </ul>`}
      </div>
    `;
  }

  /**
   * What the summary says, derived entirely from the snapshot — never from the
   * live form (design D7).
   *
   * An empty array is silence, and silence is what "not known" looks like: no
   * resource type entered yet, a fixed duration with no length, or a request
   * that failed. Rendering a chain of zeros instead would tell an editor their
   * configuration resolves to nothing, which is the one thing a failed request
   * does not know.
   *
   * The chain stops at the stage that emptied the pool. Continuing past it would
   * print "None of those…" about a stage that had nothing to filter, which is
   * how ⑧ managed to blame the capabilities for a mistyped type key.
   */
  #resolutionLines(): string[] {
    const chain = this._resolution;
    if (chain === null) {
      return [];
    }

    if (chain.ofType === 0) {
      return [this.localize.term("ubookitServices_resolutionTypeNone", chain.resourceType)];
    }

    // Nothing was excluded anywhere. Three lines carrying one number say less
    // than one line does, so the healthy case collapses.
    if (chain.ofType === chain.canProvide) {
      return [
        chain.canProvide === 1
          ? this.#term("resolutionHealthyOne")
          : this.localize.term("ubookitServices_resolutionHealthy", chain.canProvide),
      ];
    }

    const lines = [
      chain.ofType === 1
        ? this.localize.term("ubookitServices_resolutionTypeOne", chain.resourceType)
        : this.localize.term("ubookitServices_resolutionType", chain.ofType, chain.resourceType),
    ];

    if (chain.withCapabilities === 0) {
      lines.push(this.#term("resolutionCapabilitiesNone"));
      return lines;
    }

    lines.push(
      chain.withCapabilities === 1
        ? this.#term("resolutionCapabilitiesOne")
        : this.localize.term("ubookitServices_resolutionCapabilities", chain.withCapabilities),
    );

    lines.push(
      chain.canProvide === 0
        ? this.#term("resolutionDurationNone")
        : chain.canProvide === 1
          ? this.#term("resolutionDurationOne")
          : this.localize.term("ubookitServices_resolutionDuration", chain.canProvide),
    );

    if (chain.exclusions.length > 0) {
      lines.push(
        this.localize.term("ubookitServices_resolutionExcluded", this.#excludedNames(chain.exclusions)),
      );
    }

    return lines;
  }

  /**
   * The excluded resources with the bound that excluded each — the number the
   * editor has to change. Capped, because a wide pool with a low ceiling would
   * otherwise produce a list longer than the form.
   */
  #excludedNames(exclusions: DurationExclusionModel[]): string {
    const named = exclusions.slice(0, MAX_NAMED_EXCLUSIONS).map((exclusion) => {
      const key =
        exclusion.reason === "resource-minimum"
          ? "ubookitServices_resolutionExcludedMinimum"
          : exclusion.reason === "granularity"
            ? "ubookitServices_resolutionExcludedGranularity"
            : "ubookitServices_resolutionExcludedMaximum";

      return this.localize.term(key, exclusion.displayName, exclusion.boundMinutes);
    });

    const remaining = exclusions.length - named.length;
    if (remaining > 0) {
      named.push(this.localize.term("ubookitServices_resolutionExcludedMore", remaining));
    }

    return named.join(", ");
  }

  #renderDuration() {
    // Only failures the server did NOT attribute to a specific input belong on
    // the group: rendering an attributed one here as well would announce the
    // same message twice, once without saying which control it means.
    const unattributed = this.#unattributedDurationErrors();

    return html`
      <uui-box headline=${this.#term("duration")}>
        ${unattributed.length === 0
          ? nothing
          : html`<p class="group-error" id="err-duration">
              ${unattributed.map((e) => e.message).join(" ")}
            </p>`}
        <fieldset aria-describedby=${unattributed.length > 0 ? "err-duration" : nothing}>
          <legend class="visually-hidden">${this.#term("duration")}</legend>
          <!--
            Named "Duration mode" rather than "Duration": the fieldset legend
            already contributes "Duration", and repeating it here makes the
            group announce its name twice. A label attribute is not used —
            uui-radio-group has no LabelMixin, so it would be inert.
          -->
          <uui-radio-group
            aria-label=${this.#term("durationMode")}
            .value=${this._durationMode}
            @change=${(e: Event) => {
              this._durationMode = (e.target as HTMLInputElement).value as DurationMode;
              // A discrete choice: refresh immediately. Switching to a fixed
              // length with no minutes entered resolves to "not known", which
              // renders as silence rather than as a chain of zeros.
              void this.#refreshResolution();
            }}
          >
            <uui-radio value="fixed" label=${this.#term("durationFixed")}></uui-radio>
            <uui-radio value="variable" label=${this.#term("durationVariable")}></uui-radio>
          </uui-radio-group>

          <p class="hint">${this.#term("durationVariableHint")}</p>

          <div class="field">
            <label for="service-duration">${this.#term("durationMinutes")}</label>
            <input
              id="service-duration"
              type="number"
              min="1"
              .value=${this._durationMinutes === null ? "" : String(this._durationMinutes)}
              ?disabled=${this._durationMode !== "fixed"}
              aria-invalid=${this.#boundInvalid("Duration") ? "true" : nothing}
              aria-describedby=${this.#boundInvalid("Duration") ? "err-duration-length" : nothing}
              @input=${(e: InputEvent) => {
                this._durationMinutes = this.#readNumber(e);
                this.#scheduleResolutionRefresh();
              }}
            />
            ${this.#renderBoundError("err-duration-length", "Duration")}
          </div>

          <div class="field">
            <label for="service-duration-min">${this.#term("durationMin")}</label>
            <input
              id="service-duration-min"
              type="number"
              min="1"
              .value=${this._durationMin === null ? "" : String(this._durationMin)}
              ?disabled=${this._durationMode !== "variable"}
              aria-invalid=${this.#boundInvalid("Duration.Min") ? "true" : nothing}
              aria-describedby=${this.#boundInvalid("Duration.Min") ? "err-duration-min" : nothing}
              @input=${(e: InputEvent) => {
                this._durationMin = this.#readNumber(e);
                this.#scheduleResolutionRefresh();
              }}
            />
            ${this.#renderBoundError("err-duration-min", "Duration.Min")}
          </div>

          <div class="field">
            <label for="service-duration-max">${this.#term("durationMax")}</label>
            <input
              id="service-duration-max"
              type="number"
              min="1"
              .value=${this._durationMax === null ? "" : String(this._durationMax)}
              ?disabled=${this._durationMode !== "variable"}
              aria-invalid=${this.#boundInvalid("Duration.Max") ? "true" : nothing}
              aria-describedby=${this.#boundInvalid("Duration.Max") ? "err-duration-max" : nothing}
              @input=${(e: InputEvent) => {
                this._durationMax = this.#readNumber(e);
                this.#scheduleResolutionRefresh();
              }}
            />
            ${this.#renderBoundError("err-duration-max", "Duration.Max")}
          </div>
        </fieldset>
      </uui-box>
    `;
  }

  /**
   * `Number("")` is 0, so binding the coerced value straight back would repaint
   * "0" under the cursor and make the field impossible to clear and retype.
   * Empty is recorded as null instead — for the variable bounds that is also
   * the value the server acts on, meaning "use the resource's own limit".
   */
  #readNumber(event: InputEvent): number | null {
    const raw = (event.target as HTMLInputElement).value;
    return raw === "" ? null : Number(raw);
  }

  /** Duration failures the server attributed to a specific input. */
  #errorsForField(field: string): ApiError[] {
    return this._errors.filter((e) => e.code === "service-duration-invalid" && e.field === field);
  }

  /**
   * Duration failures with no field, or a field this editor has no control for
   * — those still need saying somewhere, so they stay on the group rather than
   * vanishing.
   */
  #unattributedDurationErrors(): ApiError[] {
    return this.#errorsFor("service-duration-invalid", DURATION_REQUIRED).filter(
      (e) => e.field === undefined || e.field === null || !DURATION_FIELDS.includes(e.field),
    );
  }

  #boundInvalid(field: string): boolean {
    return this.#errorsForField(field).length > 0;
  }

  /**
   * Renders a duration failure against the input it belongs to, so a screen
   * reader announces which control to correct rather than only that something
   * in the group is wrong.
   */
  #renderBoundError(id: string, field: string) {
    const errors = this.#errorsForField(field);
    return errors.length === 0
      ? nothing
      : html`<p class="group-error" id=${id}>${errors.map((e) => e.message).join(" ")}</p>`;
  }

  static override styles = css`
    uui-box {
      margin-top: var(--uui-size-space-4);
    }
    .field {
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-1);
      margin-bottom: var(--uui-size-space-4);
      max-width: 400px;
    }
    fieldset {
      border: 1px solid var(--uui-color-border, #d8d7d9);
      border-radius: var(--uui-border-radius, 3px);
      margin-bottom: var(--uui-size-space-3);
      padding: var(--uui-size-space-4);
    }
    .hint {
      color: var(--uui-color-text-alt, #515054);
      font-size: var(--uui-type-small-size, 0.8rem);
      margin: 0;
    }
    /*
      The live region is always in the DOM so a screen reader is already
      observing it when its content changes; when it has nothing to say it
      simply collapses, contributing no margin or border of its own.
    */
    .resolution ul {
      border-left: 3px solid var(--uui-color-border, #d8d7d9);
      color: var(--uui-color-text-alt, #515054);
      font-size: var(--uui-type-small-size, 0.8rem);
      list-style: none;
      margin: var(--uui-size-space-4) 0 0;
      padding: 0 0 0 var(--uui-size-space-4);
    }
    .resolution li + li {
      margin-top: var(--uui-size-space-1);
    }
    .error-summary {
      border: 2px solid var(--uui-color-danger, #d42054);
      padding: var(--uui-size-space-4);
      margin: var(--uui-size-space-4) 0;
    }
    .group-error {
      color: var(--uui-color-danger, #d42054);
    }
    .actions {
      display: flex;
      gap: var(--uui-size-space-3);
      margin-top: var(--uui-size-space-5);
    }
    .visually-hidden {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip: rect(0 0 0 0);
      white-space: nowrap;
    }
  `;
}

export default UBookItServiceEditorElement;

declare global {
  interface HTMLElementTagNameMap {
    "ubookit-service-editor": UBookItServiceEditorElement;
  }
}
