import { css, html, customElement, property, state, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type { CapabilityUsageModel } from "../api/index.js";

/**
 * Edits a set of capability keys: existing keys as removable chips, plus an
 * add field offering the keys already in use.
 *
 * Shared by the resource editor (what a resource can do) and the services
 * editor (what a role requires) because the two must behave identically — a
 * key added on one side only matches a key added on the other if both apply
 * the same trimming and the same duplicate handling.
 *
 * A native input with a datalist, not `uui-combobox`: the uui control forces
 * its value to match an option, which would block naming a capability before
 * any resource carries it — the same legitimate setup order that made the
 * resource type control a native input.
 *
 * Keys are NOT normalized here. The server rejects a malformed key rather than
 * lower-casing it, and quietly rewriting "Cert X" to "cert-x" in the browser
 * would store something the user did not type and hide a rule they are about
 * to meet again elsewhere.
 */
@customElement("ubookit-capability-input")
export class UBookItCapabilityInputElement extends UmbLitElement {
  /** The current keys. Owned by the host; this element never mutates it in place. */
  @property({ type: Array })
  capabilities: string[] = [];

  /** Keys already in use, offered as suggestions. */
  @property({ type: Array })
  known: CapabilityUsageModel[] = [];

  /** Id prefix, so two instances on one page keep distinct control ids. */
  @property({ type: String })
  controlId = "capabilities";

  @property({ type: String })
  label = "Capabilities";

  @property({ type: String })
  addLabel = "Add capability";

  @property({ type: String })
  hint = "";

  @property({ type: String })
  removeLabel = "Remove %0%";

  @property({ type: String })
  emptyLabel = "None";

  /**
   * The server's message for a capability failure, or empty for none.
   * <p>
   * Passed in as text and rendered *inside* this component rather than being
   * pointed at by id from the host: `aria-describedby` does not cross a shadow
   * boundary, so an id living in the host's shadow root would be a dangling
   * reference — present in the markup, resolving to nothing, and invisible to a
   * screenshot. The association only exists if both ends are in this root.
   * </p>
   */
  @property({ type: String })
  error = "";

  @state()
  private _draft = "";

  #emit(capabilities: string[]) {
    this.dispatchEvent(
      new CustomEvent("ubookit-capabilities-changed", {
        detail: { capabilities },
        bubbles: true,
        composed: true,
      }),
    );
  }

  #add() {
    const key = this._draft.trim();

    // Silently ignoring a duplicate rather than reporting one: the set already
    // contains it, so the user's intent is already satisfied and an error would
    // be about bookkeeping rather than about their configuration.
    if (key === "" || this.capabilities.includes(key)) {
      this._draft = "";
      return;
    }

    this.#emit([...this.capabilities, key]);
    this._draft = "";
  }

  #remove(key: string) {
    this.#emit(this.capabilities.filter((existing) => existing !== key));
  }

  /**
   * Enter adds the key rather than submitting the form. Inside a form a bare
   * Enter would otherwise save the whole record, discarding the half-typed
   * capability that prompted the keystroke.
   */
  #onKeydown(event: KeyboardEvent) {
    if (event.key !== "Enter") {
      return;
    }

    event.preventDefault();
    this.#add();
  }

  override render() {
    const inputId = `${this.controlId}-add`;
    const listId = `${this.controlId}-known`;
    const errorId = `${this.controlId}-error`;
    const hasError = this.error !== "";

    return html`
      <div class="field">
        <label for=${inputId}>${this.label}</label>

        ${this.capabilities.length === 0
          ? html`<p class="empty">${this.emptyLabel}</p>`
          : html`
              <ul class="chips">
                ${this.capabilities.map(
                  (key) => html`
                    <li>
                      <span class="key">${key}</span>
                      <uui-button
                        compact
                        look="secondary"
                        label=${this.removeLabel.replace("%0%", key)}
                        @click=${() => this.#remove(key)}
                      >
                        <uui-icon name="icon-trash"></uui-icon>
                      </uui-button>
                    </li>
                  `,
                )}
              </ul>
            `}

        <div class="add">
          <input
            id=${inputId}
            list=${listId}
            .value=${this._draft}
            aria-invalid=${hasError ? "true" : nothing}
            aria-describedby=${hasError ? errorId : nothing}
            @input=${(e: InputEvent) => (this._draft = (e.target as HTMLInputElement).value)}
            @keydown=${this.#onKeydown}
          />
          <datalist id=${listId}>
            ${this.known.map((k) => html`<option value=${k.key}></option>`)}
          </datalist>
          <uui-button look="secondary" label=${this.addLabel} @click=${() => this.#add()}></uui-button>
        </div>

        ${hasError ? html`<p class="group-error" id=${errorId}>${this.error}</p>` : nothing}
        ${this.hint === "" ? nothing : html`<p class="hint">${this.hint}</p>`}
      </div>
    `;
  }

  static override styles = css`
    .field {
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-1);
      margin-bottom: var(--uui-size-space-4);
      max-width: 400px;
    }
    .chips {
      display: flex;
      flex-wrap: wrap;
      gap: var(--uui-size-space-2);
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .chips li {
      align-items: center;
      background: var(--uui-color-surface-alt, #f3f3f5);
      border: 1px solid var(--uui-color-border, #d8d7d9);
      border-radius: var(--uui-border-radius, 3px);
      display: flex;
      gap: var(--uui-size-space-1);
      padding-left: var(--uui-size-space-2);
    }
    .key {
      font-family: monospace;
    }
    .add {
      display: flex;
      gap: var(--uui-size-space-2);
    }
    .add input {
      flex: 1;
    }
    .group-error {
      color: var(--uui-color-danger, #d42054);
      margin: 0;
    }
    .empty,
    .hint {
      color: var(--uui-color-text-alt, #515054);
      font-size: var(--uui-type-small-size, 0.8rem);
      margin: 0;
    }
  `;
}

export default UBookItCapabilityInputElement;

declare global {
  interface HTMLElementTagNameMap {
    "ubookit-capability-input": UBookItCapabilityInputElement;
  }
}
