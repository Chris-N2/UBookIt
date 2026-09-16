import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import { UBookItBackofficeService } from "../api/index.js";
import { toApiErrors } from "./api-errors.js";
import { bookingReference } from "./booking-rows.js";
import { isComplete, prefill, refusalConcernsFields, refusalTermFor, toRequest, type MoveFields } from "./move-fields.js";
import type { MoveBookingModalData, MoveBookingModalValue } from "./move-booking-modal.token.js";

/**
 * The move dialog: a date, a start time and a length, pre-filled with what the booking holds,
 * submitted to the move endpoint, and — where the domain refuses — explained in place.
 *
 * **Native inputs with real `<label for>`, not `uui-input`.** The bookings-screen change
 * recorded why: `uui-label` is not a label, and no `uui-*` control carries `aria-describedby`
 * through to its shadow root, so a refusal rendered NEXT TO a uui control is visible and
 * programmatically unrelated to it. The refusal is the whole point of this dialog — it is how
 * an operator learns the time they chose cannot be taken and which way to change it — so it is
 * associated with the inputs by id, and announced.
 *
 * **There is no availability picker here, and the dialog does not pretend to one.** An operator
 * chooses a time and is told whether it can be taken; where it cannot, the reason is shown and
 * the dialog stays open so they choose again. A read that shows where a booking could go is
 * its own change. Nothing here presents a time as available before the domain has said so.
 *
 * **The domain is the rule.** This dialog validates only that the three fields are present,
 * so the obvious case is caught before a round trip; everything else — open hours, the grid,
 * the length bounds, the past, another booking, an unchanged interval, a status that does not
 * permit a move — is the endpoint's answer, shown by its stable code.
 */
@customElement("ubookit-move-booking-modal")
export class UBookItMoveBookingModalElement extends UmbModalBaseElement<MoveBookingModalData, MoveBookingModalValue> {
  @state()
  private _fields: MoveFields = { date: "", time: "", lengthMinutes: 0 };

  @state()
  private _pending = false;

  /** The refusal's localisation key, or undefined while none. */
  @state()
  private _refusal?: string;

  #term(key: string) {
    return this.localize.term(`ubookitBookings_${key}`);
  }

  override connectedCallback() {
    super.connectedCallback();

    if (this.data?.booking) {
      this._fields = prefill(this.data.booking);
    }
  }

  /**
   * Focus lands on the first input when the dialog opens, and returns to the first input
   * after a refusal.
   *
   * Measured rather than assumed: with nothing here, the live backoffice reported
   * `document.activeElement` as `<body>` both on open and after a refusal — the modal
   * container does not move focus into the content it hosts, and a keyboard operator would
   * have been left outside the dialog they had just opened. After a refusal the input is the
   * right place too: the alert has announced the sentence, and the input's description now
   * carries it, so focusing the control puts the operator where the fix is made.
   */
  override firstUpdated() {
    void this.#focusFirstInput();
  }

  async #focusFirstInput() {
    await this.updateComplete;
    this.shadowRoot?.querySelector<HTMLInputElement>("#ubookit-move-date")?.focus();
  }

  #set<K extends keyof MoveFields>(key: K, value: MoveFields[K]) {
    this._fields = { ...this._fields, [key]: value };
    // A refusal is about the time that was refused; editing any field retires it.
    this._refusal = undefined;
  }

  async #submit(event: Event) {
    event.preventDefault();

    if (!this.data?.booking || this._pending) {
      return;
    }

    if (!isComplete(this._fields)) {
      this._refusal = "moveIncomplete";
      void this.#focusFirstInput();
      return;
    }

    this._pending = true;
    this._refusal = undefined;

    try {
      const { data, error } = await UBookItBackofficeService.moveBooking({
        path: { id: this.data.booking.bookingId },
        body: toRequest(this._fields),
      });

      if (error || !data) {
        // Stays open, and says which rule refused — in the operator's words, associated
        // with the inputs, so they change the time rather than start over.
        this._refusal = refusalTermFor(toApiErrors(error, this.#term("moveFailed")));
        void this.#focusFirstInput();
        return;
      }

      this.value = { startUtc: data.startUtc, endUtc: data.endUtc, timeZoneId: data.timeZoneId };
      this._submitModal();
    } catch (thrown) {
      this._refusal = refusalTermFor(toApiErrors(thrown, this.#term("moveFailed")));
      void this.#focusFirstInput();
    } finally {
      this._pending = false;
    }
  }

  override render() {
    const booking = this.data?.booking;
    const describedBy = this._refusal ? "ubookit-move-hint ubookit-move-refusal" : "ubookit-move-hint";
    // Invalid only when a FIELD was refused; a refusal about the booking's status or existence
    // still describes the inputs (so it is reachable from them) but does not mark them wrong.
    const invalid = this._refusal !== undefined && refusalConcernsFields(this._refusal) ? "true" : "false";

    return html`
      <uui-dialog-layout headline=${this.#term("moveHeadline")}>
        <form @submit=${this.#submit} novalidate>
          <p>${booking ? this.localize.term("ubookitBookings_moveIntro", bookingReference(booking)) : nothing}</p>

          <!--
            The truthful conditional about notification, where the operator can see it at the
            moment they are deciding — the same sentence cancel and decline carry, for the same
            reason: this string cannot see whether booking emails are configured, so it says the
            thing that is true either way. Referenced from every input, so a screen-reader user
            meets it from the control rather than only in reading order.
          -->
          <p id="ubookit-move-hint" class="hint">${this.#term("moveNotificationHint")}</p>

          <div class="field">
            <label for="ubookit-move-date">${this.#term("moveDate")}</label>
            <input
              id="ubookit-move-date"
              type="date"
              required
              aria-describedby=${describedBy}
              aria-invalid=${invalid}
              .value=${this._fields.date}
              @input=${(event: Event) => this.#set("date", (event.target as HTMLInputElement).value)}
            />
          </div>

          <div class="field">
            <label for="ubookit-move-time">${this.#term("moveTime")}</label>
            <input
              id="ubookit-move-time"
              type="time"
              required
              step="60"
              aria-describedby=${describedBy}
              aria-invalid=${invalid}
              .value=${this._fields.time}
              @input=${(event: Event) => this.#set("time", (event.target as HTMLInputElement).value)}
            />
          </div>

          <div class="field">
            <label for="ubookit-move-length">${this.#term("moveLength")}</label>
            <input
              id="ubookit-move-length"
              type="number"
              required
              min="1"
              step="1"
              aria-describedby=${describedBy}
              aria-invalid=${invalid}
              .value=${String(this._fields.lengthMinutes || "")}
              @input=${(event: Event) =>
                this.#set("lengthMinutes", Number((event.target as HTMLInputElement).value))}
            />
          </div>

          <!--
            role=alert so a refusal is ANNOUNCED when it appears, and an id so it is also
            reachable from each input's aria-describedby: two routes, because an alert that
            fires once and a description that persists serve different moments.
          -->
          ${this._refusal
            ? html`<p id="ubookit-move-refusal" role="alert" class="refusal">${this.#term(this._refusal)}</p>`
            : nothing}

          <!--
            A real submit button inside the form, so Enter in any field submits; rendered
            visually hidden because the dialog's own action slot below carries the visible
            one. The visible button is type=button and calls the same handler.
          -->
          <button type="submit" class="visually-hidden" tabindex="-1" aria-hidden="true"></button>
        </form>

        <uui-button
          slot="actions"
          label=${this.#term("moveCancel")}
          @click=${() => this._rejectModal()}
        ></uui-button>
        <uui-button
          slot="actions"
          look="primary"
          color="positive"
          label=${this.#term("moveSubmit")}
          .state=${this._pending ? "waiting" : undefined}
          ?disabled=${this._pending}
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
    input {
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

export default UBookItMoveBookingModalElement;

declare global {
  interface HTMLElementTagNameMap {
    "ubookit-move-booking-modal": UBookItMoveBookingModalElement;
  }
}
