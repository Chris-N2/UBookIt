import { UmbModalToken } from "@umbraco-cms/backoffice/modal";
import type { BookingModel } from "../api/index.js";

/** What the modal is opened with: the booking as the row knows it. */
export interface MoveBookingModalData {
  booking: BookingModel;
}

/**
 * What the modal resolves with on success.
 *
 * The new interval rather than a bare `true`, so the list can tell the operator where the
 * booking went — a moved booking may leave the current window and vanish from the table, and
 * a row that disappears without a word reads as a booking that was lost.
 */
export interface MoveBookingModalValue {
  startUtc: string;
  endUtc: string;
  timeZoneId: string;
}

export const UBOOKIT_MOVE_BOOKING_MODAL_ALIAS = "UBookIt.Modal.MoveBooking";

export const UBOOKIT_MOVE_BOOKING_MODAL = new UmbModalToken<MoveBookingModalData, MoveBookingModalValue>(
  UBOOKIT_MOVE_BOOKING_MODAL_ALIAS,
  {
    modal: {
      type: "dialog",
      size: "small",
    },
  },
);
