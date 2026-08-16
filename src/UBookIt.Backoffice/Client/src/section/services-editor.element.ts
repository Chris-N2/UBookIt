import { css, html, customElement, property, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UBookItBackofficeService } from "../api/index.js";
import type {
  CapabilityUsageModel,
  ResourceTypeUsageModel,
  ServiceDurationModel,
  ServicePreviewRequestModel,
  ServiceRequestModel,
} from "../api/index.js";
import { toApiErrors, type ApiError } from "./api-errors.js";
import { resolutionGroups, type ResolutionSnapshot } from "./resolution-summary.js";
import { alignmentReport, type AlignmentSnapshot } from "./alignment-report.js";
import "./capability-input.element.js";

/**
 * One requirement row: the resource type a role needs, the capabilities a
 * resource must carry to fill it, and how many distinct resources it takes at
 * once.
 */
type RoleRow = {
  resourceType: string;
  requiredCapabilities: string[];

  /**
   * How many distinct resources of this requirement a booking needs at once.
   * Defaults to 1, which is what every service expressed before counts existed.
   */
  count: number;
};

/**
 * The two duration kinds, matching the wire contract exactly. "variable" means
 * the person booking chooses the length, within whichever bounds are supplied
 * and always within what the booked resource itself allows.
 */
type DurationMode = "variable" | "fixed";


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
 * - Requirements render as one row per role, with add and remove; a service
 *   always keeps at least one, so the last row offers no remove control.
 *   `count` is surfaced per row, defaulting to 1.
 * - Resource types are NOT filtered against the rows already using them. The
 *   duplicate-type rule is the server's, and enforcing it here as well would
 *   make relaxing it later a change in two places (multi-role design D1).
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

  /**
   * The service's roles, one per requirement row. Always at least one: a service
   * with no roles resolves to nothing, and the server rejects it.
   *
   * Replaced wholesale on every edit rather than mutated in place, because Lit
   * only re-renders on identity change for an array-valued state.
   */
  @state()
  private _roles: RoleRow[] = [{ resourceType: "", requiredCapabilities: [], count: 1 }];

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
  private _knownCapabilities: CapabilityUsageModel[] = [];

  /**
   * The resolution chain for each role of the configuration on screen, or null
   * when that is not currently known.
   *
   * Null is not a chain of zeros, and the distinction is the whole point: zero
   * is the number that says "your configuration resolves to nothing, go and fix
   * it", so showing it because a request failed would send someone to correct a
   * configuration that is fine. This summary exists precisely to be believed, so
   * it must be silent rather than wrong (design D7).
   */
  @state()
  private _resolution: ResolutionSnapshot[] | null = null;

  /**
   * The two roles whose start times can never coincide, when the configuration
   * has such a pair — otherwise null.
   *
   * Null covers both "they can align" and "not known", and that conflation is
   * deliberate: neither is a claim that the service can be booked, and the only
   * thing this state is ever allowed to assert is the impossibility. Kept beside
   * `_resolution` rather than inside it because misalignment is a property of a
   * PAIR of roles and belongs to neither chain (design D6).
   */
  @state()
  private _alignment: AlignmentSnapshot | null = null;

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
    this._roles =
      data.roles.length > 0
        ? data.roles.map((role) => ({
            resourceType: role.resourceType,
            requiredCapabilities: [...(role.requiredCapabilities ?? [])],
            // A server that omitted the count would leave the row unusable, and
            // 1 is the only value a service saved before counts existed can have.
            count: role.count ?? 1,
          }))
        : [{ resourceType: "", requiredCapabilities: [], count: 1 }];

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
   * to resolve — no role with a resource type yet, or a fixed duration with no
   * length. Those are unfinished, not broken, and a chain of zeros would say the
   * opposite.
   *
   * A row whose type is still empty is omitted rather than silencing the whole
   * report: the other roles' chains are known and still true, and saying nothing
   * about the unfinished row is exactly what "not known" looks like. A duration
   * that cannot be resolved does silence everything, because it is an input to
   * every role's chain.
   */
  #buildPreviewRequest(): ServicePreviewRequestModel | null {
    if (this._durationMode === "fixed" && this._durationMinutes === null) {
      return null;
    }

    const roles = this._roles
      .filter((role) => role.resourceType.trim() !== "")
      .map((role) => ({
        resourceType: role.resourceType.trim(),
        requiredCapabilities: [...role.requiredCapabilities],
      }));

    return roles.length === 0 ? null : { roles, duration: this.#buildDuration() };
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
      this._alignment = null;
      return;
    }

    try {
      const { data, error } = await UBookItBackofficeService.previewServiceConfiguration({ body });

      // A later edit has already superseded this request; its answer describes a
      // configuration that is no longer on screen.
      if (token !== this.#previewToken) {
        return;
      }

      // Each chain is phrased from what came back with it — the response echoes
      // the role it describes — rather than from the form, which may already
      // describe something else by the time the answer arrives.
      this._resolution =
        error || !data
          ? null
          : data.roles.map((chain) => ({
              resourceType: chain.resourceType,
              requiresCapabilities: (chain.requiredCapabilities ?? []).length > 0,
              ofType: chain.ofType.total,
              withCapabilities: chain.withCapabilities.total,
              canProvide: chain.canProvide.total,
              exclusions: chain.durationExclusions,
            }));

      // Captured from the SAME response as the chains beside it, so the report
      // and the counts always describe one configuration. A finding read from a
      // later response than the chains could name a resource no longer in the
      // pool the chains counted.
      //
      // Absent means only that no permanent misalignment was found — never that
      // the roles align, and never that the service can be booked — so it is
      // stored as the same null a failed request produces.
      const finding = error || !data ? null : data.startMisalignment;

      this._alignment = finding
        ? {
            first: {
              resourceType: finding.first.resourceType,
              displayName: finding.first.displayName,
              windowStart: finding.first.windowStart,
              granularityMinutes: finding.first.granularityMinutes,
            },
            second: {
              resourceType: finding.second.resourceType,
              displayName: finding.second.displayName,
              windowStart: finding.second.windowStart,
              granularityMinutes: finding.second.granularityMinutes,
            },
          }
        : null;
    } catch {
      if (token === this.#previewToken) {
        this._resolution = null;
        this._alignment = null;
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
      // Every row, in the order shown, each carrying its own count.
      // Trimmed to match what the hint evaluates: otherwise " room " looks known
      // (hint hidden) but is rejected server-side as an invalid type key.
      //
      // Rows are sent exactly as entered, including two naming the same type:
      // the duplicate rule is the server's, and enforcing it here as well would
      // make relaxing it later a change in two places (design D1).
      roles: this._roles.map((role) => ({
        resourceType: role.resourceType.trim(),
        requiredCapabilities: [...role.requiredCapabilities],
        count: role.count,
      })),
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
        ${this.#renderResolutionSummary()} ${this.#renderAlignmentReport()} ${this.#renderDetails()}
        ${this.#renderRequirements()}
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

  /**
   * The failure field the server attributes to one role's control, mirrored from
   * `ServiceRole.FieldFor`. Without the index every message would land on the
   * first row.
   */
  #roleField(index: number, field: string): string {
    return `Roles[${index}].${field}`;
  }

  #errorsForRole(index: number, ...codes: string[]): ApiError[] {
    return this._errors.filter(
      (e) =>
        e.code !== undefined &&
        codes.includes(e.code) &&
        (e.field === this.#roleField(index, "ResourceType") ||
          e.field === this.#roleField(index, "RequiredCapabilities") ||
          e.field === this.#roleField(index, "Count")),
    );
  }

  /**
   * Role failures the server did not attribute to any row — or attributed to a
   * row this form no longer shows. They still need saying somewhere, so they
   * stay on the group rather than vanishing.
   */
  #unattributedRoleErrors(): ApiError[] {
    const attributed = new Set(
      this._roles.flatMap((_, index) => [
        this.#roleField(index, "ResourceType"),
        this.#roleField(index, "RequiredCapabilities"),
        this.#roleField(index, "Count"),
      ]),
    );

    return this.#errorsFor(
      "service-role-invalid",
      "type-key-invalid",
      "service-role-duplicate-type",
    ).filter((e) => e.field === undefined || e.field === null || !attributed.has(e.field));
  }

  #renderRequirements() {
    const unattributed = this.#unattributedRoleErrors();

    return html`
      <uui-box headline=${this.#term("requirements")}>
        ${unattributed.length === 0
          ? nothing
          : html`<p class="group-error" id="err-requirements">
              ${unattributed.map((e) => e.message).join(" ")}
            </p>`}

        ${this._roles.map((role, index) => this.#renderRequirementRow(role, index))}

        <datalist id="ubookit-resource-types">
          ${this._knownTypes.map((t) => html`<option value=${t.type}></option>`)}
        </datalist>

        <uui-button
          id="add-requirement"
          look="secondary"
          label=${this.#term("requirementAdd")}
          @click=${() => this.#addRole()}
        ></uui-button>
      </uui-box>
    `;
  }

  /**
   * One requirement row.
   *
   * A fieldset per row, so its legend names the group a screen reader announces
   * before each control: three controls all called "Resource type" would
   * otherwise be indistinguishable. Ids carry the row index for the same reason
   * — a `label[for]` and an `aria-describedby` must resolve within this shadow
   * root, and duplicated ids resolve to the first row for every row.
   *
   * A native input with a datalist, not uui-combobox: the uui control's value
   * must match one of its options, which would block naming a type before any
   * resource has it — a legitimate setup order (design D2).
   */
  #renderRequirementRow(role: RoleRow, index: number) {
    const typeErrors = this.#errorsForRole(index, "type-key-invalid", "service-role-duplicate-type");
    const errorId = `err-requirement-${index}`;
    const hintId = `resource-type-hint-${index}`;

    // The count's own error and hint ids. Distinct from the type control's, so
    // `aria-describedby` on each control resolves to that control's own message
    // — a shared id would read the type's failure out against the count.
    const countErrors = this.#errorsForRole(index, "service-role-count-invalid");
    const countErrorId = `err-requirement-count-${index}`;
    const countHintId = `role-count-hint-${index}`;

    return html`
      <fieldset class="requirement">
        <legend>${this.localize.term("ubookitServices_requirementLegend", index + 1)}</legend>

        <div class="field">
          <label for="service-resource-type-${index}">${this.#term("resourceType")}</label>
          <input
            id="service-resource-type-${index}"
            list="ubookit-resource-types"
            .value=${role.resourceType}
            aria-invalid=${typeErrors.length > 0 ? "true" : nothing}
            aria-describedby=${typeErrors.length > 0 ? `${hintId} ${errorId}` : hintId}
            @input=${(e: InputEvent) => {
              this.#updateRole(index, { resourceType: (e.target as HTMLInputElement).value });
              this.#scheduleResolutionRefresh();
            }}
          />
          <p id=${hintId} class="hint">${this.#term("resourceTypeHint")}</p>
          ${typeErrors.length === 0
            ? nothing
            : html`<p class="group-error" id=${errorId}>
                ${typeErrors.map((e) => e.message).join(" ")}
              </p>`}
        </div>

        <ubookit-capability-input
          controlId="role-capabilities-${index}"
          .capabilities=${role.requiredCapabilities}
          .known=${this._knownCapabilities}
          label=${this.#term("requiredCapabilities")}
          addLabel=${this.#term("capabilityAdd")}
          hint=${this.#term("requiredCapabilitiesHint")}
          removeLabel=${this.#term("capabilityRemove")}
          emptyLabel=${this.#term("requiredCapabilitiesNone")}
          error=${this.#errorsForRole(index, "capability-key-invalid")
            .map((e) => e.message)
            .join(" ")}
          @ubookit-capabilities-changed=${(e: CustomEvent<{ capabilities: string[] }>) => {
            this.#updateRole(index, { requiredCapabilities: e.detail.capabilities });
            // A discrete choice, not a keystroke: no debounce.
            void this.#refreshResolution();
          }}
        ></ubookit-capability-input>

        <div class="field">
          <label for="role-count-${index}">${this.#term("requirementCount")}</label>
          <input
            id="role-count-${index}"
            type="number"
            min="1"
            step="1"
            inputmode="numeric"
            .value=${String(role.count)}
            aria-invalid=${countErrors.length > 0 ? "true" : nothing}
            aria-describedby=${countErrors.length > 0 ? `${countHintId} ${countErrorId}` : countHintId}
            @input=${(e: InputEvent) => {
              const parsed = Number.parseInt((e.target as HTMLInputElement).value, 10);

              // A field that does not currently parse — cleared, ready to be
              // retyped — leaves the stored count alone rather than substituting
              // 1. Writing 1 here would change `role.count`, and because `.value`
              // is bound to it Lit would re-commit "1" into the element the user
              // had just emptied, under their cursor: backspacing "2" to type
              // "20" produced "120". Leaving state untouched means the binding
              // sees no change and does not fight the user.
              //
              // The server owns the bound and reports it against this control, so
              // nothing here needs to judge the number itself.
              if (!Number.isNaN(parsed)) {
                this.#updateRole(index, { count: parsed });
              }
            }}
            @blur=${(e: FocusEvent) => {
              // Display and state reconverge when the control is left. Without
              // this a field abandoned while empty would keep showing nothing
              // while the row still holds — and would save — its previous count,
              // which is a quieter lie than the one above but still a lie.
              const el = e.target as HTMLInputElement;
              if (Number.isNaN(Number.parseInt(el.value, 10))) {
                el.value = String(role.count);
              }
            }}
          />
          <p id=${countHintId} class="hint">${this.#term("requirementCountHint")}</p>
          ${countErrors.length === 0
            ? nothing
            : html`<p class="group-error" id=${countErrorId}>
                ${countErrors.map((e) => e.message).join(" ")}
              </p>`}
        </div>

        <!--
          Offered only while more than one row exists: a service always keeps at
          least one role, and a control that removes the last one would either
          fail on save or need the form to invent a replacement.
        -->
        ${this._roles.length === 1
          ? nothing
          : html`<uui-button
              class="remove-requirement"
              data-index=${index}
              look="secondary"
              color="danger"
              label=${this.localize.term("ubookitServices_requirementRemove", index + 1)}
              @click=${() => void this.#removeRole(index)}
            ></uui-button>`}
      </fieldset>
    `;
  }

  #updateRole(index: number, changes: Partial<RoleRow>) {
    this._roles = this._roles.map((role, i) => (i === index ? { ...role, ...changes } : role));
  }

  #addRole() {
    this._roles = [...this._roles, { resourceType: "", requiredCapabilities: [], count: 1 }];
    this.#dropRoleErrors();
  }

  /**
   * Discards role-attributed failures whenever the rows are re-indexed.
   *
   * A failure carries a row index (`Roles[2].ResourceType`). Adding or removing
   * a row moves every later row to a different index, so a message left behind
   * would render against whatever row now occupies that position — a server
   * message on the wrong control, which is worse than no message.
   *
   * They leave the error summary too, since that reads the same list. That is
   * the right trade: the summary would otherwise keep asserting a failure about
   * a row the user has just changed or removed, and the next save re-reports
   * whatever is still wrong. Failures not attributed to a row are untouched.
   */
  #dropRoleErrors() {
    this._errors = this._errors.filter(
      (e) => !(e.field ?? "").startsWith("Roles["),
    );
  }

  /**
   * Removes a row and puts focus somewhere sensible: the remove control of the
   * row that took its place, or of the last row when the removed one was last.
   * Focus left on a detached button falls back to the document, which strands a
   * keyboard user at the top of the page.
   */
  async #removeRole(index: number) {
    if (this._roles.length === 1) {
      return;
    }

    this._roles = this._roles.filter((_, i) => i !== index);
    this.#dropRoleErrors();
    void this.#refreshResolution();

    await this.updateComplete;

    const target = Math.min(index, this._roles.length - 1);
    const next =
      this._roles.length === 1
        ? this.shadowRoot?.querySelector<HTMLElement>("#add-requirement")
        : this.shadowRoot?.querySelector<HTMLElement>(`.remove-requirement[data-index="${target}"]`);

    next?.focus();
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
    const groups = this.#resolutionGroups();

    return html`
      <div class="resolution" role="status" aria-label=${this.#term("resolutionSummary")}>
        ${groups.length === 0
          ? nothing
          : html`<ul>
              ${groups.map(
                (group) => html`
                  <li>
                    <!--
                      Labelled with its type: the roles constrain different
                      pools, so an unlabelled list of lines would leave an
                      editor guessing which requirement each count is about.
                    -->
                    <strong>${group.resourceType}</strong>
                    <ul>
                      ${group.lines.map((line) => html`<li>${line}</li>`)}
                    </ul>
                  </li>
                `,
              )}
            </ul>`}
      </div>
    `;
  }

  /**
   * The start-alignment report, beside the resolution summary rather than inside
   * it (design D6).
   *
   * Separate because it is a different claim about different data: the chains
   * describe which resources can provide the service and say nothing about
   * opening hours, while this describes whether two of them can ever start at
   * the same moment. Folding it into a chain would make the chain assert
   * something about availability, which it is forbidden to do.
   *
   * A live region on the same terms as the summary — always present, only its
   * content changing — because a `role="status"` element inserted at the same
   * moment as its text is frequently not announced. It is `aria-label`led and
   * references no ids, so there is no association here that can dangle; the
   * live pass verifies that every id referenced anywhere in this editor's shadow
   * root resolves within it.
   *
   * Not a validation failure and not styled as one: a misaligned service is
   * legitimate, saving is unaffected, and the resources it needs may be adjusted
   * or added later.
   */
  #renderAlignmentReport() {
    const lines = alignmentReport(this._alignment, (key, ...args) =>
      this.localize.term(`ubookitServices_${key}`, ...args),
    );

    return html`
      <div class="alignment" role="status" aria-label=${this.#term("alignmentReport")}>
        ${lines.length === 0
          ? nothing
          : html`<ul>
              ${lines.map((line) => html`<li>${line}</li>`)}
            </ul>`}
      </div>
    `;
  }

  /**
   * Delegates to the pure {@link resolutionGroups}, passing a term resolver so
   * the phrasing logic can be exercised without a DOM or a localization host.
   */
  #resolutionGroups() {
    return resolutionGroups(this._resolution, (key, ...args) =>
      this.localize.term(`ubookitServices_${key}`, ...args),
    );
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
    /* A role's own lines sit under its type label rather than beside it. */
    .resolution ul ul {
      border-left: none;
      margin: 0;
      padding-left: 0;
    }
    /*
      Set apart from the resolution summary above it so the two read as separate
      statements, and deliberately NOT in the danger colour: this is information
      about two resources' opening hours, not a validation failure, and a service
      it describes still saves.
    */
    .alignment ul {
      background: var(--uui-color-surface-alt, #f3f3f5);
      border-left: 3px solid var(--uui-color-warning-standalone, #d29c00);
      list-style: none;
      margin: var(--uui-size-space-4) 0 0;
      padding: var(--uui-size-space-3) var(--uui-size-space-4);
    }
    .alignment li + li {
      margin-top: var(--uui-size-space-1);
    }
    .requirement {
      position: relative;
    }
    .remove-requirement {
      margin-top: var(--uui-size-space-2);
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
