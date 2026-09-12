import { UMB_CURRENT_USER_CONTEXT } from "@umbraco-cms/backoffice/current-user";
import type {
  UmbConditionConfigBase,
  UmbConditionControllerArguments,
  UmbExtensionCondition,
} from "@umbraco-cms/backoffice/extension-api";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbConditionBase } from "@umbraco-cms/backoffice/extension-registry";
import { hasAnyVerb } from "./permission-verbs.js";

export const UBOOKIT_VERB_CONDITION_ALIAS = "UBookIt.Condition.Verb";

export type UBookItVerbConditionConfig = UmbConditionConfigBase<typeof UBOOKIT_VERB_CONDITION_ALIAS> & {
  /**
   * The verbs, ANY of which permits — the same any-of shape the server's verb
   * requirement uses, so the read views pass [Read, Manage] and the implication
   * stays a rule rather than data.
   */
  oneOf: Array<string>;
};

declare global {
  interface UmbExtensionConditionConfigMap {
    UBookItVerbConditionConfig: UBookItVerbConditionConfig;
  }
}

const ObserveSymbol = Symbol();

/**
 * Permits an extension when the current user's fallback permissions contain any of the
 * configured verbs. Convenience only: everything this hides is refused by the server's
 * own policies when called directly — the client is never the control.
 */
export class UBookItVerbCondition
  extends UmbConditionBase<UBookItVerbConditionConfig>
  implements UmbExtensionCondition
{
  constructor(host: UmbControllerHost, args: UmbConditionControllerArguments<UBookItVerbConditionConfig>) {
    super(host, args);

    this.consumeContext(UMB_CURRENT_USER_CONTEXT, (context) => {
      this.observe(
        context?.currentUser,
        (currentUser) => {
          this.permitted = hasAnyVerb(currentUser?.fallbackPermissions, ...(this.config.oneOf ?? []));
        },
        ObserveSymbol,
      );
    });
  }
}

export { UBookItVerbCondition as api };
