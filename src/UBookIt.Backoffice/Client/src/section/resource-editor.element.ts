import { css, html, customElement, property, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UBookItBackofficeService } from "../api/index.js";
import type {
  CapabilityUsageModel,
  DayOfWeek,
  ResourceClosureModel,
  ResourceRequestModel,
} from "../api/index.js";
import { toApiErrors, type ApiError } from "./api-errors.js";
import {
  exceptionDescribedByIds,
  isSuperseded,
  optOutIds,
  supersededId,
} from "./closure-fields.js";
import "./capability-input.element.js";
import "./responsibility-editor.element.js";
import type { UBookItResponsibilityEditorElement } from "./responsibility-editor.element.js";

interface WindowForm {
  start: string;
  end: string;
}

interface ExceptionForm {
  date: string;
  closed: boolean;
  windows: WindowForm[];

  /**
   * Whether a site closure currently overrides this exception, as reported by the
   * SERVER. Never derived here: precedence has one implementation, in the domain, and a
   * second one in the client would be free to disagree with the availability a visitor
   * is actually offered.
   */
  superseded: boolean;
}

const DAY_ORDER: DayOfWeek[] = [
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
  "Sunday",
];

/** Trim "HH:mm:ss" (API) to "HH:mm" (time input). */
const toInputTime = (value: string) => value.slice(0, 5);

/** Client-only code for the pre-submit empty-date guard; rendered in the Exceptions group. */
const EXCEPTION_DATE_REQUIRED = "exception-date-required";

/**
 * Workspace editor for one resource: Details, Opening hours, Exceptions, and
 * Constraints groups. Saves the full resource; failures surface as an
 * announced error summary plus group-associated messages, and never lose
 * form state.
 *
 * Accessibility notes: uui form controls receive their programmatic name via
 * the `label` property (a visible uui-label with `for` cannot pierce the
 * component's shadow root); native inputs are labelled with `label[for]` in
 * the same shadow root; group errors are announced via the focused
 * role="alert" summary and associated to the native-input fieldsets via
 * aria-describedby.
 */
@customElement("ubookit-resource-editor")
export class UBookItResourceEditorElement extends UmbLitElement {
  @property({ type: String })
  resourceId?: string;

  @state()
  private _loading = true;

  @state()
  private _saving = false;

  @state()
  private _errors: ApiError[] = [];

  @state()
  private _displayName = "";

  @state()
  private _type = "room";

  @state()
  private _description = "";

  @state()
  private _capabilities: string[] = [];

  /**
   * Whether this resource may be booked on its own.
   *
   * False by default, matching the domain: a resource that has not been given
   * the permission does not have it. That default is the point of the field —
   * it is what stops a therapist being sold without a room — so this must never
   * be initialised to true "to preserve existing behaviour".
   */
  @state()
  private _directlyBookable = false;

  /**
   * Keys already carried by some resource, offered as suggestions. Purely a
   * convenience — a failed lookup leaves this empty and the control degrades to
   * free text rather than blocking the save.
   */
  @state()
  private _knownCapabilities: CapabilityUsageModel[] = [];

  @state()
  private _hours = new Map<DayOfWeek, WindowForm[]>();

  @state()
  private _exceptions: ExceptionForm[] = [];

  /**
   * Every site closure, with whether this resource is exempt from it. Read-only apart
   * from the exemption: the dates and labels belong to the site and are edited in the
   * Closures view, behind a different grant.
   */
  @state()
  private _closures: ResourceClosureModel[] = [];

  @state()
  private _constraints = {
    granularityMinutes: 15,
    minDurationMinutes: 30,
    maxDurationMinutes: 480,
    leadTimeMinutes: 0,
    horizonDays: 90,
  };

  #term(key: string) {
    return this.localize.term(`ubookitResources_${key}`);
  }

  #dayLabel(day: DayOfWeek) {
    return this.#term(`day${day}`);
  }

  override connectedCallback() {
    super.connectedCallback();
    void this.#load();
  }

  async #load() {
    // Suggestions are a convenience, so their failure is never surfaced as a
    // save-blocking error (mirrors the services editor's type list).
    void this.#loadKnownCapabilities();

    if (!this.resourceId) {
      this._loading = false;
      return;
    }

    let data;
    try {
      const response = await UBookItBackofficeService.getResource({ path: { id: this.resourceId } });
      data = response.data;
      if (response.error || !data) {
        this._errors = toApiErrors(response.error, this.#term("resourceLoadFailed"));
        this._loading = false;
        return;
      }
    } catch (thrown) {
      this._errors = toApiErrors(thrown, this.#term("resourceLoadFailed"));
      this._loading = false;
      return;
    }

    this._displayName = data.displayName;
    this._type = data.type;
    this._description = data.description ?? "";
    const hours = new Map<DayOfWeek, WindowForm[]>();
    for (const window of data.openingHours) {
      const list = hours.get(window.day) ?? [];
      list.push({ start: toInputTime(window.start), end: toInputTime(window.end) });
      hours.set(window.day, list);
    }
    this._hours = hours;
    this._exceptions = data.exceptions.map((exception) => ({
      date: exception.date,
      closed: exception.windows.length === 0,
      windows: exception.windows.map((w) => ({ start: toInputTime(w.start), end: toInputTime(w.end) })),
      superseded: isSuperseded(exception),
    }));
    this._closures = [...data.closures];
    this._constraints = { ...data.constraints };
    this._capabilities = [...data.capabilities];
    this._directlyBookable = data.directlyBookable;
    this._loading = false;
  }

  async #loadKnownCapabilities() {
    try {
      const { data, error } = await UBookItBackofficeService.listCapabilities();
      if (error || !data) {
        return;
      }

      this._knownCapabilities = data;
    } catch {
      // Leaves the suggestion list empty: the control still accepts free text.
    }
  }

  #buildRequest(): ResourceRequestModel {
    return {
      type: this._type,
      displayName: this._displayName,
      description: this._description || null,
      // Sent verbatim. Trimming happens where the key is added; normalizing
      // here would submit something other than what the chips show.
      capabilities: [...this._capabilities],
      directlyBookable: this._directlyBookable,
      openingHours: DAY_ORDER.flatMap((day) =>
        (this._hours.get(day) ?? []).map((w) => ({ day, start: w.start, end: w.end })),
      ),
      exceptions: this._exceptions.map((exception) => ({
        date: exception.date,
        windows: exception.closed ? [] : exception.windows.map((w) => ({ start: w.start, end: w.end })),
      })),
      // Full replacement, like the capabilities above: an exemption left out is an
      // exemption withdrawn.
      closureOptOuts: optOutIds(this._closures),
      constraints: { ...this._constraints },
    };
  }

  async #save(event: Event) {
    event.preventDefault();
    this._saving = true;
    this._errors = [];

    // Client-side guard for a state only the UI can create: an exception row
    // whose date was never picked. The server would reject it with a raw
    // binding error; catch it here with a stable message instead.
    if (this._exceptions.some((exception) => exception.date === "")) {
      this._errors = [{ code: EXCEPTION_DATE_REQUIRED, message: this.#term("exceptionNeedsDate") }];
      this._saving = false;
      await this.updateComplete;
      this.shadowRoot?.querySelector<HTMLElement>("#error-summary")?.focus();
      return;
    }

    const body = this.#buildRequest();
    try {
      const result = this.resourceId
        ? await UBookItBackofficeService.updateResource({ path: { id: this.resourceId }, body })
        : await UBookItBackofficeService.createResource({ body });

      if (result.error) {
        this._errors = toApiErrors(result.error, this.#term("resourceSaveFailed"));
        return;
      }

      // The responsibility panel saves through its own endpoint, and only once the
      // resource save has succeeded — on create, the id exists only now. A failed
      // panel save keeps the editor open: the resource itself IS saved, and the
      // panel's own message says exactly that.
      const savedId = this.resourceId ?? result.data?.id;
      const responsibility = this.shadowRoot?.querySelector<UBookItResponsibilityEditorElement>(
        "ubookit-responsibility-editor",
      );

      if (savedId && responsibility && !(await responsibility.save(savedId))) {
        this._errors = [{ message: this.#term("responsibilityNotSaved") }];
        return;
      }
    } catch (thrown) {
      this._errors = toApiErrors(thrown, this.#term("resourceSaveFailed"));
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

  #mutateHours(day: DayOfWeek, mutate: (windows: WindowForm[]) => void) {
    const hours = new Map(this._hours);
    const windows = [...(hours.get(day) ?? [])];
    mutate(windows);
    hours.set(day, windows);
    this._hours = hours;
  }

  override render() {
    if (this._loading) {
      return html`<uui-loader-bar aria-label=${this.#term("loadingResource")}></uui-loader-bar>`;
    }

    return html`
      <uui-button
        look="secondary"
        label=${this.#term("back")}
        @click=${() => this.dispatchEvent(new CustomEvent("ubookit-close"))}
      ></uui-button>

      <h2>${this.resourceId ? this._displayName || this.#term("edit") : this.#term("newResource")}</h2>

      ${this.#renderErrorSummary()}

      <form @submit=${this.#save} novalidate>
        ${this.#renderDetails()} ${this.#renderCapabilities()} ${this.#renderResponsibility()}
        ${this.#renderOpeningHours()} ${this.#renderExceptions()} ${this.#renderGlobalClosures()}
        ${this.#renderConstraints()}

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

  /**
   * Capability failures are attributed by the server to the capability field,
   * so they render against this control rather than on the Details group where
   * the type key's failure lives — the two are separate codes precisely so the
   * two messages can land on two different controls.
   */
  #renderCapabilities() {
    // The message is handed to the control as text, not referenced by id: the
    // control owns its own shadow root, and aria-describedby cannot reach into
    // it from here.
    const invalid = this._errors.filter((e) => e.code === "capability-key-invalid");

    return html`
      <uui-box headline=${this.#term("capabilities")}>
        <ubookit-capability-input
          controlId="resource-capabilities"
          .capabilities=${this._capabilities}
          .known=${this._knownCapabilities}
          label=${this.#term("capabilities")}
          addLabel=${this.#term("capabilityAdd")}
          hint=${this.#term("capabilityHint")}
          removeLabel=${this.#term("capabilityRemove")}
          emptyLabel=${this.#term("capabilityNone")}
          error=${invalid.map((e) => e.message).join(" ")}
          @ubookit-capabilities-changed=${(e: CustomEvent<{ capabilities: string[] }>) =>
            (this._capabilities = e.detail.capabilities)}
        ></ubookit-capability-input>
      </uui-box>
    `;
  }

  #renderResponsibility() {
    return html`
      <ubookit-responsibility-editor
        subjectType="resource"
        .subjectId=${this.resourceId}
      ></ubookit-responsibility-editor>
    `;
  }

  #renderDetails() {
    return html`
      <uui-box headline=${this.#term("details")}>
        ${this.#renderGroupErrors("err-details", "display-name-required", "type-key-invalid")}
        <div class="field">
          <uui-label for="resource-name" required>${this.#term("name")}</uui-label>
          <uui-input
            id="resource-name"
            label=${this.#term("name")}
            .value=${this._displayName}
            @input=${(e: InputEvent) => (this._displayName = (e.target as HTMLInputElement).value)}
          ></uui-input>
        </div>
        <div class="field">
          <uui-label for="resource-type">${this.#term("typeKey")}</uui-label>
          <uui-input
            id="resource-type"
            label=${this.#term("typeKey")}
            .value=${this._type}
            @input=${(e: InputEvent) => (this._type = (e.target as HTMLInputElement).value)}
          ></uui-input>
        </div>
        <div class="field">
          <uui-label for="resource-description">${this.#term("description")}</uui-label>
          <uui-textarea
            id="resource-description"
            label=${this.#term("description")}
            .value=${this._description}
            @input=${(e: InputEvent) => (this._description = (e.target as HTMLTextAreaElement).value)}
          ></uui-textarea>
        </div>
        <!--
          A native checkbox rather than uui-toggle, and the reason is the hint
          below it. The explanation has to be ASSOCIATED with the control, not
          merely sitting next to it — a control saying only "bookable directly"
          reads as "can be booked at all", which is false and is the misreading
          most likely to make an editor tick it for a therapist. A native input
          takes aria-describedby in this shadow root, where a uui component's
          internal input is not reliably reachable from the host attribute; the
          editor already uses native inputs for the same reason elsewhere.

          (No backticks in here: this is inside a Lit template literal, and one
          would end the template.)
        -->
        <div class="field">
          <div class="checkbox-field">
            <input
              id="resource-directly-bookable"
              type="checkbox"
              .checked=${this._directlyBookable}
              aria-describedby="resource-directly-bookable-hint"
              @change=${(e: Event) =>
                (this._directlyBookable = (e.target as HTMLInputElement).checked)}
            />
            <label for="resource-directly-bookable">${this.#term("directlyBookable")}</label>
          </div>
          <p id="resource-directly-bookable-hint" class="hint">
            ${this.#term("directlyBookableHint")}
          </p>
        </div>
      </uui-box>
    `;
  }

  #renderWindowRow(
    idPrefix: string,
    window: WindowForm,
    onChange: (patch: Partial<WindowForm>) => void,
    onRemove: () => void,
  ) {
    return html`
      <div class="window-row">
        <label for="${idPrefix}-start">${this.#term("from")}</label>
        <input
          id="${idPrefix}-start"
          type="time"
          .value=${window.start}
          @input=${(e: InputEvent) => onChange({ start: (e.target as HTMLInputElement).value })}
        />
        <label for="${idPrefix}-end">${this.#term("to")}</label>
        <input
          id="${idPrefix}-end"
          type="time"
          .value=${window.end}
          @input=${(e: InputEvent) => onChange({ end: (e.target as HTMLInputElement).value })}
        />
        <uui-button
          look="secondary"
          compact
          label=${this.#term("removeWindow")}
          @click=${onRemove}
        ></uui-button>
      </div>
    `;
  }

  #renderOpeningHours() {
    const hasErrors = this.#errorsFor("window-invalid", "windows-overlap").length > 0;

    return html`
      <uui-box headline=${this.#term("openingHours")}>
        ${this.#renderGroupErrors("err-hours", "window-invalid", "windows-overlap")}
        ${DAY_ORDER.map((day) => {
          const windows = this._hours.get(day) ?? [];
          return html`
            <fieldset class="day" aria-describedby=${hasErrors ? "err-hours" : nothing}>
              <legend>${this.#dayLabel(day)}</legend>
              ${windows.map((window, index) =>
                this.#renderWindowRow(
                  `oh-${day}-${index}`,
                  window,
                  (patch) => this.#mutateHours(day, (list) => (list[index] = { ...list[index], ...patch })),
                  () => this.#mutateHours(day, (list) => list.splice(index, 1)),
                ),
              )}
              <uui-button
                look="secondary"
                compact
                label="${this.#term("addWindow")} (${this.#dayLabel(day)})"
                @click=${() => this.#mutateHours(day, (list) => list.push({ start: "09:00", end: "17:00" }))}
              ></uui-button>
            </fieldset>
          `;
        })}
      </uui-box>
    `;
  }

  #mutateExceptions(mutate: (exceptions: ExceptionForm[]) => void) {
    const exceptions = this._exceptions.map((e) => ({ ...e, windows: [...e.windows] }));
    mutate(exceptions);
    this._exceptions = exceptions;
  }

  #mutateClosures(mutate: (closures: ResourceClosureModel[]) => void) {
    const closures = this._closures.map((closure) => ({ ...closure }));
    mutate(closures);
    this._closures = closures;
  }

  /**
   * Site closures this resource inherits, each with a control exempting it.
   *
   * Read-only except for the exemption: the dates and labels are the site's, edited in
   * the Closures view behind the settings grant, and shown here so that somebody who
   * cannot reach that view can still see what is closing their resource — and say that
   * this one is open anyway.
   *
   * Absent entirely when the site has no closures, rather than rendered empty: a group
   * whose only content is "none" is noise in an editor that already has five.
   */
  #renderGlobalClosures() {
    if (this._closures.length === 0) {
      return nothing;
    }

    return html`
      <uui-box headline=${this.#term("globalClosures")}>
        <p class="group-intro">${this.#term("globalClosuresIntro")}</p>
        <ul class="closures">
          ${this._closures.map(
            (closure, index) => html`
              <li>
                <uui-toggle
                  label="${this.#term("closureOpenAnyway")}: ${closure.date} ${closure.label}"
                  ?checked=${closure.excluded}
                  @change=${(e: Event) =>
                    this.#mutateClosures(
                      (list) => (list[index].excluded = (e.target as HTMLInputElement).checked),
                    )}
                ></uui-toggle>
                <span class="closure-date">${closure.date}</span>
                <span class="closure-label">${closure.label}</span>
              </li>
            `,
          )}
        </ul>
      </uui-box>
    `;
  }

  #renderExceptions() {
    // Window-shape failures can originate from exception overrides as well as
    // weekly hours, so both groups claim the window codes.
    const exceptionCodes = [
      "duplicate-exception-date",
      EXCEPTION_DATE_REQUIRED,
      "window-invalid",
      "windows-overlap",
    ];
    const hasErrors = this.#errorsFor(...exceptionCodes).length > 0;

    return html`
      <uui-box headline=${this.#term("exceptions")}>
        ${this.#renderGroupErrors("err-exceptions", ...exceptionCodes)}
        ${this._exceptions.map(
          (exception, index) => html`
            <fieldset
              class="exception"
              aria-describedby=${exceptionDescribedByIds(index, {
                hasGroupErrors: hasErrors,
                superseded: exception.superseded,
              }).join(" ") || nothing}
            >
              <legend>${this.#term("exception")} ${index + 1}</legend>
              ${exception.superseded
                ? html`<p id=${supersededId(index)} class="superseded">
                    ${this.#term("exceptionSuperseded")}
                  </p>`
                : nothing}
              <div class="window-row">
                <label for="ex-${index}-date">${this.#term("date")}</label>
                <input
                  id="ex-${index}-date"
                  type="date"
                  .value=${exception.date}
                  @input=${(e: InputEvent) =>
                    this.#mutateExceptions((list) => (list[index].date = (e.target as HTMLInputElement).value))}
                />
                <uui-toggle
                  label=${this.#term("closedAllDay")}
                  ?checked=${exception.closed}
                  @change=${(e: Event) =>
                    this.#mutateExceptions((list) => (list[index].closed = (e.target as HTMLInputElement).checked))}
                ></uui-toggle>
                <uui-button
                  look="secondary"
                  compact
                  label="${this.#term("removeException")} ${index + 1}"
                  @click=${() => this.#mutateExceptions((list) => list.splice(index, 1))}
                ></uui-button>
              </div>
              ${exception.closed
                ? nothing
                : html`
                    ${exception.windows.map((window, windowIndex) =>
                      this.#renderWindowRow(
                        `ex-${index}-${windowIndex}`,
                        window,
                        (patch) =>
                          this.#mutateExceptions(
                            (list) => (list[index].windows[windowIndex] = { ...list[index].windows[windowIndex], ...patch }),
                          ),
                        () => this.#mutateExceptions((list) => list[index].windows.splice(windowIndex, 1)),
                      ),
                    )}
                    <uui-button
                      look="secondary"
                      compact
                      label="${this.#term("addWindow")} (${this.#term("exception")} ${index + 1})"
                      @click=${() =>
                        this.#mutateExceptions((list) => list[index].windows.push({ start: "09:00", end: "17:00" }))}
                    ></uui-button>
                  `}
            </fieldset>
          `,
        )}
        <uui-button
          look="secondary"
          label=${this.#term("addException")}
          @click=${() =>
            this.#mutateExceptions((list) =>
              // A new exception is never superseded: the flag is the SERVER's answer about a
              // stored date, and this one has not been saved, or even given a date yet.
              list.push({ date: "", closed: true, windows: [], superseded: false }),
            )}
        ></uui-button>
      </uui-box>
    `;
  }

  #renderConstraintField(id: string, label: string, key: keyof typeof this._constraints) {
    return html`
      <div class="field">
        <uui-label for=${id}>${label}</uui-label>
        <uui-input
          id=${id}
          type="number"
          min="0"
          label=${label}
          .value=${String(this._constraints[key])}
          @input=${(e: InputEvent) =>
            (this._constraints = {
              ...this._constraints,
              [key]: Number((e.target as HTMLInputElement).value),
            })}
        ></uui-input>
      </div>
    `;
  }

  #renderConstraints() {
    return html`
      <uui-box headline=${this.#term("constraints")}>
        ${this.#renderGroupErrors("err-constraints", "constraints-incoherent", "granularity")}
        ${this.#renderConstraintField("c-granularity", this.#term("granularity"), "granularityMinutes")}
        ${this.#renderConstraintField("c-min", this.#term("minDuration"), "minDurationMinutes")}
        ${this.#renderConstraintField("c-max", this.#term("maxDuration"), "maxDurationMinutes")}
        ${this.#renderConstraintField("c-lead", this.#term("leadTime"), "leadTimeMinutes")}
        ${this.#renderConstraintField("c-horizon", this.#term("horizon"), "horizonDays")}
      </uui-box>
    `;
  }

  static override styles = css`
    uui-box {
      margin-top: var(--uui-size-space-4);
    }
    .checkbox-field {
      align-items: center;
      display: flex;
      gap: var(--uui-size-space-2);
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
    }
    .window-row {
      display: flex;
      align-items: center;
      gap: var(--uui-size-space-3);
      margin-bottom: var(--uui-size-space-2);
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
    .group-intro {
      margin: 0 0 var(--uui-size-space-3);
    }
    .closures {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .closures li {
      align-items: center;
      display: flex;
      gap: var(--uui-size-space-3);
      margin-bottom: var(--uui-size-space-2);
    }
    .closure-date {
      font-weight: bold;
    }
    /*
     * The statement that an exception is superseded. Deliberately NOT coloured as an
     * error: nothing is wrong, and the exception becomes live again the moment the
     * closure above it is opted out of. It carries no colour of its own at all, so it
     * inherits whatever the backoffice sets and cannot fail contrast on a theme this
     * package has never seen.
     */
    .superseded {
      font-style: italic;
      margin: var(--uui-size-space-2) var(--uui-size-space-3);
    }
  `;
}

export default UBookItResourceEditorElement;

declare global {
  interface HTMLElementTagNameMap {
    "ubookit-resource-editor": UBookItResourceEditorElement;
  }
}
