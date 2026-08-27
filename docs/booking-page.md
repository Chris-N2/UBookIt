# The Booking Page

Installing uBookIt adds a **Booking Page** document type and a template of the same
name. Create a page of that type, publish it, and the booking flow renders on it. You
do not need to write any Razor, know the name of a view component, or configure
anything.

With nothing else specified the page shows the **catalogue** — everything the site
offers, services and directly bookable resources together. To send a visitor straight
to one thing, name it in the URL:

```
/book                      the catalogue
/book?ubBook=s:<serviceId>    that service
/book?ubBook=r:<resourceId>   that resource
```

The Booking Page is allowed at the root of the content tree so it can be created
immediately on a fresh install. You are free to allow it under your own page types
instead — that is a setting on *your* types, and uBookIt does not touch it.

The page uses **your site's layout**, via your `_ViewStart.cshtml`. uBookIt does not
set one. If your site has no `_ViewStart.cshtml`, the page renders without your site
chrome — that is Umbraco's normal behaviour, not a uBookIt setting.

### Commit the template before you deploy

Installing writes `Views/uBookItBookingPage.cshtml` into your project **at runtime**.
On a development machine it is compiled on the fly and works immediately.

Most production deployments precompile their views when publishing (`RazorCompileOnBuild`
or `RazorCompileOnPublish`), and a site running in Umbraco's `Production` runtime mode
does not have the development-mode compiler that would pick up a `.cshtml` appearing
later. So install locally first, **commit `Views/uBookItBookingPage.cshtml` to source
control**, and deploy it with the rest of your views. A booking page that renders
locally and 500s on your production server is almost always this.

## What an upgrade does, and what it never touches

uBookIt's schema is imported **once**, when you install it. An ordinary upgrade does
**not** re-import it, so your site's copy is left alone.

Installing writes the template into your project as a real file:

```
Views/uBookItBookingPage.cshtml
```

It is an ordinary template and **it is yours**. You may edit it.

### The one case where uBookIt replaces things

A uBookIt release that ships a *new migration step* — which we do only for a schema
change that existing sites need — re-imports the whole manifest. When that happens it
**overwrites** and never removes:

| | |
|---|---|
| **The Booking Page template's contents** | Replaced with the shipped version |
| The document type's **name**, **icon**, **description**, **allowed at root** | Reset to the shipped values |
| **Properties you added** to the type | Untouched — nothing is ever removed |
| **Your pages** and their content | Untouched — uBookIt installs a document *type*, never a document |

Umbraco announces it in the log when it happens:

```
Package migration executed. Summary: Conflicting templates found,
they will be overwritten: uBookItBookingPage
```

Release notes will say when a release contains a migration step. If you have made
substantial changes to the template, keep them in source control — as you would
anyway — so you can reapply them.

### Deleting the Booking Page document type is not reversible

If you delete the document type, **uBookIt will not put it back.** The import runs once
and is recorded as done; deleting the type does not change that record, so no later
release restores it. You are left with a template pointing at a type that no longer
exists.

Recovering means clearing uBookIt's migration record in the database
(`umbracoKeyValue`, key `Umbraco.Core.Upgrader.State+uBookIt`) so the install runs
again. If you do not want the Booking Page, delete the *page* and leave the type alone
— an unused document type costs nothing.

### Do not install uBookIt with `RunSchemaAndContentMigrations` turned off

Umbraco has a global switch that stops package migrations importing schema:

```json
{ "Umbraco": { "CMS": { "PackageMigration": { "RunSchemaAndContentMigrations": false } } } }
```

**If that is set when uBookIt first starts, uBookIt is permanently broken on that
site.** Umbraco skips the import but still records the migration as done, and because
uBookIt's migration runs once, it never runs again. You get no Booking Page type, no
template, and **no error** — just one INFO line in the log of a boot that may have been
months ago. Turning the setting back on does not help.

If that has already happened, recover the same way as for a deleted document type:
delete the `Umbraco.Core.Upgrader.State+uBookIt` row from `umbracoKeyValue` and restart.

If your organisation sets this flag as policy, turn it off for the boot that installs
uBookIt, then put it back.

(The setting is global rather than per-package, so it is a blunt instrument for
freezing schema in any case.)

### Changing the page around the flow

**Add your own template** and make it the document type's default. uBookIt only ever
touches templates it declares, so a template you create is never overwritten — this is
the supported way to change the page, and it survives everything.

Your template can put the booking flow wherever you want it, inside whatever markup
you like:

```cshtml
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
<div class="my-site-chrome">
    <h1>Book with My Site</h1>
    @await Component.InvokeAsync("BookingFlow")
</div>
```

Worth supplying an `<h1>` as above: uBookIt's flow starts at `<h2>`, on the assumption
that the page around it provides the heading.

To use it: create the template, allow it on the **Booking Page** document type, and
select it on your page. uBookIt only touches templates it declares, so yours is never
overwritten — including by a release carrying a migration step.

## Styling the booking flow

uBookIt ships a stylesheet, and it is **off until you ask for it**. It does layout and
spacing only and makes no colour decision at all — so the flow takes on your site's
colours and type by inheriting them, with no configuration.

### Turning it on

One line, in the `<head>` of your layout:

```cshtml
@await Html.PartialAsync("~/Views/Shared/UBookIt/_Styles.cshtml")
```

Put your own stylesheet **after** it, so your rules and tokens win.

Use the partial rather than writing the `<link>` yourself. It keeps the URL in one
place, and when a theme mechanism arrives it is the single thing a theme replaces to
turn uBookIt's CSS off — a hand-written `<link>` would survive the theme and fight it.

> **You need a layout for this.** The line goes in `<head>`, and the booking flow
> renders from a view component, which cannot reach `<head>` at all. As noted above,
> uBookIt sets no layout — your `_ViewStart.cshtml` does. If your site has none, the
> Booking Page renders with no `<head>`, so there is nowhere to put the line and the
> flow renders unstyled. That is the same condition described earlier, seen from the
> other side.

If you add nothing, the flow renders exactly as it did before: semantic HTML, no
styling, fully operable.

### What the stylesheet does, and deliberately does not do

| | |
|---|---|
| **Does** | Field layout and spacing, the start times as a wrapping run rather than a long column, text measure, a `line-height` floor, a minimum target size |
| **Does not** | Choose a single colour. Restyle your date input, selects or buttons. Set a focus style. Impose a font |

Three of those are worth the explanation, because they look like omissions and are not.

**No colour.** A colour we pick sits on a background we have never seen, so its
contrast cannot be computed and any claim about it would be unfounded. Instead,
emphasis is carried by `currentColor`, border weight and font weight — all of which
follow your text colour automatically, in light mode and dark. The one exception is
`accent-color` on the radio controls, where the browser computes the indicator's own
contrast for us.

**Native controls keep their platform appearance.** A browser's own date input, select
and button are accessible by construction, respect the visitor's preferences, and
behave correctly in Windows High Contrast. Restyling them is the most common way a
booking form loses its accessibility, so we apply a minimum target size and nothing
else.

**No focus style.** Modern browsers draw a focus ring that adapts to whatever is behind
it. Replacing it with our own would be worse on some sites and better on none.

### Design tokens

Set any of these anywhere in your CSS — `:root` is the expected place. Every one has
its default expressed at the point of use, so nothing of ours competes with yours and
you need no `!important` and no uBookIt selector.

Defaults are given exactly as the stylesheet declares them, so this table can be
compared against the file rather than trusted — a test asserts the two agree.

| Token | Default | Controls |
|---|---|---|
| `--ubookit-space` | `1rem` | Panel padding, gap between start times |
| `--ubookit-field-gap` | `0.35rem` | Label-to-control gap within a field |
| `--ubookit-section-gap` | `1.5rem` | Gap between fields and between regions |
| `--ubookit-measure` | `34rem` | Maximum width of fields and text blocks |
| `--ubookit-radius` | `0` | Corner radius of the panels |
| `--ubookit-border-width` | `3px` | Border and rule weight |
| `--ubookit-font-family` | `inherit` | Type family |
| `--ubookit-font-size` | `inherit` | Type size |
| `--ubookit-line-height` | `1.5` | Line height |
| `--ubookit-accent` | `auto` | Radio and checkbox accent |
| `--ubookit-color-error` | `currentColor` | Error text, error summary border |
| `--ubookit-color-muted` | `inherit` | Hint text — see the note below the table |
| `--ubookit-color-border` | `color-mix(in srgb, currentColor 35%, transparent)` | Panel borders, notice rules |
| `--ubookit-color-surface` | `transparent` | Panel backgrounds |

`color-mix(in srgb, currentColor 35%, transparent)` reads as "35% of whatever your text
colour is, the rest see-through" — a faint rule that follows your palette instead of
fighting it.

**Setting a colour token transfers responsibility for its contrast to you** — see the
accessibility section below. That is the whole reason the shipped defaults set no text
colour at all: `--ubookit-color-error` and `--ubookit-color-muted` resolve to your own
text colour unless you say otherwise, so nothing we ship can be less readable than what
your site already chose.

The two border tokens are the exception, and deliberately so: they default to a
translucent derivation of your text colour, because the things they draw are
decoration. See the note on 1.4.11 below.

The minimum target size is deliberately **not** a token. It is a WCAG floor, and a
floor you can lower is not a floor.

### The class vocabulary

These names are a compatibility promise: they will not be renamed without a release
note saying so.

| | |
|---|---|
| **Flows** | `ubookit-booking`, plus `ubookit-booking--service` on the service flow · `ubookit-catalogue` · `ubookit-confirmation` |
| **Forms** | `ubookit-date-form` · `ubookit-catalogue-form` |
| **Groups** | `ubookit-times` · `ubookit-details` · `ubookit-catalogue-choices` · `ubookit-booked-resources` |
| **Items** | `ubookit-times-option` · `ubookit-catalogue-choice` |
| **Fields** | `ubookit-field` on every label-and-control group · `ubookit-submit` on every submit button |
| **Messages** | `ubookit-field-error` · `ubookit-errors` (the summary) · `ubookit-hint` · `ubookit-notice` · `ubookit-no-times` · `ubookit-no-choices` · `ubookit-fixed-length` |

The naming rule, so you can predict a name rather than look it up: `ubookit-<block>`
is a block, `ubookit-<block>-<part>` is a part of one, and `ubookit-<block>--<variant>`
is a variant. A variant always appears alongside its block, so a rule written against
`.ubookit-booking` also applies to the service flow.

**Do not style the ids.** The ids in the flow are its accessibility wiring — the
targets of `aria-describedby` and of the error summary's links. They are not
appearance, and treating them as styling hooks puts screen-reader behaviour at the
mercy of a restyle.

### Accessibility: what we hold, and what becomes yours

uBookIt's flows are built to WCAG 2.2 AA, and the honest form of that claim has three
parts — because a booking flow is a component inside **your** page, and conformance is
a property of a page.

**Met by uBookIt, in the markup, whatever you do to the CSS.** Every control has a
programmatically associated label. The start times and the catalogue are grouped sets
with legends that name what is being chosen. Hints and errors are associated with their
controls. Required fields are indicated in text, never by colour or placeholder alone.
Every flow is fully operable by keyboard. And each page keeps a logical reading and
focus order **with no stylesheet applied at all** — which is why no stylesheet can make
the flow inoperable, only harder to read.

**Met by our defaults, and yours the moment you override the token.** Text contrast
(1.4.3), focus appearance (2.4.11, 2.4.13) and target size (2.5.8) are determined by
CSS. Our defaults meet them by **setting no text colour of their own** — every text
colour resolves to `currentColor` or `inherit`, so it is whatever your site already
chose — and by leaving focus styling to the browser. If you set
`--ubookit-color-error`, `--ubookit-color-muted`, `--ubookit-color-surface` or
`--ubookit-accent`, the contrast of that choice is yours to check.

> **Why muted text is not muted by default.** An earlier version derived hint text as
> 75% of your text colour, on the reasoning that deriving from your own colour could
> not clash. It cannot clash, but compositing text at 75% opacity *reduces* its
> contrast — a site with body text at `#767676`, which is exactly AA-conformant, would
> have had hints at 2.9:1. So the default now changes nothing, and
> `--ubookit-color-muted` is there for when you want recession and can pick a value
> that passes on your background.

**Non-text contrast (1.4.11): the borders are decoration, deliberately.** The rule
beside a notice and the border around the confirmation panel default to a translucent
derivation of your text colour, which is well under 3:1. That is exempt rather than
non-conformant: neither border carries any information — every message they mark is
stated in full in the text beside them — and 1.4.11 applies to meaning-bearing
graphics and to UI component boundaries you must perceive to operate the control. If
you would rather they were prominent, set `--ubookit-color-border`.

**Determined by your page, and never ours to claim.** Reflow (1.4.10), text spacing
(1.4.12), bypass blocks (2.4.1), page titled (2.4.2), language of page (3.1.1), and the
document's heading outline — the flow starts at `<h2>` on the assumption your page
supplies the `<h1>`.

### What you still cannot change

**The markup *inside* the booking flow is not customisable.** Placing your own file at
the same path as one of uBookIt's views does **not** work: those views are compiled
into `UBookIt.Web.dll` without source checksums, so ASP.NET Core uses the compiled copy
and never consults your file.

Verified on a development site. We have not established what happens on a fully
precompiled production site, where your override would also be compiled and the outcome
depends on assembly load order — so treat the flow's markup as fixed either way, and
do not build anything on an override taking effect.

We would like to fix this properly, with a theme mechanism that looks in a path the
package deliberately does not compile into itself. It is not built yet. Until it is,
your options are your own template for the page around the flow, and the tokens and
classes above for its appearance.

## A note on the Umbraco documentation

Umbraco's package-migration documentation states that *"existing schema or content will
not be overwritten"* during a package migration. That holds for **content** — your pages
are safe — but not for **schema**. Templates and document type settings are overwritten,
as described above.

This was measured directly against Umbraco 17.6.2 rather than taken from either the
documentation or the source, because the two disagreed. If you check the upstream docs
and think this page is wrong, this is why it is not.
