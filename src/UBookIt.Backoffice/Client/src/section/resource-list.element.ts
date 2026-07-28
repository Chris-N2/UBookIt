import { css, html, customElement, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UBookItBackofficeService } from "../api/index.js";
import type { ResourceResponseModel } from "../api/index.js";
import { toApiErrors } from "./api-errors.js";

const PAGE_SIZE = 20;

/**
 * Collection view: a semantic table of resources with paging and create /
 * edit / delete affordances. Editing happens in the workspace editor, never
 * inline.
 */
@customElement("ubookit-resource-list")
export class UBookItResourceListElement extends UmbLitElement {
  @state()
  private _items: ResourceResponseModel[] = [];

  @state()
  private _total = 0;

  @state()
  private _skip = 0;

  @state()
  private _loading = true;

  @state()
  private _error?: string;

  #term(key: string) {
    return this.localize.term(`ubookitResources_${key}`);
  }

  override connectedCallback() {
    super.connectedCallback();
    void this.#load();
  }

  async #load() {
    this._loading = true;
    this._error = undefined;

    try {
      const { data, error } = await UBookItBackofficeService.listResources({
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

  async #delete(resource: ResourceResponseModel) {
    if (!window.confirm(`Delete "${resource.displayName}"? This cannot be undone.`)) {
      return;
    }

    let failure: unknown;
    try {
      const { error } = await UBookItBackofficeService.deleteResource({ path: { id: resource.id } });
      if (error) {
        failure = error;
      }
    } catch (thrown) {
      failure = thrown ?? new Error("delete failed");
    }

    if (failure !== undefined) {
      this._error = toApiErrors(failure, "").some((e) => e.code === "resource-in-use")
        ? `"${resource.displayName}" has bookings and cannot be deleted.`
        : `"${resource.displayName}" could not be deleted.`;
      return;
    }

    if (this._items.length === 1 && this._skip > 0) {
      this._skip -= PAGE_SIZE;
    }
    await this.#load();
  }

  #summarize(resource: ResourceResponseModel): string {
    const windows = resource.openingHours.length;
    const exceptions = resource.exceptions.length;
    const windowsPart = windows === 0 ? "no opening hours" : `${windows} window${windows === 1 ? "" : "s"}/week`;
    const exceptionsPart = exceptions === 0 ? "" : `, ${exceptions} exception${exceptions === 1 ? "" : "s"}`;
    return `${windowsPart}${exceptionsPart}`;
  }

  #edit(id: string) {
    this.dispatchEvent(new CustomEvent("ubookit-edit", { detail: { id } }));
  }

  override render() {
    const pageEnd = Math.min(this._skip + PAGE_SIZE, this._total);

    return html`
      <div class="header">
        <h2>${this.localize.term("ubookitResources_label")}</h2>
        <uui-button
          look="primary"
          label=${this.localize.term("ubookitResources_create")}
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
      return html`<p>${this.localize.term("ubookitResources_empty")}</p>`;
    }

    return html`
      <uui-table aria-label=${this.#term("tableLabel")}>
        <uui-table-head>
          <uui-table-head-cell>${this.#term("name")}</uui-table-head-cell>
          <uui-table-head-cell>${this.#term("type")}</uui-table-head-cell>
          <uui-table-head-cell>${this.#term("availability")}</uui-table-head-cell>
          <uui-table-head-cell><span class="visually-hidden">${this.#term("actions")}</span></uui-table-head-cell>
        </uui-table-head>
        ${this._items.map(
          (resource) => html`
            <uui-table-row>
              <uui-table-cell>${resource.displayName}</uui-table-cell>
              <uui-table-cell>${resource.type}</uui-table-cell>
              <uui-table-cell>${this.#summarize(resource)}</uui-table-cell>
              <uui-table-cell>
                <uui-button
                  look="secondary"
                  label="${this.localize.term("ubookitResources_edit")} ${resource.displayName}"
                  @click=${() => this.#edit(resource.id)}
                ></uui-button>
                <uui-button
                  look="secondary"
                  color="danger"
                  label="${this.localize.term("ubookitResources_delete")} ${resource.displayName}"
                  @click=${() => this.#delete(resource)}
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
          ${this.localize.term("ubookitResources_showing", this._skip + 1, pageEnd, this._total)}
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
    "ubookit-resource-list": UBookItResourceListElement;
  }
}
