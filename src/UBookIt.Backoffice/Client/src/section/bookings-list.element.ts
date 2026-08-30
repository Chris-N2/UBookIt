import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UBookItBackofficeService } from "../api/index.js";
import type { BookingModel } from "../api/index.js";
import { toApiErrors } from "./api-errors.js";
import {
  currentWeek,
  formatInterval,
  listQuery,
  serviceLabel,
  shouldLoad,
  showsEmptyMessage,
  skipAfter,
  zoneFallbackOccurred,
  zoneLabelNeeded,
} from "./booking-rows.js";

const PAGE_SIZE = 20;

/**
 * The statuses the package publishes, in the order the domain declares them.
 *
 * These are **wire values**, not labels. The label is looked up per status from
 * localization; the value sent is the published name. A localized label reaching
 * the wire would be refused by the endpoint — which is the good outcome — but on
 * a site running a translation it would be refused for every request, so the
 * separation matters more than it looks.
 */
const STATUSES = ["Requested", "Confirmed", "Cancelled", "Declined"] as const;

/**
 * Collection view over the bookings endpoint: a window, a status filter, a
 * semantic table and prev/next paging.
 *
 * Read-only by design. Cancelling is the only other v1 verb and is its own
 * change; there is deliberately no editor to route to and no seam prepared for
 * one, because a routing seam for a workspace that does not exist is a guess
 * about what that change will need.
 */
@customElement("ubookit-bookings-list")
export class UBookItBookingsListElement extends UmbLitElement {
  @state()
  private _items: BookingModel[] = [];

  @state()
  private _total = 0;

  @state()
  private _skip = 0;

  @state()
  private _loading = true;

  @state()
  private _error?: string;

  @state()
  private _statuses: string[] = [];

  @state()
  private _window = currentWeek(new Date());

  /**
   * Which load is allowed to write state.
   *
   * Requests can finish out of order, and an operator changing From and then To
   * starts two. **Found in the browser, not by a test:** the first request went
   * out with the second date still empty, its failure arrived after the good
   * response, and the view ended up showing an error above correct results —
   * the error and the data contradicting each other on screen.
   *
   * That is a worse version of the state `showsEmptyMessage` exists to prevent.
   *
   * **No test in the current suite reaches this** — every client suite here is
   * pure-module, and reproducing it needs an element instance and therefore a
   * DOM environment, which is not installed. That is a fact about the suite
   * rather than about the code: a spy returning two deferred promises would
   * reproduce it exactly. The guarantee is stated in the spec instead, so it is
   * at least written down somewhere that outlives this comment.
   */
  #latestLoad = 0;

  #term(key: string) {
    return this.localize.term(`ubookitBookings_${key}`);
  }

  override connectedCallback() {
    super.connectedCallback();
    void this.#load();
  }

  async #load() {
    // A half-edited date reads as empty, and asking with it earns a 400 the
    // operator did not cause and cannot act on. Waiting costs nothing: the next
    // keystroke that completes the date fires another change.
    if (!shouldLoad(this._window)) {
      this._loading = false;
      return;
    }

    const load = ++this.#latestLoad;

    // Only the most recently started load may write anything. A superseded one
    // has already been answered by a newer question.
    const current = () => load === this.#latestLoad;

    this._loading = true;
    this._error = undefined;

    try {
      // The window travels as two DATES. No instant is computed here and no
      // zone is applied: the endpoint resolves them against the site's zone,
      // which is where that rule lives precisely so it is not reimplemented
      // once per client and got differently wrong at a daylight-saving edge.
      // The query is built by `listQuery` so that shape is assertable — it is a
      // guarantee that fails invisibly, since a wrong window still returns a
      // perfectly plausible list.
      const { data, error } = await UBookItBackofficeService.listBookings({
        query: listQuery(this._window, this._statuses, this._skip, PAGE_SIZE),
      });

      if (!current()) {
        return;
      }

      if (error || !data) {
        // The endpoint reports an over-wide window in terms of the dates that
        // were sent, so the message is shown rather than replaced by a generic
        // one — it names something the operator can act on, and the dates it
        // names are the ones in the two controls above.
        this._error = toApiErrors(error, this.#term("listLoadFailed"))
          .map((failure) => failure.message)
          .filter(Boolean)
          .join(" ") || this.#term("listLoadFailed");

        this._items = [];
        this._total = 0;
      } else {
        this._items = data.items;
        this._total = data.total;
      }
    } catch (thrown) {
      if (!current()) {
        return;
      }

      this._error = toApiErrors(thrown, this.#term("listLoadFailed"))
        .map((failure) => failure.message)
        .filter(Boolean)
        .join(" ") || this.#term("listLoadFailed");

      this._items = [];
      this._total = 0;
    }

    // Only the newest load clears the spinner, so a superseded one cannot
    // report "finished" while its replacement is still in flight.
    if (current()) {
      this._loading = false;
    }
  }

  /** A window change makes the current page number meaningless, so paging resets. */
  #setWindow(end: "from" | "to", value: string) {
    this._window = { ...this._window, [end]: value };
    this._skip = skipAfter("query", this._skip);
    void this.#load();
  }

  #toggleStatus(status: string, selected: boolean) {
    this._statuses = selected
      ? [...this._statuses, status]
      : this._statuses.filter((candidate) => candidate !== status);

    this._skip = skipAfter("query", this._skip);
    void this.#load();
  }

  override render() {
    const pageEnd = Math.min(this._skip + PAGE_SIZE, this._total);

    return html`
      <div class="header">
        <h2>${this.#term("label")}</h2>
      </div>

      ${this.#renderControls()}

      <!--
        role=alert so a failed load is ANNOUNCED, not merely rendered. An empty
        table and a failed request look identical to a reader and mean opposite
        things: "nothing is booked this week" versus "we could not find out".
      -->
      ${this._error ? html`<div role="alert" class="error">${this._error}</div>` : nothing}
      ${this._loading
        ? html`<uui-loader-bar aria-label=${this.#term("loadingList")}></uui-loader-bar>`
        : this.#renderTable(pageEnd)}
    `;
  }

  #renderControls() {
    return html`
      <div class="controls">
        <!--
          The label attribute on the input, not only uui-label. uui-label is not
          a native label: its "for" is a click handler that focuses the target,
          and it sets no aria-labelledby. A uui-input names its internal input
          from its own label property or aria-label and from nothing else, so
          without this the control is announced as an unlabelled edit field and
          only a pointer user gets the association. Every other uui-input in
          this client pairs the two for the same reason.

          (No backticks in here: this is inside a Lit template literal, and one
          would end the template. The editor leaves the same warning, and this
          comment was written with them the first time.)
        -->
        <div class="field">
          <uui-label for="ubookit-bookings-from">${this.#term("from")}</uui-label>
          <uui-input
            id="ubookit-bookings-from"
            type="date"
            label=${this.#term("from")}
            .value=${this._window.from}
            @change=${(event: Event) =>
              this.#setWindow("from", (event.target as HTMLInputElement).value)}
          ></uui-input>
        </div>

        <div class="field">
          <uui-label for="ubookit-bookings-to">${this.#term("to")}</uui-label>
          <uui-input
            id="ubookit-bookings-to"
            type="date"
            label=${this.#term("to")}
            .value=${this._window.to}
            @change=${(event: Event) =>
              this.#setWindow("to", (event.target as HTMLInputElement).value)}
          ></uui-input>
        </div>

        <!--
          The hint is described BY THE FIELDSET, not by each control:
          aria-describedby on a uui-toggle host is dropped, because
          uui-boolean-input forwards aria-label and aria-labelledby to its
          internal input and nothing else. The editor hit that wall and left a
          note; the service editor puts the reference on the fieldset too.

          WHAT THIS IS AND IS NOT. It makes the description valid — the
          reference resolves within this shadow root, which was measured. It
          does NOT reliably make it announced: a fieldset maps to role=group,
          aria-describedby is not inherited by descendants, and screen readers
          announce a group's NAME on entry rather than its description. Group
          descriptions are read reliably for composite widgets with one focus
          stop, which four independently tabbable toggles are not.

          So the hint is carried by DOM ORDER rather than by that reference: it
          is a visible paragraph inside the fieldset, after the legend and
          before the toggles, where a reader going through the view meets it.
          The reference is worth keeping and is not worth relying on, and this
          note exists because an earlier version of it claimed more than had
          been measured — which is exactly the fault that produced this round's
          predecessor.
        -->
        <fieldset class="statuses" aria-describedby="ubookit-bookings-status-hint">
          <legend>${this.#term("statusFilter")}</legend>
          <p class="hint" id="ubookit-bookings-status-hint">${this.#term("statusHint")}</p>
          ${STATUSES.map(
            (status) => html`
              <uui-toggle
                label=${this.#term(`status${status}`)}
                ?checked=${this._statuses.includes(status)}
                @change=${(event: Event) =>
                  this.#toggleStatus(status, (event.target as HTMLInputElement).checked)}
              ></uui-toggle>
            `,
          )}
        </fieldset>
      </div>
    `;
  }

  #renderTable(pageEnd: number) {
    if (this._total === 0) {
      // "No bookings in this window" is a claim about the site, and after a
      // failed request the view does not know whether it is true. The alert
      // above already says what happened; saying "none" as well would answer a
      // question nothing asked, with the one answer most likely to be wrong.
      return showsEmptyMessage(this._total, this._error !== undefined)
        ? html`<p>${this.#term("empty")}</p>`
        : nothing;
    }

    // Either because the page mixes zones, or because a zone the runtime could
    // not resolve has been shown in UTC — in which case the reader is told
    // rather than left reading a time attributed to a zone nobody chose.
    const showZone = zoneLabelNeeded(this._items) || zoneFallbackOccurred(this._items);

    return html`
      <uui-table aria-label=${this.#term("tableLabel")}>
        <uui-table-head>
          <uui-table-head-cell>${this.#term("when")}</uui-table-head-cell>
          <uui-table-head-cell>${this.#term("booker")}</uui-table-head-cell>
          <uui-table-head-cell>${this.#term("resources")}</uui-table-head-cell>
          <uui-table-head-cell>${this.#term("service")}</uui-table-head-cell>
          <uui-table-head-cell>${this.#term("status")}</uui-table-head-cell>
        </uui-table-head>
        ${this._items.map((booking) => this.#renderRow(booking, showZone))}
      </uui-table>

      <nav class="paging" aria-label=${this.#term("pagingLabel")}>
        <uui-button
          look="secondary"
          label=${this.#term("previousPage")}
          ?disabled=${this._skip === 0}
          @click=${() => {
            this._skip = skipAfter("page", Math.max(0, this._skip - PAGE_SIZE));
            void this.#load();
          }}
        ></uui-button>
        <span aria-live="polite">
          ${this.localize.term("ubookitBookings_showing", this._skip + 1, pageEnd, this._total)}
        </span>
        <uui-button
          look="secondary"
          label=${this.#term("nextPage")}
          ?disabled=${pageEnd >= this._total}
          @click=${() => {
            this._skip = skipAfter("page", this._skip + PAGE_SIZE);
            void this.#load();
          }}
        ></uui-button>
      </nav>
    `;
  }

  #renderRow(booking: BookingModel, showZone: boolean) {
    // The reader's own locale for month names and clock format; the ZONE comes
    // from the booking. Those are different questions: how a time is written is
    // the reader's, which time it is was decided when the booking was placed.
    const interval = formatInterval(booking);

    return html`
      <uui-table-row>
        <uui-table-cell>
          ${interval.text}${showZone ? html` <span class="zone">${interval.zone}</span>` : nothing}
        </uui-table-cell>
        <uui-table-cell>
          ${booking.bookerName}
          <div class="secondary">${booking.bookerEmail}</div>
        </uui-table-cell>
        <uui-table-cell>
          ${booking.resources.map((resource) => resource.displayName).join(", ")}
        </uui-table-cell>
        <uui-table-cell>${serviceLabel(booking, this.#term("bookedDirectly"))}</uui-table-cell>
        <uui-table-cell>${this.#term(`status${booking.status}`)}</uui-table-cell>
      </uui-table-row>
    `;
  }

  static override styles = css`
    .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: var(--uui-size-space-4);
    }
    .controls {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--uui-size-space-4);
      margin: var(--uui-size-space-4) 0;
    }
    .statuses {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--uui-size-space-3);
      border: 1px solid var(--uui-color-border);
      padding: var(--uui-size-space-3);
    }
    .hint {
      margin: 0;
      font-size: var(--uui-type-small-size);
      color: var(--uui-color-text-alt);
    }
    .secondary {
      font-size: var(--uui-type-small-size);
      color: var(--uui-color-text-alt);
    }
    .zone {
      font-size: var(--uui-type-small-size);
      color: var(--uui-color-text-alt);
    }
    .error {
      color: var(--uui-color-danger, #d42054);
      margin: var(--uui-size-space-3) 0;
    }
    .paging {
      display: flex;
      align-items: center;
      gap: var(--uui-size-space-4);
      margin-top: var(--uui-size-space-4);
    }
  `;
}

export default UBookItBookingsListElement;

declare global {
  interface HTMLElementTagNameMap {
    "ubookit-bookings-list": UBookItBookingsListElement;
  }
}
