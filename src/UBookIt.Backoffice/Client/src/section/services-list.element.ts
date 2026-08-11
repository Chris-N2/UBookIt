import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UBookItBackofficeService } from "../api/index.js";
import type { ServiceResponseModel } from "../api/index.js";
import { toApiErrors } from "./api-errors.js";
import { confirmDestructive } from "./confirm.js";

const PAGE_SIZE = 20;

/**
 * Collection view: a semantic table of services with paging and create /
 * edit / delete affordances. Editing happens in the workspace editor, never
 * inline.
 *
 * Failure messages come from the server rather than a client-side map of
 * known codes, so a code added later (a `service-in-use` delete guard, say)
 * surfaces meaningfully without a change here (design D7).
 */
@customElement("ubookit-service-list")
export class UBookItServiceListElement extends UmbLitElement {
  @state()
  private _items: ServiceResponseModel[] = [];

  @state()
  private _total = 0;

  @state()
  private _skip = 0;

  @state()
  private _loading = true;

  @state()
  private _error?: string;

  #term(key: string) {
    return this.localize.term(`ubookitServices_${key}`);
  }

  override connectedCallback() {
    super.connectedCallback();
    void this.#load();
  }

  async #load() {
    this._loading = true;
    this._error = undefined;

    try {
      const { data, error } = await UBookItBackofficeService.listServices({
        query: { skip: this._skip, take: PAGE_SIZE },
      });

      if (error || !data) {
        this._error = this.#term("listLoadFailed");
      } else {
        this._items = data.items;
        this._total = data.total;
      }
    } catch {
      this._error = this.#term("listLoadFailed");
    }

    this._loading = false;
  }

  async #delete(service: ServiceResponseModel) {
    const confirmed = await confirmDestructive(this, {
      headline: this.#term("confirmDeleteHeadline"),
      content: this.localize.term("ubookitServices_confirmDeleteContent", service.name),
      confirmLabel: this.#term("confirmDelete"),
    });

    if (!confirmed) {
      return;
    }

    let failure: unknown;
    try {
      const { error } = await UBookItBackofficeService.deleteService({ path: { id: service.id } });
      if (error) {
        failure = error;
      }
    } catch (thrown) {
      failure = thrown ?? new Error("delete failed");
    }

    if (failure !== undefined) {
      const fallback = this.localize.term("ubookitServices_serviceDeleteFailed", service.name);
      // Prefer whatever the server said; the fallback covers a response that
      // carries no usable message at all.
      this._error = toApiErrors(failure, fallback).map((e) => e.message ?? e.code).join(" ") || fallback;
      return;
    }

    if (this._items.length === 1 && this._skip > 0) {
      this._skip -= PAGE_SIZE;
    }
    await this.#load();
  }

  /** "Requires 1 × masseur" — the single v1 role, ready to list more later. */
  #summarizeRequirements(service: ServiceResponseModel): string {
    if (service.roles.length === 0) {
      return "—";
    }

    return service.roles.map((role) => `${role.count} × ${role.resourceType}`).join(", ");
  }

  #summarizeDuration(service: ServiceResponseModel): string {
    return service.durationMinutes === null || service.durationMinutes === undefined
      ? this.#term("durationInheritSummary")
      : this.localize.term("ubookitServices_durationFixedSummary", service.durationMinutes);
  }

  #edit(id: string) {
    this.dispatchEvent(new CustomEvent("ubookit-edit", { detail: { id } }));
  }

  override render() {
    const pageEnd = Math.min(this._skip + PAGE_SIZE, this._total);

    return html`
      <div class="header">
        <h2>${this.localize.term("ubookitServices_label")}</h2>
        <uui-button
          look="primary"
          label=${this.localize.term("ubookitServices_create")}
          @click=${() => this.dispatchEvent(new CustomEvent("ubookit-create"))}
        ></uui-button>
      </div>

      ${this._error ? html`<div role="alert" class="error">${this._error}</div>` : nothing}
      ${this._loading
        ? html`<uui-loader-bar aria-label=${this.#term("loadingList")}></uui-loader-bar>`
        : this.#renderTable(pageEnd)}
    `;
  }

  #renderTable(pageEnd: number) {
    if (this._total === 0) {
      return html`<p>${this.localize.term("ubookitServices_empty")}</p>`;
    }

    return html`
      <uui-table aria-label=${this.#term("tableLabel")}>
        <uui-table-head>
          <uui-table-head-cell>${this.#term("name")}</uui-table-head-cell>
          <uui-table-head-cell>${this.#term("requirementsSummary")}</uui-table-head-cell>
          <uui-table-head-cell>${this.#term("duration")}</uui-table-head-cell>
          <uui-table-head-cell><span class="visually-hidden">${this.#term("actions")}</span></uui-table-head-cell>
        </uui-table-head>
        ${this._items.map(
          (service) => html`
            <uui-table-row>
              <uui-table-cell>${service.name}</uui-table-cell>
              <uui-table-cell>${this.#summarizeRequirements(service)}</uui-table-cell>
              <uui-table-cell>${this.#summarizeDuration(service)}</uui-table-cell>
              <uui-table-cell>
                <uui-button
                  look="secondary"
                  label="${this.localize.term("ubookitServices_edit")} ${service.name}"
                  @click=${() => this.#edit(service.id)}
                ></uui-button>
                <uui-button
                  look="secondary"
                  color="danger"
                  label="${this.localize.term("ubookitServices_delete")} ${service.name}"
                  @click=${() => this.#delete(service)}
                ></uui-button>
              </uui-table-cell>
            </uui-table-row>
          `,
        )}
      </uui-table>

      <nav class="paging" aria-label=${this.#term("pagingLabel")}>
        <uui-button
          look="secondary"
          label=${this.#term("previousPage")}
          ?disabled=${this._skip === 0}
          @click=${() => {
            this._skip = Math.max(0, this._skip - PAGE_SIZE);
            void this.#load();
          }}
        ></uui-button>
        <span aria-live="polite">
          ${this.localize.term("ubookitServices_showing", this._skip + 1, pageEnd, this._total)}
        </span>
        <uui-button
          look="secondary"
          label=${this.#term("nextPage")}
          ?disabled=${pageEnd >= this._total}
          @click=${() => {
            this._skip += PAGE_SIZE;
            void this.#load();
          }}
        ></uui-button>
      </nav>
    `;
  }

  static override styles = css`
    .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: var(--uui-size-space-4);
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

declare global {
  interface HTMLElementTagNameMap {
    "ubookit-service-list": UBookItServiceListElement;
  }
}
