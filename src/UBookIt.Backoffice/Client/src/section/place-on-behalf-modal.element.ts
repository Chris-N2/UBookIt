import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import { UBookItBackofficeService } from "../api/index.js";
import { toApiErrors } from "./api-errors.js";
import {
  emptyFields,
  isComplete,
  refusalConcernsBooker,
  refusalTermFor,
  toRequest,
  type PlaceOnBehalfFields,
} from "./place-on-behalf-fields.js";
import type { PlaceOnBehalfModalData, PlaceOnBehalfModalValue } from "./place-on-behalf-modal.token.js";

interface Subject {
  id: string;
  name: string;
}

/**
 * The placement dialog: what to book, when, for how long, and who for — submitted to the
 * placement endpoint, and, where the domain refuses, explained in place.
 *
 * **Native inputs with real `<label for>`, not `uui-input`.** The bookings-screen change
 * recorded why: `uui-label` is not a label, and no `uui-*` control carries `aria-describedby`
 * through to its shadow root, so a refusal rendered NEXT TO a uui control is visible and
 * programmatically unrelated to it.
 *
 * **There is no availability picker here, and the dialog does not pretend to one.** An operator
 * chooses a time and is told whether it can be taken. A read that shows where a booking could go
 * is its own change, and nothing here presents a time as available before the domain has said so.
 *
 * **The domain is the rule.** This dialog checks only that the fields are filled in, so the
 * obvious case is caught before a round trip; open hours, the grid, the length bounds, the past,
 * another booking, a service's own length rules and the booker's address are all the endpoint's
 * answer, shown by its stable code.
 *
 * **What to book is ONE select with two groups**, rather than a kind-then-item wizard: one
 * control, one label, no custom keyboard handling, and the operator sees everything bookable at
 * once. The resources offered include those a site withholds from direct booking by visitors,
 * because that permission does not bind an operator.
 */
@customElement("ubookit-place-on-behalf-modal")
export class UBookItPlaceOnBehalfModalElement extends UmbModalBaseElement<
  PlaceOnBehalfModalData,
  PlaceOnBehalfModalValue
> {
  @state()
  private _fields: PlaceOnBehalfFields = emptyFields("");

  @state()
  private _services: Subject[] = [];

  @state()
  private _resources: Subject[] = [];

  @state()
  private _loading = true;

  @state()
  private _pending = false;

  /** The refusal's localisation key, or undefined while none. */
  @state()
  private _refusal?: string;

  /** Whether the refusal was about the booker's details rather than the time. */
  @state()
  private _refusalIsBooker = false;

  #term(key: string) {
    return this.localize.term(`ubookitBookings_${key}`);
  }

  override connectedCallback() {
    super.connectedCallback();

    this._fields = emptyFields(this.data?.from ?? "");
    void this.#loadSubjects();
  }

  /**
   * What there is to book, from the read gated on the same verb as the placement.
   *
   * A failure here is reported rather than left as an empty select: an operator staring at a
   * picker with nothing in it would conclude the site has no resources, which is a different and
   * much more alarming thing than a request that did not come back.
   */
  async #loadSubjects() {
    try {
      const { data, error } = await UBookItBackofficeService.listBookableSubjects();

      if (error || !data) {
        this._refusal = "placeSubjectsFailed";
        return;
      }

      this._services = data.services.map((s) => ({ id: s.id, name: s.name }));
      this._resources = data.resources.map((r) => ({ id: r.id, name: r.name }));
    } catch {
      this._refusal = "placeSubjectsFailed";
    } finally {
      this._loading = false;
      void this.#focusFirstField();
    }
  }

  /**
   * Focus lands inside the dialog when it opens, and on the offending control after a refusal.
   *
   * Measured rather than assumed: with nothing here, the live backoffice reported
   * `document.activeElement` as `<body>` both on open and after a refusal — the modal container
   * does not move focus into the content it hosts.
   */
  override firstUpdated() {
    void this.#focusFirstField();
  }

  async #focusFirstField() {
    await this.updateComplete;
    this.shadowRoot?.querySelector<HTMLElement>("#ubookit-place-subject")?.focus();
  }

  async #focusBooker() {
    await this.updateComplete;
    this.shadowRoot?.querySelector<HTMLElement>("#ubookit-place-booker-email")?.focus();
  }

  #set<K extends keyof PlaceOnBehalfFields>(key: K, value: PlaceOnBehalfFields[K]) {
    this._fields = { ...this._fields, [key]: value };
    // A refusal is about what was submitted; editing anything retires it.
    this._refusal = undefined;
    this._refusalIsBooker = false;
  }

  #chooseSubject(value: string) {
    // "service:<id>" or "resource:<id>", so one control produces one chosen subject and the
    // request cannot name both — see `toRequest`.
    const [kind, ...rest] = value.split(":");
    const id = rest.join(":");

    this.#set(
      "subject",
      kind === "service" || kind === "resource" ? { kind, id } : undefined,
    );
  }

  async #submit(event: Event) {
    event.preventDefault();

    if (this._pending) {
      return;
    }

    if (!isComplete(this._fields)) {
      this._refusal = "placeIncomplete";
      void this.#focusFirstField();
      return;
    }

    this._pending = true;
    this._refusal = undefined;
    this._refusalIsBooker = false;

    try {
      const { data, error } = await UBookItBackofficeService.placeBookingOnBehalf({
        body: toRequest(this._fields),
      });

      if (error || !data) {
        this.#refuse(toApiErrors(error, this.#term("placeFailed")));
        return;
      }

      this.value = {
        reference: data.reference,
        startUtc: data.startUtc,
        endUtc: data.endUtc,
        timeZoneId: data.timeZoneId,
      };
      this._submitModal();
    } catch (thrown) {
      this.#refuse(toApiErrors(thrown, this.#term("placeFailed")));
    } finally {
      this._pending = false;
    }
  }

  /**
   * Shows a refusal and puts focus where the correction is made.
   *
   * **Everything the operator typed stays.** A dialog that cleared itself on a refusal would
   * make an operator ask a customer for their name and address a second time, which is the worst
   * possible moment to look disorganised.
   */
  #refuse(errors: ReturnType<typeof toApiErrors>) {
    this._refusal = refusalTermFor(errors);
    this._refusalIsBooker = refusalConcernsBooker(errors);

    void (this._refusalIsBooker ? this.#focusBooker() : this.#focusFirstField());
  }

  override render() {
    const describedBy = this._refusal ? "ubookit-place-hint ubookit-place-refusal" : "ubookit-place-hint";
    const timeInvalid = this._refusal !== undefined && !this._refusalIsBooker ? "true" : "false";
    const bookerInvalid = this._refusalIsBooker ? "true" : "false";
    const chosen = this._fields.subject ? `${this._fields.subject.kind}:${this._fields.subject.id}` : "";

    return html`
      <uui-dialog-layout headline=${this.#term("placeHeadline")}>
        <form @submit=${this.#submit} novalidate>
          <!--
            The truthful conditional about notification, where the operator can see it at the
            moment they are deciding — the same sentence cancel, decline and move carry, for the
            same reason: this string cannot see whether booking emails are configured, so it says
            the thing that is true either way.
          -->
          <p id="ubookit-place-hint" class="hint">${this.#term("placeNotificationHint")}</p>

          <div class="field">
            <label for="ubookit-place-subject">${this.#term("placeSubject")}</label>
            <select
              id="ubookit-place-subject"
              required
              aria-describedby=${describedBy}
              aria-invalid=${timeInvalid}
              ?disabled=${this._loading}
              .value=${chosen}
              @change=${(event: Event) => this.#chooseSubject((event.target as HTMLSelectElement).value)}
            >
              <option value="">${this.#term("placeSubjectUnchosen")}</option>
              ${this._services.length
                ? html`<optgroup label=${this.#term("placeServices")}>
                    ${this._services.map(
                      (service) => html`<option value="service:${service.id}">${service.name}</option>`,
                    )}
                  </optgroup>`
                : nothing}
              ${this._resources.length
                ? html`<optgroup label=${this.#term("placeResources")}>
                    ${this._resources.map(
                      (resource) => html`<option value="resource:${resource.id}">${resource.name}</option>`,
                    )}
                  </optgroup>`
                : nothing}
            </select>
          </div>

          <div class="field">
            <label for="ubookit-place-date">${this.#term("placeDate")}</label>
            <input
              id="ubookit-place-date"
              type="date"
              required
              aria-describedby=${describedBy}
              aria-invalid=${timeInvalid}
              .value=${this._fields.date}
              @input=${(event: Event) => this.#set("date", (event.target as HTMLInputElement).value)}
            />
          </div>

          <div class="field">
            <label for="ubookit-place-time">${this.#term("placeTime")}</label>
            <input
              id="ubookit-place-time"
              type="time"
              required
              step="60"
              aria-describedby=${describedBy}
              aria-invalid=${timeInvalid}
              .value=${this._fields.time}
              @input=${(event: Event) => this.#set("time", (event.target as HTMLInputElement).value)}
            />
          </div>

          <div class="field">
            <label for="ubookit-place-length">${this.#term("placeLength")}</label>
            <input
              id="ubookit-place-length"
              type="number"
              required
              min="1"
              step="1"
              aria-describedby=${describedBy}
              aria-invalid=${timeInvalid}
              .value=${String(this._fields.lengthMinutes || "")}
              @input=${(event: Event) =>
                this.#set("lengthMinutes", Number((event.target as HTMLInputElement).value))}
            />
          </div>

          <div class="field">
            <label for="ubookit-place-booker-name">${this.#term("placeBookerName")}</label>
            <input
              id="ubookit-place-booker-name"
              type="text"
              required
              autocomplete="off"
              aria-describedby=${describedBy}
              aria-invalid=${bookerInvalid}
              .value=${this._fields.bookerName}
              @input=${(event: Event) => this.#set("bookerName", (event.target as HTMLInputElement).value)}
            />
          </div>

          <div class="field">
            <label for="ubookit-place-booker-email">${this.#term("placeBookerEmail")}</label>
            <input
              id="ubookit-place-booker-email"
              type="email"
              required
              autocomplete="off"
              aria-describedby=${describedBy}
              aria-invalid=${bookerInvalid}
              .value=${this._fields.bookerEmail}
              @input=${(event: Event) => this.#set("bookerEmail", (event.target as HTMLInputElement).value)}
            />
          </div>

          <div class="field">
            <label for="ubookit-place-booker-phone">${this.#term("placeBookerPhone")}</label>
            <input
              id="ubookit-place-booker-phone"
              type="tel"
              autocomplete="off"
              aria-describedby=${describedBy}
              .value=${this._fields.bookerPhone}
              @input=${(event: Event) => this.#set("bookerPhone", (event.target as HTMLInputElement).value)}
            />
          </div>

          <!--
            role=alert so a refusal is ANNOUNCED when it appears, and an id so it is also
            reachable from each control's aria-describedby: two routes, because an alert that
            fires once and a description that persists serve different moments.
          -->
          ${this._refusal
            ? html`<p id="ubookit-place-refusal" role="alert" class="refusal">${this.#term(this._refusal)}</p>`
            : nothing}

          <!--
            A real submit button inside the form, so Enter in any field submits; rendered
            visually hidden because the dialog's own action slot below carries the visible one.
          -->
          <button type="submit" class="visually-hidden" tabindex="-1" aria-hidden="true"></button>
        </form>

        <uui-button
          slot="actions"
          label=${this.#term("placeCancel")}
          @click=${() => this._rejectModal()}
        ></uui-button>
        <uui-button
          slot="actions"
          look="primary"
          color="positive"
          label=${this.#term("placeSubmit")}
          .state=${this._pending ? "waiting" : undefined}
          ?disabled=${this._pending || this._loading}
          @click=${(event: Event) => this.#submit(event)}
        ></uui-button>
      </uui-dialog-layout>
    `;
  }

  static override styles = css`
    form {
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-4);
    }
    .field {
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-1);
    }
    label {
      font-weight: 700;
    }
    input,
    select {
      font: inherit;
      padding: var(--uui-size-space-2);
      border: 1px solid var(--uui-color-border);
      border-radius: var(--uui-border-radius);
      background: var(--uui-color-surface);
      color: inherit;
    }
    .hint {
      margin: 0;
      font-size: var(--uui-type-small-size);
      color: var(--uui-color-text-alt);
    }
    .refusal {
      margin: 0;
      color: var(--uui-color-danger, #d42054);
    }
    .visually-hidden {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip: rect(0 0 0 0);
      white-space: nowrap;
      border: 0;
      padding: 0;
    }
  `;
}

export default UBookItPlaceOnBehalfModalElement;

declare global {
  interface HTMLElementTagNameMap {
    "ubookit-place-on-behalf-modal": UBookItPlaceOnBehalfModalElement;
  }
}
