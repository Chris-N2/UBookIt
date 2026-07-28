import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import "./resource-list.element.js";
import "./resource-editor.element.js";

/**
 * Root of the uBookIt Resources section view. Routes between the collection
 * view (list) and the workspace editor for one resource — the agreed
 * collection→workspace UX, no inline editing.
 */
@customElement("ubookit-resources-view")
export class UBookItResourcesViewElement extends UmbLitElement {
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
      ? html`<ubookit-resource-editor
          .resourceId=${this._editing.id}
          @ubookit-close=${this.#onClose}
          @ubookit-saved=${this.#onClose}
        ></ubookit-resource-editor>`
      : html`<ubookit-resource-list
          @ubookit-create=${this.#onCreate}
          @ubookit-edit=${this.#onEdit}
        ></ubookit-resource-list>`;
  }

  static override styles = css`
    :host {
      display: block;
      padding: var(--uui-size-layout-1);
    }
  `;
}

export default UBookItResourcesViewElement;

declare global {
  interface HTMLElementTagNameMap {
    "ubookit-resources-view": UBookItResourcesViewElement;
  }
}
