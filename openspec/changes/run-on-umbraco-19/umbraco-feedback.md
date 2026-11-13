# Notes for Umbraco: upgrading uBookIt from 18 to 19.0.0-rc1

Collected while porting uBookIt (a backoffice section with its own Management API controllers, an
anonymous delivery API with its own OpenAPI document, an EF Core persistence layer, and a Lit/Vite
client generated with hey-api). Each entry: **what happened**, **how we found it**, and **what would
have helped**. These are Chris's to edit and send; nothing here has been sent.

## Overall

Smooth, with one serious exception (the first entry). The retarget needed:
- `net11.0`;
- EF Core and `Microsoft.AspNetCore.OpenApi` 11.0.0, taken straight from RC1's nuspecs;
- one renamed method in our source;
- interface members on our test doubles.

The documented breaking changes were clear, and most of them didn't touch a package like ours.

## 1. Package-owned Management API controllers lock editors out, and nothing says so

- **What happened.** Our controllers are protected by our own authorization policies. Those
  policies named only `OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme`, which was
  correct on 17 and 18. On 19, the backoffice authenticates with the cookie alone, so every call our
  section made returned 401.
- **What an editor sees.** Not an error in our section. The backoffice treats the 401 as an
  expired session and shows **"Your session has timed out. Please sign in again"**. To an editor,
  opening the package logs them out. API users (client credentials) were unaffected, so a
  token-based smoke test passes and hides it.
- **How we found it.** We read RC1's source (the removal of `HideBackOfficeTokensHandler`, and
  `BackOfficeAuthPolicyBuilderExtensions` naming both schemes), then proved it on a running RC1
  site. In one page load, Umbraco's `user/current` returned 200 and our endpoint returned 401.
- **The fix.** One line per policy, copying Umbraco's own:
  `policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, Constants.Security.BackOfficeAuthenticationType)`.
- **What would have helped.**
  - A breaking-change entry for package authors saying that custom policies on backoffice
    controllers must also name `Constants.Security.BackOfficeAuthenticationType`, with that line of
    code.
  - Better still, a public helper (the private `AddAuthenticationSchemes`, made public), so
    packages can't drift from Umbraco's own policies.
  - The version-specific page covers the client side thoroughly ("send requests with
    `credentials: 'include'`… remove any `Authorization` header"), but says nothing about the server
    side of a package's own API.

## 2. `IEmailSender.CanSendRequiredEmail()` was removed without a note

- **What happened.** Removed, and replaced by `IsEmailConfigured()` (same logic) plus a new
  `IsEmailAvailableAsync()`. Three of our call sites, and five test doubles implementing
  `IEmailSender`, stopped compiling.
- **How we found it.** The compiler. The version-specific page doesn't mention it.
- **What would have helped.** A line in the breaking changes, ideally an `[Obsolete]` pointer on
  18's method first. The new XML docs explaining the configured/available split are good.

## 3. OpenAPI documents registered with plain `AddOpenApi` move to OpenAPI 3.2

- **What happened.** Our delivery API's document uses the framework's own `AddOpenApi`, not
  Umbraco's builder. On .NET 11 it moved from OpenAPI 3.1.1 to **3.2.0**. Umbraco's own documents
  stay on 3.1, because Umbraco pins `OpenApiSpecVersion.OpenApi3_1`.
- **How we found it.** By diffing our two documents on 18 against 19: the content was identical,
  but the version field differed.
- **What would have helped.** The page's note that ".NET 11 requires `Microsoft.OpenApi` 3.10 or
  later; custom OpenAPI transformers may need updates" could add: "documents not registered through
  Umbraco default to OpenAPI 3.2; set `OpenApiVersion` to stay on 3.1, as Umbraco does."

## 4. `IRuntimeState.CurrentMigrationVersion` was added

- **What happened.** A new interface member broke our four test stubs of `IRuntimeState`.
- **What would have helped.** A mention under package-author guidance, next to the existing
  `IPublishedElement.OwningContentId` and `IContentTypeService` entries.

## What worked well

- **"Back up the database; 18 can't open it once the 19 upgrade starts"** is prominent and
  correct. It made us notice that our 19 test site had inherited our 18 site's user-secrets id, so
  it would have migrated the 18 database. We copied it instead, and unattended upgrade took the copy
  to 19 cleanly on first boot.
- `@umbraco-cms/backoffice` 19.0.0-rc1 installed cleanly beside `@hey-api/openapi-ts` 0.99. Our
  section code needed no change: `tsc` was clean and all 391 client tests passed.
- The client-side auth guidance was accurate: `authContext.configureClient()` was all our generated
  client needed.

## Added during apply

### 5. `ReadOnlyUserGroup`'s current constructor changed shape with no obsolete warning first

- **What happened.** Our test fixtures build a user group with the constructor 18 recommended,
  the one that added `startElementId`, using named arguments. In 19 that constructor gained two
  required parameters (`startDocumentBlueprintId`, `hasAccessToInvariantForVariant`), and the
  shorter overloads were removed. Six fixtures stopped compiling.
- **The contrast.** The *shorter* overloads were marked
  `[Obsolete("… Scheduled for removal in Umbraco 19.")]` on 18, so that removal was signposted. The
  constructor everyone was told to move to was not, and it changed anyway.
- **How we found it.** The compiler, on its second pass: these errors only appear once the
  interface-member errors are fixed. They were not in our first error list.
- **What would have helped.** An overload with the 18 signature, marked obsolete in 19 and removed
  in 20. Or, at least, a breaking-change line, since `ReadOnlyUserGroup` is the natural type to
  construct when testing permission handlers. Also worth stating: what an upgraded group's
  `hasAccessToInvariantForVariant` becomes. It is backfilled from the legacy
  `AllowEditInvariantFromNonDefault` setting, which we only learned by reading the migration.
