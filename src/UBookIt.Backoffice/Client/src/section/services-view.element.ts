import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import "./services-list.element.js";
import "./services-editor.element.js";

/**
 * Root of the uBookIt Services section view. Routes between the collection
 * view (list) and the workspace editor for one service — the same
 * collection→workspace UX as resources, with no inline editing.
 *
 * Routing is component state, mirroring the resources view: an open editor is
 * therefore not deep-linkable or refresh-safe. Consistency with the existing
 * view was chosen over fixing it here; moving both views onto Umbraco routing
 * is its own change (design D3).
 */
@customElement("ubookit-services-view")
export class UBookItServicesViewElement extends UmbLitElement {
  @state()
  private _editing?: { id?: string };

  #onCreate = () => {
    this._editing = {};
  };

  #onEdit = (event: CustomEvent<{ id: string }>) => {
    this._editing = { id: event.detail.id };
  };

  #onClose = () => {
    this._editing = undefined;
  };

  override render() {
    return this._editing
      ? html`<ubookit-service-editor
          .serviceId=${this._editing.id}
          @ubookit-close=${this.#onClose}
          @ubookit-saved=${this.#onClose}
        ></ubookit-service-editor>`
      : html`<ubookit-service-list
          @ubookit-create=${this.#onCreate}
          @ubookit-edit=${this.#onEdit}
        ></ubookit-service-list>`;
  }

  static override styles = css`
    :host {
      display: block;
      padding: var(--uui-size-layout-1);
    }
  `;
}

export default UBookItServicesViewElement;

declare global {
  interface HTMLElementTagNameMap {
    "ubookit-services-view": UBookItServicesViewElement;
  }
}
