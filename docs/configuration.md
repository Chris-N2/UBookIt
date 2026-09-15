# Configuration

Every uBookIt setting, where it comes from, and who is meant to decide it.

## Two sources, and which wins

A setting can be set in two places:

1. **Your site's configuration** — `appsettings.json`, `appsettings.{Environment}.json`, environment
   variables, a secret store: whatever your `IConfiguration` is built from.
2. **The backoffice settings screen**, which stores a value in uBookIt's own table.

**A stored value wins.** Where nothing is stored for a key, the configured value shows through
unchanged, and your whole configuration stack keeps working underneath — a value from an
environment variable is the configured value exactly as one from a file is.

Both sources are read by the same code, so a value that cannot be read behaves the same way
whichever place it came from: the same fallback, the same message in the log.

> **Once a setting is stored, `appsettings` no longer describes what runs.** A deployment that
> changes the file will have no effect until the stored value is removed. The settings screen shows
> the configured value beside the effective one for exactly this reason, and **Reset to configured
> value** removes the stored value so the file takes over again — it does not copy the file's value
> into the store.

A fresh install and an upgrade both start with nothing stored, so uBookIt behaves exactly as it did
before this feature existed until somebody saves something.

## Who decides what

Settings are divided by **who is competent to judge them**:

> Policy and communication belong to whoever runs the bookings.
> Cost, safety and data lifetime belong to whoever deploys the site.

That is why some settings are editable in the backoffice and some are shown there but changed only
in configuration. It is also the rule for where any future setting belongs.

### Editable in the backoffice

Requires the **`UBookIt.Settings`** permission — see [Permissions](#permissions) below.

| Key | Type | Default | Meaning |
|---|---|---|---|
| `UBookIt:AutoConfirm` | bool | `true` | `true`, bookings are confirmed as they are placed. `false`, they arrive as **requested** for somebody to confirm or decline. |
| `UBookIt:Notifications:SendBookerEmails` | bool | `false` | Whether the person who booked is emailed when their booking is placed, confirmed, declined or cancelled. |
| `UBookIt:Notifications:InternalRecipients` | string[] | empty | Your own addresses to tell about bookings. **Supplying addresses is what turns internal messages on** — there is no separate switch. |
| `UBookIt:PrivacyPolicyUrl` | string | none | Linked from the booking form's privacy notice. With none, the notice renders without a link rather than with a broken one. |
| `UBookIt:TimeZoneId` | IANA id | `UTC` | The zone the site's availability rules are written in, e.g. `Europe/London`. |

**`UBookIt:TimeZoneId` carries a consequence, and the screen states it before you change it.**
Availability rules are stored as day-and-time with no zone of their own, while bookings are stored
as absolute instants. So changing the zone changes what every existing rule *means* — a 9:00–17:00
rule becomes 9:00–17:00 in the new zone — while existing bookings keep the times they were actually
made for, and some may no longer fall inside their resource's hours. **Nothing is rewritten, and
setting it back restores what every rule meant.**

### Changed in configuration only

Shown on the settings screen so you can see what is in effect, never editable there.

| Key | Type | Default | Why it is not editable |
|---|---|---|---|
| `UBookIt:RetentionDays` | int | none | **Data lifetime.** Erasure is irreversible, and it happens on the next hourly sweep rather than when you save — so a mistyped value destroys personal data quietly, some time after the mistake. |
| `UBookIt:MaxQueryRangeDays` | int | `31` | **Cost.** A guardrail on availability queries, which walk their range day by day. Too high does not look broken; it just makes the site slower. |
| `UBookIt:DeliveryApi:EnableReads` | bool | `false` | **Exposure, and restart-bound.** Decided while the application starts — a disabled direction is *absent*, not refused. |
| `UBookIt:DeliveryApi:EnablePlacement` | bool | `false` | As above, for anonymous booking placement. |
| `UBookIt:Frontend:PreservedQueryParameters` | string[] | empty | A developer's setting about their own page's URLs. Not presented at all. |

The delivery API settings are **restart-bound**: changing them takes effect when the site restarts.
The other two take effect immediately, and are read-only for policy reasons rather than technical
ones — the screen distinguishes the two cases.

**The active theme is not a setting and is not shown.** It has no configuration key: a site calls
`AddUBookItTheme(...)` in its own composer. See [Theming](theming.md).

## What happens when a value cannot be read

The fallbacks are deliberately asymmetric, each chosen for which failure is *silent* rather than
which is convenient. They apply identically to stored and configured values.

| Key | Unreadable value resolves to | Logged at startup |
|---|---|---|
| `UBookIt:TimeZoneId` | `UTC` | warning, when absent |
| `UBookIt:MaxQueryRangeDays` | `31` (the default) | — |
| `UBookIt:RetentionDays` | **no retention** — never a default period | error, if something was written |
| `UBookIt:AutoConfirm` | `true` (on) | error, if something was written |
| `UBookIt:PrivacyPolicyUrl` | **no link** | error, if something unusable was written |

`UBookIt:RetentionDays` and `UBookIt:MaxQueryRangeDays` are both numbers and are resolved on
opposite terms on purpose: the cost of misreading a query range is a rejected query, and the cost of
misreading a retention period is irreversibly destroying personal data. The failure direction is
chosen to keep data rather than to keep the feature.

The settings screen validates what you type before storing it, so you are told rather than silently
given the fallback. The fallbacks still apply to anything that reached the database another way.

## Upgrading to this version

**If your own code registers a singleton that takes `SiteBookingSettings` in its constructor, or
resolves it from the root service provider, it will now fail at startup** with *"Cannot consume
scoped service 'SiteBookingSettings' from singleton"*. The settings are resolved per scope so that
a change made in the backoffice takes effect without a restart. Take an `IServiceScopeFactory` and
resolve the settings inside a scope per unit of work, as uBookIt's own retention job does.

Nothing in uBookIt itself is affected, and nothing else about the upgrade changes behaviour: with
no stored settings, every value resolves exactly as it did before.

## Permissions

The settings screen and its endpoints require the **`UBookIt.Settings`** verb, granted in
**Users → User Groups → Default permissions** as *"Change site settings"*.

> **It is not granted automatically, including on upgrade.** A group holding `UBookIt.Configure`
> does not get it: configuring a bookable resource and configuring the site are different
> privileges, and these settings reach your retention posture, your anonymous API exposure and the
> addresses your bookers' details are sent to. Granting it on upgrade would widen privilege
> silently.
>
> **Umbraco administrators are not exempt.** Until the box is ticked, nobody has it — the settings
> tab explains this rather than disappearing.

See [Permissions in the backoffice](backoffice.md) for the other three verbs.

## Example

```json
{
  "UBookIt": {
    "TimeZoneId": "Europe/London",
    "AutoConfirm": false,
    "RetentionDays": 365,
    "PrivacyPolicyUrl": "https://example.com/privacy",
    "Notifications": {
      "SendBookerEmails": true,
      "InternalRecipients": [ "bookings@example.com" ]
    },
    "DeliveryApi": {
      "EnableReads": true,
      "EnablePlacement": false
    }
  }
}
```

Related: [Notifications](notifications.md) · [Delivery API](delivery-api.md) ·
[Theming](theming.md) · [The booking page](booking-page.md) · [Backoffice](backoffice.md)
