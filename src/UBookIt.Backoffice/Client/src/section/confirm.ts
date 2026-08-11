import { umbOpenModal, UMB_CONFIRM_MODAL } from "@umbraco-cms/backoffice/modal";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";

/**
 * Asks the user to confirm a destructive action using the backoffice's own
 * modal, and resolves to whether they went ahead.
 *
 * Replaces `window.confirm`, which is not keyboard-manageable by the page, is
 * styled by the browser rather than the backoffice, and blocks the event loop
 * (which also stalls automated verification).
 *
 * Cancelling or dismissing the modal REJECTS the underlying promise rather
 * than resolving false, so the catch is the whole point of this wrapper — an
 * unguarded `await` would raise an unhandled rejection every time a user backs
 * out of a delete.
 */
export const confirmDestructive = async (
  host: UmbControllerHost,
  options: { headline: string; content: string; confirmLabel: string },
): Promise<boolean> => {
  try {
    await umbOpenModal(host, UMB_CONFIRM_MODAL, {
      data: {
        headline: options.headline,
        content: options.content,
        confirmLabel: options.confirmLabel,
        color: "danger",
      },
    });
    return true;
  } catch {
    return false;
  }
};
