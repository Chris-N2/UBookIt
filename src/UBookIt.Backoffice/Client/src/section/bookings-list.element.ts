import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_CURRENT_USER_CONTEXT } from "@umbraco-cms/backoffice/current-user";
import { umbOpenModal } from "@umbraco-cms/backoffice/modal";
import { UBookItBackofficeService } from "../api/index.js";
import type { BookingModel } from "../api/index.js";
import { toApiErrors } from "./api-errors.js";
import { confirmDestructive } from "./confirm.js";
import { canManageBookings } from "./permission-verbs.js";
import { UBOOKIT_MOVE_BOOKING_MODAL } from "./move-booking-modal.token.js";
import { UBOOKIT_PLACE_ON_BEHALF_MODAL } from "./place-on-behalf-modal.token.js";
import { placedInsideWindow, placedLocalDate } from "./place-on-behalf-fields.js";
import { classify, refusalTermFor, windowControlsApply, type FindMode } from "./find-fields.js";
import {
  actionFor,
  bookerCell,
  bookerNote,
  bookingReference,
  canCancel,
  canConfirmOrDecline,
  canMove,
  currentWeek,
  formatInterval,
  listQuery,
  serviceLabel,
  shouldLoad,
  showsEmptyMessage,
  skipAfter,
  skipAfterEmptyPage,
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
 * semantic table, prev/next paging, and cancellation.
 *
 * Every management verb is a row action rather than a workspace — there is
 * still nothing to open a booking into, because the list already shows
 * everything the read port carries. Cancel and Move work on anything still
 * holding its time; confirm and decline appear only on a Requested row, which
 * exists only on a site that has turned AutoConfirm off. Move opens a small
 * dialog from the row rather than a workspace: the dialog is the first thing
 * that has needed a form over one booking, and a workspace built for one form
 * would be the seam this view deliberately did not guess at.
 */
@customElement("ubookit-bookings-list")
export class UBookItBookingsListElement extends UmbLitElement {
  /**
   * Whether the current user's verbs allow acting on bookings (Manage). Read from the
   * current-user context; convenience only — the endpoints refuse independently, so a
   * stale value here can never authorize anything. Without Manage the whole Actions
   * column goes, header included: a column of nothing reads as data that failed to
   * load, which is the same reasoning the booker cell records.
   */
  @state()
  private _canManage = false;

  /**
   * Whether the current user may handle a booker's personal data.
   *
   * Read from the SAME context the verbs come from, and for the same reason: recording a booking
   * takes a name and an address, so the endpoint requires this alongside the manage verb. A
   * control that is always refused teaches an operator to ignore failures, which is the
   * reasoning the move requirement already records — so an operator who cannot place a booking
   * is not offered the control.
   *
   * Convenience only. The endpoint refuses independently, so a stale value here can never
   * authorize anything.
   */
  @state()
  private _canSeePersonalData = false;

  constructor() {
    super();
    this.consumeContext(UMB_CURRENT_USER_CONTEXT, (context) => {
      this.observe(context?.currentUser, (currentUser) => {
        this._canManage = canManageBookings(currentUser?.fallbackPermissions);
        this._canSeePersonalData = currentUser?.hasAccessToSensitiveData === true;
      });
    });
  }

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

  /**
   * Where the last moved booking went, shown above the table until the next action.
   *
   * A moved booking may leave the current window and vanish from the table, and a row that
   * disappears without a word reads as a booking that was lost. role=status rather than
   * alert: it is a confirmation, not an interruption.
   */
  @state()
  private _notice?: string;

  @state()
  private _statuses: string[] = [];

  @state()
  private _window = currentWeek(new Date());

  /**
   * Which read the table shows: the windowed list, or one of the two lookups.
   *
   * A lookup REPLACES the list rather than filtering it, because neither lookup has a window and
   * pretending the window still applied would be a lie the filters told. Everything that reloads
   * — a row action, a page change — reloads whatever this says, so a booking found by its
   * reference and moved to another date is still the booking that was found.
   */
  @state()
  private _mode: FindMode = { mode: "window" };

  @state()
  private _findText = "";

  /** A refusal of the Find control's own input, before or after a round trip. */
  @state()
  private _findError?: string;

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
      // Which read depends on the mode; see #fetch, which carries the window's own
      // note about dates and zones.
      const { data, error } = await this.#fetch();

      if (!current()) {
        return;
      }

      if (error || !data) {
        if (windowControlsApply(this._mode)) {
          // The endpoint reports an over-wide window in terms of the dates that
          // were sent, so the message is shown rather than replaced by a generic
          // one — it names something the operator can act on, and the dates it
          // names are the ones in the two controls above.
          this._error = toApiErrors(error, this.#term("listLoadFailed"))
            .map((failure) => failure.message)
            .filter(Boolean)
            .join(" ") || this.#term("listLoadFailed");
        } else {
          // A lookup the server refused is a fault in what was TYPED, in the operator's words,
          // associated with the control that took it, not a list that failed to load.
          const subject =
            this._mode.mode === "reference" ? this._mode.canonical : this._mode.mode === "email" ? this._mode.email : "";
          this._findError = this.localize.term(
            `ubookitBookings_${refusalTermFor(toApiErrors(error, this.#term("findFailed")))}`,
            subject,
          );
          this._mode = { mode: "window" };
        }
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
  /**
   * One read per mode, all answering in the list's page shape so `#load` has one branch.
   *
   * The reference lookup answers with ONE row or a 404; both become a page here — one item, or
   * none with a total of zero — so the table, the pager and the empty state need no third case.
   * A miss is not an error: the status line says "no booking has that reference", and `_error`
   * stays for a request that actually failed.
   */
  async #fetch(): Promise<{ data?: { items: BookingModel[]; total: number }; error?: unknown }> {
    switch (this._mode.mode) {
      case "window":
        // The window travels as two DATES. No instant is computed here and no zone is
        // applied: the endpoint resolves them against the site's zone, which is where that
        // rule lives precisely so it is not reimplemented once per client and got
        // differently wrong at a daylight-saving edge. The query is built by `listQuery`
        // so that shape is assertable — a wrong window still returns a plausible list.
        return UBookItBackofficeService.listBookings({
          query: listQuery(this._window, this._statuses, this._skip, PAGE_SIZE),
        });
      case "email":
        return UBookItBackofficeService.findBookingsByBooker({
          body: { email: this._mode.email, skip: this._skip, take: PAGE_SIZE },
        });
      case "reference": {
        const { data, error, response } = await UBookItBackofficeService.findBookingByReference({
          path: { reference: this._mode.canonical },
        });
        if (data) {
          return { data: { items: [data], total: 1 } };
        }
        // 404 is a miss, not a failure. Anything else is.
        return response?.status === 404 ? { data: { items: [], total: 0 } } : { error };
      }
    }
  }

  /**
   * Runs whichever lookup the typed text calls for — decided from its SHAPE, never from a mode
   * the operator picked. The classification is a convenience: a value that passes here and fails
   * on the server is shown the server's code.
   */
  #find(event: Event) {
    event.preventDefault();
    this._findError = undefined;

    const kind = classify(this._findText, this._canSeePersonalData);

    switch (kind.kind) {
      case "neither":
        this._findError = this.#term("findNeither");
        return;
      case "email-not-offered":
        // Told WHY, and who can — the same courtesy the withheld-details note pays a row.
        this._findError = this.#term("findEmailNotOffered");
        return;
      case "reference":
        this._mode = { mode: "reference", canonical: kind.canonical };
        break;
      case "email":
        this._mode = { mode: "email", email: kind.email };
        break;
    }

    this._skip = 0;
    this._notice = undefined;
    void this.#load();
  }

  /**
   * Restores the windowed list and puts focus on its first control. Focus is placed rather than
   * left, because the controls this returns to were hidden a moment ago and the document is
   * where focus goes by default.
   */
  async #backToDates() {
    this._mode = { mode: "window" };
    this._findError = undefined;
    this._skip = 0;
    void this.#load();
    await this.updateComplete;
    this.shadowRoot?.querySelector<HTMLElement>("#ubookit-bookings-from")?.focus();
  }

  /** The status line for a lookup mode, including the miss. */
  #renderLookupStatus() {
    if (this._mode.mode === "window") {
      return nothing;
    }

    const shown =
      this._mode.mode === "reference"
        ? this.localize.term("ubookitBookings_findShowingReference", this._mode.canonical)
        : this.localize.term("ubookitBookings_findShowingEmail", this._mode.email);

    // A miss is a SENTENCE, not an empty table: an empty table under a window means "nothing
    // booked", under a lookup it would mean "no such booking", and the two look identical.
    const miss =
      !this._loading && this._total === 0 && this._error === undefined
        ? this._mode.mode === "reference"
          ? this.localize.term("ubookitBookings_findNotFoundReference", this._mode.canonical)
          : this.localize.term("ubookitBookings_findNotFoundEmail", this._mode.email)
        : undefined;

    return html`
      <p role="status" class="notice lookup-status">
        ${miss ?? shown}
        <uui-button
          look="secondary"
          compact
          label=${this.#term("findBackToDates")}
          @click=${() => this.#backToDates()}
        ></uui-button>
      </p>
    `;
  }

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
        <!--
          tabindex="-1" so cancellation can put focus here afterwards: it is not in the tab
          order, and only code can move focus to it. Without it the browser refuses, and
          focus lands on <body> instead.
        -->
        <h2 tabindex="-1">${this.#term("label")}</h2>

        <!--
          Offered from the VIEW rather than from a row, because the booking does not exist yet.
          Hidden unless the user holds both gates the endpoint requires — the manage verb and
          sensitive-data access — so that a control which would always be refused is not shown
          at all. The server refuses independently regardless; this is convenience, never the
          rule.
        -->
        ${this._canManage && this._canSeePersonalData
          ? html`<uui-button
              look="primary"
              color="positive"
              label=${this.#term("place")}
              @click=${() => this.#place()}
            ></uui-button>`
          : nothing}
      </div>

      <!--
        ONE control for both lookups, dispatched by the shape of what was typed: a reference has
        no "@", an address always does, so the operator never chooses a mode. A native input with
        a real label, for the reason every dialog in this client records. The label narrows for a
        user not offered the email route, and an address typed anyway is answered with the
        sentence naming the group, which tells them who to ask.
      -->
      <form class="find" @submit=${(event: Event) => this.#find(event)} novalidate>
        <label for="ubookit-bookings-find">
          ${this.#term(this._canSeePersonalData ? "findLabel" : "findLabelReferenceOnly")}
        </label>
        <input
          id="ubookit-bookings-find"
          type="text"
          autocomplete="off"
          aria-describedby=${this._findError ? "ubookit-bookings-find-error" : nothing}
          aria-invalid=${this._findError ? "true" : "false"}
          .value=${this._findText}
          @input=${(event: Event) => {
            this._findText = (event.target as HTMLInputElement).value;
            this._findError = undefined;
          }}
        />
        <uui-button look="secondary" label=${this.#term("findSubmit")} type="submit"></uui-button>
        ${this._findError
          ? html`<p id="ubookit-bookings-find-error" role="alert" class="error">${this._findError}</p>`
          : nothing}
      </form>

      ${this.#renderLookupStatus()}
      ${windowControlsApply(this._mode) ? this.#renderControls() : nothing}

      <!--
        role=alert so a failed load is ANNOUNCED, not merely rendered. An empty
        table and a failed request look identical to a reader and mean opposite
        things: "nothing is booked this week" versus "we could not find out".
      -->
      ${this._error ? html`<div role="alert" class="error">${this._error}</div>` : nothing}
      ${this._notice ? html`<p role="status" class="notice">${this._notice}</p>` : nothing}
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
          NATIVE CHECKBOXES, not uui-toggle, and the reason is the hint.

          The hint is the load-bearing part of this control: it is how an
          operator learns that a cancelled booking is one tick away rather than
          gone, and that ticking replaces rather than adds. It has to be
          ASSOCIATED with the controls, and a uui component's internal input is
          not reachable from the host attribute — uui-boolean-input forwards
          aria-label and aria-labelledby and nothing else, so aria-describedby
          on the host is dropped.

          This is not a new judgement. The resource editor made exactly this
          call, for a hint it treated as less load-bearing than this one, and
          wrote down why. Two earlier attempts here put the reference on the
          fieldset instead and CLAIMED it was announced; it was only valid — a
          fieldset is role=group, aria-describedby is not inherited by
          descendants, and a screen reader announces a group's name on entry
          rather than its description. Relying on DOM order would have left a
          user in focus mode, tabbing between the four controls, never meeting
          the paragraph at all.

          Checkboxes are also the more honest semantics: four independent
          filters, not four switches.
        -->
        <fieldset class="statuses">
          <legend>${this.#term("statusFilter")}</legend>
          <p class="hint" id="ubookit-bookings-status-hint">${this.#term("statusHint")}</p>
          ${STATUSES.map(
            (status) => html`
              <div class="checkbox-field">
                <input
                  id="ubookit-bookings-status-${status.toLowerCase()}"
                  type="checkbox"
                  aria-describedby="ubookit-bookings-status-hint"
                  .checked=${this._statuses.includes(status)}
                  @change=${(event: Event) =>
                    this.#toggleStatus(status, (event.target as HTMLInputElement).checked)}
                />
                <label for="ubookit-bookings-status-${status.toLowerCase()}">
                  ${this.#term(`status${status}`)}
                </label>
              </div>
            `,
          )}
        </fieldset>
      </div>
    `;
  }

  #renderTable(pageEnd: number) {
    if (this._total === 0) {
      // In a lookup mode the miss is already stated by the status line; rendering the window's
      // "nothing booked" sentence under it would say two different things about one absence.
      if (!windowControlsApply(this._mode)) {
        return nothing;
      }

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
      <!--
        Shown once per page, immediately before the table, when any row's booker was
        withheld — so a reader meets the explanation on the way in rather than after
        wondering about the cells.

        role=status, NOT role=alert. The alert above is an interruption because a
        failed load changes what the reader should do; this is a standing
        explanation of what they are looking at, and announcing it assertively
        would talk over them on every page. status announces politely when it
        appears — which covers paging from a page with no hidden rows to one with
        them — and reading order covers the first render, where a live region
        would not announce at all.

        Not aria-describedby on the table: no uui-* component carries that
        attribute through to its shadow root, so the association would be written,
        look correct, and reach nothing.
      -->
      ${bookerNote(this._items, this.#term("bookerHiddenNote")).map(
        (note) => html`<p role="status" class="withheld-note">${note}</p>`,
      )}

      <uui-table aria-label=${this.#term("tableLabel")}>
        <uui-table-head>
          <uui-table-head-cell>${this.#term("reference")}</uui-table-head-cell>
          <uui-table-head-cell>${this.#term("when")}</uui-table-head-cell>
          <uui-table-head-cell>${this.#term("booker")}</uui-table-head-cell>
          <uui-table-head-cell>${this.#term("resources")}</uui-table-head-cell>
          <uui-table-head-cell>${this.#term("service")}</uui-table-head-cell>
          <uui-table-head-cell>${this.#term("status")}</uui-table-head-cell>
          ${this._canManage
            ? html`<uui-table-head-cell>
                <span class="visually-hidden">${this.#term("actions")}</span>
              </uui-table-head-cell>`
            : nothing}
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

  /**
   * The Booker cell, derived in `booking-rows` and only rendered here.
   *
   * The union's arms cannot be swapped without a type error — the `hidden` variant
   * has no `name` — which is the protection the previous ternary lacked: swapping it
   * rendered an empty cell for a withheld row and "Contact details hidden" for a row
   * whose details were supplied, with every guard still green.
   */
  #bookerCell(booking: BookingModel) {
    const cell = bookerCell(booking, this.#term("bookerHidden"), this.#term("bookerErased"));

    // Switched on the discriminant rather than chained ternaries, so that adding a fourth
    // condition to the union is a compile error here instead of falling silently into the
    // last arm — which for a cell about personal data would render somebody's name under
    // the wrong explanation.
    switch (cell.kind) {
      case "hidden":
        return html`<span class="withheld">${cell.label}</span>`;
      case "erased":
        // Its own class, not `withheld`. The entire requirement is that these two read as
        // different things; sharing a class leaves a site unable to style them apart and
        // quietly says they are the same kind of absence.
        return html`<span class="erased">${cell.label}</span>`;
      case "shown":
        return html`
          ${cell.name}
          <div class="secondary">${cell.email}</div>
        `;
    }
  }

  #renderRow(booking: BookingModel, showZone: boolean) {
    // The reader's own locale for month names and clock format; the ZONE comes
    // from the booking. Those are different questions: how a time is written is
    // the reader's, which time it is was decided when the booking was placed.
    const interval = formatInterval(booking);

    return html`
      <uui-table-row>
        <uui-table-cell><span class="reference">${bookingReference(booking)}</span></uui-table-cell>
        <uui-table-cell>
          ${interval.text}${showZone ? html` <span class="zone">${interval.zone}</span>` : nothing}
        </uui-table-cell>
        <uui-table-cell>
          <!--
            Words, not a blank cell — the same reason "Booked directly" is words.
            A blank here reads as data that failed to load, and the explanation
            above the table says why it is missing.
          -->
          ${this.#bookerCell(booking)}
        </uui-table-cell>
        <uui-table-cell>
          ${booking.resources.map((resource) => resource.displayName).join(", ")}
        </uui-table-cell>
        <uui-table-cell>${serviceLabel(booking, this.#term("bookedDirectly"))}</uui-table-cell>
        <uui-table-cell>${this.#term(`status${booking.status}`)}</uui-table-cell>
        ${this._canManage ? html`<uui-table-cell>
          <!--
            Offered only where the domain would allow it. A control that is always
            refused teaches an operator to ignore failures — and the endpoint refuses
            independently anyway, so this is a convenience rather than the rule.
          -->
          ${canConfirmOrDecline(booking.status)
            ? html`<uui-button
                  look="primary"
                  color="positive"
                  label="${this.#term("confirm")} ${bookingReference(booking)}"
                  @click=${() => this.#confirm(booking)}
                ></uui-button>
                <uui-button
                  look="secondary"
                  color="danger"
                  label="${this.#term("decline")} ${bookingReference(booking)}"
                  @click=${() => this.#decline(booking)}
                ></uui-button>`
            : nothing}
          ${canMove(booking.status)
            ? html`<uui-button
                look="secondary"
                label="${this.#term("move")} ${bookingReference(booking)}"
                @click=${() => this.#move(booking)}
              ></uui-button>`
            : nothing}
          ${canCancel(booking.status)
            ? html`<uui-button
                look="secondary"
                color="danger"
                label="${this.#term("cancel")} ${bookingReference(booking)}"
                @click=${() => this.#cancel(booking)}
              ></uui-button>`
            : nothing}
        </uui-table-cell>` : nothing}
      </uui-table-row>
    `;
  }

  /**
   * Opens the move dialog for a row. The dialog does the request itself and stays open on a
   * refusal, so all that arrives here is the outcome: moved (with where to), backed out, or
   * a dialog that could not be shown — which is reported, not treated as a refusal.
   */
  async #move(booking: BookingModel) {
    this._error = undefined;
    this._notice = undefined;

    let moved: { startUtc: string; endUtc: string; timeZoneId: string };

    try {
      moved = await umbOpenModal(this, UBOOKIT_MOVE_BOOKING_MODAL, { data: { booking } });
    } catch (reason) {
      // The same two shapes confirmDestructive treats as a user-initiated dismissal; anything
      // else is the dialog failing to appear, which an operator must be told about because
      // the alternative is pressing Move and seeing nothing happen.
      const dismissed =
        reason === undefined ||
        (typeof reason === "object" && reason !== null && (reason as { type?: unknown }).type === "close");

      if (!dismissed) {
        console.error("[uBookIt] Move dialog could not be shown", reason);
        this._error = this.#term("moveDialogFailed");
      }

      // Backed out, nothing changed: focus returns to the control that opened the dialog,
      // which is still in the DOM because the row is unchanged.
      await this.updateComplete;
      this.shadowRoot
        ?.querySelector<HTMLElement>(`uui-button[label="${this.#term("move")} ${bookingReference(booking)}"]`)
        ?.focus();

      return;
    }

    // Where it went, in the booking's own zone, before the reload — the row may not survive it.
    const { text, zone } = formatInterval({ ...booking, ...moved });
    this._notice = this.localize.term("ubookitBookings_movedNotice", bookingReference(booking), `${text} (${zone})`);

    await this.#settleAfterRowAction();
  }

  /**
   * Opens the placement dialog. The dialog does the request itself and stays open on a refusal,
   * so all that arrives here is the outcome: placed (with its reference and interval), backed
   * out, or a dialog that could not be shown — which is reported, not treated as a refusal.
   */
  async #place() {
    this._error = undefined;
    this._notice = undefined;

    let placed: { reference: string; startUtc: string; endUtc: string; timeZoneId: string };

    try {
      placed = await umbOpenModal(this, UBOOKIT_PLACE_ON_BEHALF_MODAL, {
        data: { from: this._window.from, to: this._window.to },
      });
    } catch (reason) {
      const dismissed =
        reason === undefined ||
        (typeof reason === "object" && reason !== null && (reason as { type?: unknown }).type === "close");

      if (!dismissed) {
        console.error("[uBookIt] Booking dialog could not be shown", reason);
        this._error = this.#term("placeDialogFailed");
      }

      // Backed out, nothing placed: focus returns to the control that opened the dialog.
      //
      // Matched on the LABEL ATTRIBUTE's value rather than interpolated into a selector: a
      // translation containing a double quote makes an attribute selector invalid, the query
      // throws, and focus is lost to the document — which is the exact failure this block
      // exists to prevent, arriving through the code meant to prevent it.
      await this.updateComplete;
      const label = this.#term("place");
      [...(this.shadowRoot?.querySelectorAll<HTMLElement>("uui-button") ?? [])]
        .find((button) => button.getAttribute("label") === label)
        ?.focus();

      return;
    }

    // THE REFERENCE, ALWAYS — it is what the operator reads out to the person on the telephone,
    // and making them find it by reading the booking back would be a worse screen for no reason.
    //
    // AND WHERE IT WENT, when it went somewhere this list is not showing. Every other action on
    // this screen operates on a row already in front of the operator; this one can produce a
    // booking for next month on a screen showing this week, and a table that does not change is
    // indistinguishable from a failure.
    const localDate = placedLocalDate(placed.startUtc, placed.timeZoneId);

    this._notice = placedInsideWindow(localDate, this._window.from, this._window.to)
      ? this.localize.term("ubookitBookings_placedNotice", placed.reference)
      : this.localize.term("ubookitBookings_placedOutsideWindowNotice", placed.reference, localDate);

    await this.#settleAfterRowAction();
  }

  async #cancel(booking: BookingModel) {
    const outcome = await confirmDestructive(this, {
      headline: this.#term("confirmCancelHeadline"),
      // By reference, for every operator — see the localization entry. An operator
      // without sensitive-data access has no name to be shown, and branching on that
      // would leave two behaviours where the reference serves both better.
      content: this.localize.term(
        "ubookitBookings_confirmCancelContent",
        bookingReference(booking),
      ),
      confirmLabel: this.#term("confirmCancel"),
    });

    // Three outcomes, not two. A confirmation that failed to appear is not the
    // operator declining — treating it as one turns a broken dialog into a silent
    // no-op, where they press Cancel, confirm nothing, and see no request, message
    // or trace. The rule itself lives in `actionFor`, where it is testable and
    // shared with decline; this branch only supplies the message.
    const action = actionFor(outcome);

    if (action === "report-failure") {
      this._error = this.#term("confirmFailed");
      return;
    }

    if (action === "abort") {
      return;
    }

    await this.#applyRowAction(
      () => UBookItBackofficeService.cancelBooking({ path: { id: booking.bookingId } }),
      "cancelFailed",
    );
  }

  /**
   * Confirmation of a booking has NO dialog, deliberately: it is the expected
   * disposition of a request, and a confirmed booking can still be cancelled —
   * unlike decline and cancel, which are terminal or outward-facing and get one.
   */
  async #confirm(booking: BookingModel) {
    await this.#applyRowAction(
      () => UBookItBackofficeService.confirmBooking({ path: { id: booking.bookingId } }),
      "confirmBookingFailed",
    );
  }

  async #decline(booking: BookingModel) {
    const outcome = await confirmDestructive(this, {
      headline: this.#term("confirmDeclineHeadline"),
      // By reference, for every operator, on the cancel dialog's reasoning.
      content: this.localize.term(
        "ubookitBookings_confirmDeclineContent",
        bookingReference(booking),
      ),
      confirmLabel: this.#term("confirmDecline"),
    });

    // Three outcomes, not two — a confirmation that failed to appear is not the
    // operator declining the dialog. Same rule as cancel's, from the same function.
    const action = actionFor(outcome);

    if (action === "report-failure") {
      this._error = this.#term("declineConfirmFailed");
      return;
    }

    if (action === "abort") {
      return;
    }

    await this.#applyRowAction(
      () => UBookItBackofficeService.declineBooking({ path: { id: booking.bookingId } }),
      "declineBookingFailed",
    );
  }

  /**
   * Sends one row-changing request and settles the view afterwards — shared by
   * cancel, confirm and decline, because every one of them changes what the
   * current query matches and all three must fail loudly and land the operator
   * somewhere deliberate.
   */
  async #applyRowAction(
    send: () => Promise<{ error?: unknown }>,
    failedTerm: string,
  ) {
    try {
      const { error } = await send();

      if (error) {
        // Shown rather than swallowed. The domain refuses a booking somebody else
        // already dealt with, and an operator whose row simply stopped offering the
        // button would have no idea why.
        this._error = toApiErrors(error, this.#term(failedTerm))
          .map((failure) => failure.message)
          .filter(Boolean)
          .join(" ") || this.#term(failedTerm);
        return;
      }
    } catch (thrown) {
      this._error = toApiErrors(thrown, this.#term(failedTerm))
        .map((failure) => failure.message)
        .filter(Boolean)
        .join(" ") || this.#term(failedTerm);
      return;
    }

    await this.#settleAfterRowAction();
  }

  /**
   * What every row action does once the server has agreed: reload, step back off an
   * emptied page, and put focus somewhere deliberate. Shared by cancel, confirm, decline
   * and move.
   */
  async #settleAfterRowAction() {
    // Reload rather than patch the row in place. The action changes what the query
    // matches — the default filter excludes cancelled and declined bookings, a
    // confirmed one leaves a Requested-only filter, and a moved one may leave the
    // window — and the total changes with it. Editing the row would show a booking
    // the current query no longer selects.
    await this.#load();

    // Removing the last row on a page leaves `skip` past the end: the table renders
    // empty with "showing 21–20 of 20", and the empty message is suppressed because the
    // total is not zero. Step back a page and ask again — once, since the retry only
    // happens when the page came back empty.
    const stepped = skipAfterEmptyPage(this._skip, this._items.length, PAGE_SIZE);

    if (stepped !== this._skip) {
      this._skip = stepped;
      await this.#load();
    }

    // And put focus somewhere, because the button that had it may have been removed
    // from the DOM along with its row. Left alone, focus falls to <body> and a keyboard
    // operator working through several bookings restarts their traversal every time.
    //
    // The heading, rather than another row: which row is "next" depends on a filter that
    // just changed under them, and guessing wrong moves them somewhere they did not ask
    // to be. The heading is where the list begins.
    await this.updateComplete;
    this.shadowRoot?.querySelector<HTMLElement>("h2")?.focus();
  }

  static override styles = css`
    .find {
      display: flex;
      flex-wrap: wrap;
      align-items: end;
      gap: var(--uui-size-space-3);
      margin-bottom: var(--uui-size-space-4);
    }
    .find label {
      flex-basis: 100%;
      font-weight: 700;
    }
    .find input {
      font: inherit;
      padding: var(--uui-size-space-2);
      border: 1px solid var(--uui-color-border);
      border-radius: var(--uui-border-radius);
      background: var(--uui-color-surface);
      color: inherit;
      min-width: 20rem;
    }
    .find .error {
      flex-basis: 100%;
      margin: 0;
    }
    .lookup-status {
      display: flex;
      align-items: center;
      gap: var(--uui-size-space-3);
    }
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
    .checkbox-field {
      align-items: center;
      display: flex;
      gap: var(--uui-size-space-2);
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
    /*
      Italic rather than a lighter colour ALONE: this cell says something different
      from its neighbours and the difference should not rest on a hue. It still
      takes the backoffice's own muted token rather than a colour of its own, so a
      site's theme keeps deciding contrast.
    */
    .withheld,
    .erased {
      font-style: italic;
      color: var(--uui-color-text-alt);
    }
    .withheld-note {
      margin: var(--uui-size-space-3) 0;
      color: var(--uui-color-text-alt);
    }
    /*
      Tabular figures and a monospace face, because this column exists to be read out and
      typed back. Proportional digits make an eight-symbol code harder to keep your place in,
      and the reference is the one cell an operator scans down while somebody talks.
    */
    .reference {
      font-family: var(--uui-font-monospace, monospace);
      font-variant-numeric: tabular-nums;
      white-space: nowrap;
    }
    /*
      Copied the markup from the resource and service lists and not this rule, so the
      column heading meant to be screen-reader-only was rendered to everyone. Shadow DOM
      does not inherit page classes and this element adopts no shared stylesheet, so the
      class had no meaning here at all.
    */
    .visually-hidden {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip: rect(0 0 0 0);
      white-space: nowrap;
    }
    .error {
      color: var(--uui-color-danger, #d42054);
      margin: var(--uui-size-space-3) 0;
    }
    .notice {
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
