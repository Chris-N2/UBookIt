import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_CURRENT_USER_CONTEXT } from "@umbraco-cms/backoffice/current-user";
import { canManageClosures, canReadClosures } from "./permission-verbs.js";
import { UBookItBackofficeService } from "../api/index.js";
import type { SiteClosureModel } from "../api/index.js";
import { toApiErrors } from "./api-errors.js";
import { listQuery, showsEditingControls } from "./closure-fields.js";

/** A closure being added or edited, before it is sent. */
interface ClosureDraft {
  id?: string;
  date: string;
  label: string;
}

/**
 * The site closure list.
 *
 * Two grants, mirroring the server: reading is satisfied by Configure OR Settings, so an
 * operator who edits resources can see what is closing them; changing needs Settings alone,
 * because one entry shuts every resource the site has.
 *
 * A user who may read but not write is TOLD SO, with where the grant is given, rather than
 * shown controls whose use would be refused. The settings verb is never seeded, so on an
 * upgraded site nobody holds it until an administrator ticks the box — a screen that merely
 * rendered inert buttons would read as a feature that failed to ship.
 *
 * Accessibility notes follow the resources editor: native inputs are labelled with
 * `label[for]` inside this shadow root, uui controls take their name from `label`, and the
 * error summary is focused and announced through `role="alert"`.
 */
@customElement("ubookit-closures-view")
export class UBookItClosuresViewElement extends UmbLitElement {
  @state()
  private _closures: SiteClosureModel[] = [];

  @state()
  private _loading = true;

  /** Undefined until the current user is known — neither granted nor refused yet. */
  @state()
  private _canRead?: boolean;

  @state()
  private _canWrite = false;

  @state()
  private _includePast = false;

  @state()
  private _error?: string;

  @state()
  private _draft?: ClosureDraft;

  @state()
  private _busy = false;

  #term(key: string) {
    return this.localize.term(`ubookitClosures_${key}`);
  }

  constructor() {
    super();

    this.consumeContext(UMB_CURRENT_USER_CONTEXT, (context) => {
      this.observe(context?.currentUser, (currentUser) => {
        const permissions = currentUser?.fallbackPermissions;
        const canRead = canReadClosures(permissions);

        this._canRead = canRead;
        this._canWrite = canManageClosures(permissions);

        if (canRead && this._closures.length === 0) {
          void this.#load();
        } else if (!canRead) {
          this._loading = false;
        }
      });
    });
  }

  async #load() {
    this._loading = true;
    this._error = undefined;

    try {
      // The filter is the SERVER's: the default list is what it returns, not a full list
      // with rows hidden here.
      const { data, error } = await UBookItBackofficeService.listClosures({
        query: listQuery(this._includePast),
      });

      if (error || !data) {
        this._error = this.#term("loadFailed");
      } else {
        this._closures = data;
      }
    } catch {
      this._error = this.#term("loadFailed");
    }

    this._loading = false;
  }

  async #togglePast() {
    this._includePast = !this._includePast;
    await this.#load();
  }

  #startAdd() {
    this._draft = { date: "", label: "" };
  }

  #startEdit(closure: SiteClosureModel) {
    this._draft = { id: closure.id, date: closure.date, label: closure.label };
  }

  #cancelDraft() {
    this._draft = undefined;
    this._error = undefined;
  }

  async #saveDraft() {
    const draft = this._draft;
    if (!draft) {
      return;
    }

    // A state only the UI can create: a row whose date was never picked. The server would
    // reject it as a binding error; this is the stable message instead.
    if (draft.date === "") {
      this._error = this.#term("dateRequired");
      await this.#focusError();
      return;
    }

    this._busy = true;
    this._error = undefined;

    try {
      const body = { date: draft.date, label: draft.label };
      const result = draft.id
        ? await UBookItBackofficeService.updateClosure({ path: { id: draft.id }, body })
        : await UBookItBackofficeService.createClosure({ body });

      if (result.error) {
        this._error = toApiErrors(result.error, this.#term("saveFailed"))[0]?.message
          ?? this.#term("saveFailed");
        await this.#focusError();
        return;
      }

      this._draft = undefined;
      await this.#load();
    } catch (thrown) {
      this._error = toApiErrors(thrown, this.#term("saveFailed"))[0]?.message ?? this.#term("saveFailed");
      await this.#focusError();
    } finally {
      this._busy = false;
    }
  }

  async #delete(closure: SiteClosureModel) {
    this._busy = true;
    this._error = undefined;

    try {
      const result = await UBookItBackofficeService.deleteClosure({ path: { id: closure.id } });

      if (result.error) {
        this._error = this.#term("deleteFailed");
        await this.#focusError();
        return;
      }

      await this.#load();
    } catch {
      this._error = this.#term("deleteFailed");
      await this.#focusError();
    } finally {
      this._busy = false;
    }
  }

  async #focusError() {
    await this.updateComplete;
    this.shadowRoot?.querySelector<HTMLElement>("#closure-error")?.focus();
  }

  override render() {
    if (this._canRead === undefined || this._loading) {
      return html`<uui-loader></uui-loader>`;
    }

    if (!this._canRead) {
      // A DIFFERENT message from the read-only banner: this reader cannot see the list at all,
      // and telling them they cannot CHANGE it would answer a question they did not ask.
      return html`<uui-box headline=${this.#term("label")}>
        <p>${this.#term("notPermittedRead")}</p>
      </uui-box>`;
    }

    return html`
      <uui-box headline=${this.#term("label")}>
        <p>${this.#term("intro")}</p>

        <!--
          Unconditional, and it queries nothing: true of every site whatever its data. A
          count of affected bookings would be a live number rendered beside an action, stale
          by the time it is read.
        -->
        <p class="unaffected">${this.#term("bookingsUnaffected")}</p>

        ${showsEditingControls(this._canWrite)
          ? nothing
          : html`<p class="read-only">${this.#term("notPermittedWrite")}</p>`}
        ${this.#renderError()} ${this.#renderList()} ${this.#renderDraft()}

        <div class="actions">
          ${showsEditingControls(this._canWrite) && !this._draft
            ? html`<uui-button
                look="primary"
                label=${this.#term("add")}
                @click=${() => this.#startAdd()}
              ></uui-button>`
            : nothing}
          <uui-button
            look="secondary"
            label=${this._includePast ? this.#term("hidePast") : this.#term("showPast")}
            @click=${() => void this.#togglePast()}
          ></uui-button>
        </div>
      </uui-box>
    `;
  }

  #renderError() {
    return this._error
      ? html`<p id="closure-error" class="error" role="alert" tabindex="-1">${this._error}</p>`
      : nothing;
  }

  #renderList() {
    if (this._closures.length === 0) {
      return html`<p>${this._includePast ? this.#term("emptyPast") : this.#term("empty")}</p>`;
    }

    return html`
      <table>
        <thead>
          <tr>
            <th scope="col">${this.#term("columnDate")}</th>
            <th scope="col">${this.#term("columnLabel")}</th>
            ${showsEditingControls(this._canWrite)
              ? html`<th scope="col">${this.#term("columnActions")}</th>`
              : nothing}
          </tr>
        </thead>
        <tbody>
          ${this._closures.map(
            (closure) => html`
              <tr>
                <td>${closure.date}</td>
                <td>${closure.label}</td>
                ${showsEditingControls(this._canWrite)
                  ? html`<td class="row-actions">
                      <uui-button
                        look="secondary"
                        compact
                        ?disabled=${this._busy}
                        label="${this.#term("edit")} ${closure.date} ${closure.label}"
                        @click=${() => this.#startEdit(closure)}
                      ></uui-button>
                      <uui-button
                        look="secondary"
                        compact
                        ?disabled=${this._busy}
                        label="${this.#term("delete")} ${closure.date} ${closure.label}"
                        @click=${() => void this.#delete(closure)}
                      ></uui-button>
                    </td>`
                  : nothing}
              </tr>
            `,
          )}
        </tbody>
      </table>
    `;
  }

  #renderDraft() {
    const draft = this._draft;
    if (!draft || !showsEditingControls(this._canWrite)) {
      return nothing;
    }

    return html`
      <fieldset>
        <legend>${draft.id ? this.#term("edit") : this.#term("add")}</legend>
        <div class="field">
          <label for="closure-date">${this.#term("date")}</label>
          <input
            id="closure-date"
            type="date"
            .value=${draft.date}
            @input=${(e: InputEvent) =>
              (this._draft = { ...draft, date: (e.target as HTMLInputElement).value })}
          />
        </div>
        <div class="field">
          <label for="closure-label">${this.#term("labelField")}</label>
          <input
            id="closure-label"
            type="text"
            aria-describedby="closure-label-hint"
            .value=${draft.label}
            @input=${(e: InputEvent) =>
              (this._draft = { ...draft, label: (e.target as HTMLInputElement).value })}
          />
          <p id="closure-label-hint" class="hint">${this.#term("labelHint")}</p>
        </div>
        <div class="actions">
          <uui-button
            look="primary"
            ?disabled=${this._busy}
            label=${this.#term("save")}
            @click=${() => void this.#saveDraft()}
          ></uui-button>
          <uui-button
            look="secondary"
            ?disabled=${this._busy}
            label=${this.#term("cancel")}
            @click=${() => this.#cancelDraft()}
          ></uui-button>
        </div>
      </fieldset>
    `;
  }

  static override styles = css`
    uui-box {
      margin-top: var(--uui-size-space-4);
    }
    table {
      border-collapse: collapse;
      width: 100%;
    }
    th,
    td {
      border-bottom: 1px solid var(--uui-color-border, #d8d7d9);
      padding: var(--uui-size-space-3);
      text-align: left;
    }
    .row-actions {
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
      margin-top: var(--uui-size-space-4);
    }
    .actions {
      display: flex;
      gap: var(--uui-size-space-3);
      margin-top: var(--uui-size-space-4);
    }
    .error {
      border: 2px solid var(--uui-color-danger, #d42054);
      padding: var(--uui-size-space-4);
    }
    .hint {
      margin: 0;
    }
  `;
}

export default UBookItClosuresViewElement;

declare global {
  interface HTMLElementTagNameMap {
    "ubookit-closures-view": UBookItClosuresViewElement;
  }
}
