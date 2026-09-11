import { css, html, customElement, property, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
// Side-effect imports: the pickers are registered by the user packages, which the
// backoffice loads lazily per section — our section must pull them in itself rather
// than assume another section has. Resolved at runtime through the host's import map
// (the bundle externalizes everything under @umbraco).
import "@umbraco-cms/backoffice/user";
import "@umbraco-cms/backoffice/user-group";
import type { UmbUserInputElement } from "@umbraco-cms/backoffice/user";
import type { UmbUserGroupInputElement } from "@umbraco-cms/backoffice/user-group";
import { UBookItBackofficeService } from "../api/index.js";
import type { ResponsibilityPartyModel } from "../api/index.js";
import {
  GROUP_KIND,
  USER_KIND,
  buildAssignments,
  marksFor,
  selectionOf,
  type MarkedParty,
} from "./responsibility-fields.js";

/**
 * The Responsibility box shared by the resource and service editors: who is emailed
 * about the subject's bookings. Backed by its own endpoints, saved by the HOST editor
 * calling {@link save} after its own save succeeds — on create, the subject id does
 * not exist until then.
 *
 * A party the pickers cannot render — deleted, disabled, invitation never accepted —
 * is listed beneath them with its condition named, because the pickers draw their
 * selections from Umbraco's current data and a deleted party is precisely the one
 * they cannot show. Silently dropping it from the display would hide that a subject's
 * contact went away.
 *
 * Accessibility: the pickers are CMS-owned shadow composites, so text around them
 * cannot be programmatically associated into them (the uui/umb trap the bookings
 * screen documented). Each picker is introduced by a visible heading immediately
 * before it, and the condition list is ordinary text.
 */
@customElement("ubookit-responsibility-editor")
export class UBookItResponsibilityEditorElement extends UmbLitElement {
  @property({ type: String })
  subjectType: "resource" | "service" = "resource";

  @property({ type: String })
  subjectId?: string;

  @state()
  private _users: string[] = [];

  @state()
  private _groups: string[] = [];

  @state()
  private _marks: MarkedParty[] = [];

  @state()
  private _error = "";

  /**
   * Whether the stored assignments failed to load for an existing subject. While true,
   * {@link save} refuses to write: the pickers are empty for the wrong reason, and a
   * wholesale replace from them would erase assignments the operator never saw.
   */
  @state()
  private _loadFailed = false;

  #term(key: string) {
    return this.localize.term(`ubookitResponsibility_${key}`);
  }

  override connectedCallback() {
    super.connectedCallback();
    void this.#load();
  }

  async #load() {
    if (!this.subjectId) {
      return;
    }

    try {
      const { data, error } =
        this.subjectType === "resource"
          ? await UBookItBackofficeService.getResourceResponsibility({ path: { id: this.subjectId } })
          : await UBookItBackofficeService.getServiceResponsibility({ path: { id: this.subjectId } });

      if (error || !data) {
        this._error = this.#term("loadFailed");
        this._loadFailed = true;
        return;
      }

      this.#apply(data.assignments);
      this._loadFailed = false;
    } catch {
      this._error = this.#term("loadFailed");
      this._loadFailed = true;
    }
  }

  /**
   * Writes the pickers' current selections for the given subject. Called by the host
   * editor after its own save succeeds; the id is a parameter because on create it
   * exists only then. Returns false when the write failed — the host keeps its editor
   * open and surfaces its own message alongside this element's.
   */
  public async save(subjectId: string): Promise<boolean> {
    // Refused, not retried: the pickers are empty because the load failed, and a
    // wholesale replace from them would erase assignments the operator never saw.
    // The subject's own save has already succeeded when this runs; the host stays
    // open and this message says what did and did not happen.
    if (this._loadFailed) {
      this._error = this.#term("saveRefusedAfterLoadFailure");
      return false;
    }

    this._error = "";

    const body = { assignments: buildAssignments(this._users, this._groups) };

    try {
      const { data, error } =
        this.subjectType === "resource"
          ? await UBookItBackofficeService.putResourceResponsibility({ path: { id: subjectId }, body })
          : await UBookItBackofficeService.putServiceResponsibility({ path: { id: subjectId }, body });

      if (error || !data) {
        this._error = this.#term("saveFailed");
        return false;
      }

      this.#apply(data.assignments);
      return true;
    } catch {
      this._error = this.#term("saveFailed");
      return false;
    }
  }

  #apply(assignments: ResponsibilityPartyModel[]) {
    this._users = selectionOf(assignments, USER_KIND);
    this._groups = selectionOf(assignments, GROUP_KIND);
    this._marks = marksFor(assignments);
  }

  #markText(mark: MarkedParty) {
    const name = mark.displayName ?? this.#term("unnamedParty");
    const condition = this.#term(
      mark.mark === "missing" ? "markMissing" : mark.mark === "disabled" ? "markDisabled" : "markInvited",
    );

    return `${name} — ${condition}`;
  }

  override render() {
    // INTENDED, both halves (QA round 1 named the tension): an untouched save keeps a
    // dangling assignment, because the picker state was seeded from the loaded set — but
    // the moment an operator CHANGES a picker, its selection replaces that state, and a
    // deleted party's key cannot be in a picker's selection, so the next save drops it
    // FROM THAT PICKER (the other picker's state is untouched and keeps its entries).
    // The write is wholesale by spec ("what is saved is what was seen"), the marks list
    // is display rather than state, and a dangling assignment sends nothing either way —
    // editing a picker is exactly the moment its stale entries should go. A JS comment,
    // not an HTML one, so it stays out of the rendered DOM and the bundle.
    //
    // The hint paragraph below is the not-permissions boundary, stated where the
    // assignment happens — the copy lives in ubookitResponsibility_hint.
    return html`
      <uui-box headline=${this.#term("headline")}>
        <p class="hint">${this.#term("hint")}</p>

        ${this._error ? html`<p class="error" role="alert">${this._error}</p>` : nothing}

        <h3 class="picker-heading">${this.#term("users")}</h3>
        <umb-user-input
          .selection=${this._users}
          @change=${(e: Event) => {
            this._users = [...(e.target as UmbUserInputElement).selection];
          }}
        ></umb-user-input>

        <h3 class="picker-heading">${this.#term("groups")}</h3>
        <umb-user-group-input
          .selection=${this._groups}
          @change=${(e: Event) => {
            this._groups = [...(e.target as UmbUserGroupInputElement).selection];
          }}
        ></umb-user-group-input>

        ${this._marks.length === 0
          ? nothing
          : html`
              <h3 class="picker-heading">${this.#term("marksHeading")}</h3>
              <ul class="marks">
                ${this._marks.map((mark) => html`<li>${this.#markText(mark)}</li>`)}
              </ul>
            `}
      </uui-box>
    `;
  }

  static override readonly styles = css`
    uui-box {
      margin-bottom: var(--uui-size-space-4);
    }

    .hint {
      margin-top: 0;
      color: var(--uui-color-text-alt);
    }

    .picker-heading {
      font-size: var(--uui-size-5);
      margin: var(--uui-size-space-4) 0 var(--uui-size-space-2);
    }

    .error {
      color: var(--uui-color-danger);
    }

    .marks {
      margin: 0;
      padding-left: var(--uui-size-space-5);
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    "ubookit-responsibility-editor": UBookItResponsibilityEditorElement;
  }
}
