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

### Changing how the booking page looks

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

### What you cannot change yet

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
your options are the template above, and CSS.

## A note on the Umbraco documentation

Umbraco's package-migration documentation states that *"existing schema or content will
not be overwritten"* during a package migration. That holds for **content** — your pages
are safe — but not for **schema**. Templates and document type settings are overwritten,
as described above.

This was measured directly against Umbraco 17.6.2 rather than taken from either the
documentation or the source, because the two disagreed. If you check the upstream docs
and think this page is wrong, this is why it is not.
