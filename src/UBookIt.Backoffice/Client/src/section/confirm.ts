import { umbOpenModal, UMB_CONFIRM_MODAL } from "@umbraco-cms/backoffice/modal";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";

/**
 * Outcome of a confirmation prompt. `failed` is deliberately distinct from
 * `cancelled`: both leave the action undone, but only one of them is something
 * the user chose, and treating a failure as a cancellation makes a broken
 * confirmation indistinguishable from a working one.
 */
export type ConfirmOutcome = "confirmed" | "cancelled" | "failed";

/**
 * Asks the user to confirm a destructive action using the backoffice's own
 * modal.
 *
 * Replaces `window.confirm`, which is not keyboard-manageable by the page, is
 * styled by the browser rather than the backoffice, and blocks the event loop
 * (which also stalls automated verification).
 *
 * Cancelling or dismissing REJECTS the underlying promise rather than resolving
 * false, so catching is mandatory — an unguarded `await` raises an unhandled
 * rejection every time a user backs out. Collapsing every rejection into "the
 * user cancelled" would turn a genuine fault into a silent no-op: the user
 * presses Delete, confirms nothing, and no request, message, or log appears.
 *
 * The discriminator therefore matches the CANCEL shapes and treats everything
 * else as a failure, rather than the reverse. Cancellation is the closed set:
 * the confirm modal's Cancel rejects with no argument, and backdrop/escape
 * rejects with `{type:'close'}`. Failures are open-ended and are NOT all
 * Errors — a missing modal-manager context rejects with a plain *string* from
 * UmbContextConsumer, so testing `instanceof Error` would let the most likely
 * fault through as a cancellation.
 */
export const confirmDestructive = async (
  host: UmbControllerHost,
  options: { headline: string; content: string; confirmLabel: string },
): Promise<ConfirmOutcome> => {
  try {
    await umbOpenModal(host, UMB_CONFIRM_MODAL, {
      data: {
        headline: options.headline,
        content: options.content,
        confirmLabel: options.confirmLabel,
        color: "danger",
      },
    });
    return "confirmed";
  } catch (reason) {
    if (isCancellation(reason)) {
      return "cancelled";
    }

    console.error("[uBookIt] Confirmation dialog could not be shown", reason);
    return "failed";
  }
};

/** The two shapes the backoffice uses to signal a user-initiated dismissal. */
const isCancellation = (reason: unknown): boolean =>
  reason === undefined ||
  (typeof reason === "object" && reason !== null && (reason as { type?: unknown }).type === "close");
