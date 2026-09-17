import { UmbModalToken } from "@umbraco-cms/backoffice/modal";

/**
 * What the dialog is opened with: the window the list is currently showing.
 *
 * The window rather than nothing, for two reasons. The date field opens on its first day, so an
 * operator taking a booking while looking at next week gets next week. And the list needs to
 * know, on success, whether the booking it just placed is one it would show — a booking placed
 * outside the window leaves the table unchanged, which is indistinguishable from a failure
 * unless somebody says otherwise.
 */
export interface PlaceOnBehalfModalData {
  /** `YYYY-MM-DD`, the first day of the window the list is showing. */
  from: string;
  /** `YYYY-MM-DD`, the last day of it. */
  to: string;
}

/**
 * What the dialog resolves with on success: the booking as it was placed.
 *
 * The reference is the point — an operator is holding somebody on the telephone and the
 * reference is what they say next. The interval comes too, so the list can tell them where it
 * went and whether it is in view.
 */
export interface PlaceOnBehalfModalValue {
  reference: string;
  startUtc: string;
  endUtc: string;
  timeZoneId: string;
}

export const UBOOKIT_PLACE_ON_BEHALF_MODAL_ALIAS = "UBookIt.Modal.PlaceOnBehalf";

export const UBOOKIT_PLACE_ON_BEHALF_MODAL = new UmbModalToken<
  PlaceOnBehalfModalData,
  PlaceOnBehalfModalValue
>(UBOOKIT_PLACE_ON_BEHALF_MODAL_ALIAS, {
  modal: {
    type: "dialog",
    size: "small",
  },
});
