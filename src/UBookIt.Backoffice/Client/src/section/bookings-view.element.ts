import { css, html, customElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import "./bookings-list.element.js";

/**
 * Root of the uBookIt Bookings section view.
 *
 * Unlike the Resources and Services views this routes nowhere: v1 backoffice
 * booking management is *see* and *cancel*, cancel is its own change, and there
 * is nothing to open a booking into. The read port returns everything a row
 * shows and nothing more, so a detail view would have nothing further to
 * display.
 *
 * No collection→workspace seam is prepared for the change that adds cancelling,
 * because a seam built now is a guess about what that change will need — and if
 * cancelling turns out to be a row action with a confirmation, as it likely is,
 * the guess would be a routing layer nothing ever routes through.
 */
@customElement("ubookit-bookings-view")
export class UBookItBookingsViewElement extends UmbLitElement {
  override render() {
    return html`<ubookit-bookings-list></ubookit-bookings-list>`;
  }

  static override styles = css`
    :host {
      display: block;
      padding: var(--uui-size-layout-1);
    }
  `;
}

export default UBookItBookingsViewElement;

declare global {
  interface HTMLElementTagNameMap {
    "ubookit-bookings-view": UBookItBookingsViewElement;
  }
}
