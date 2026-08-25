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

## What an upgrade replaces, and what it never touches

This is the part worth reading before you customise anything, because it is not what
you might expect and it is **not** what the Umbraco documentation says.

Upgrading uBookIt re-imports everything the package installs. That import **overwrites
some things and never removes anything**.

### uBookIt owns these — an upgrade will replace them

| | |
|---|---|
| **The Booking Page template's contents** | Replaced with the shipped version |
| The document type's **name**, **icon**, **description** | Reset to the shipped values |
| Whether the type is **allowed at root** | Reset to the shipped value |

**Do not edit the Booking Page template.** Your changes will be replaced, silently, the
next time you upgrade. This is why that template contains nothing but a single line
handing rendering to uBookIt — there is deliberately nothing in it worth keeping.

Installing uBookIt writes that template into your project as a real file:

```
Views/uBookItBookingPage.cshtml
```

It will appear in your source tree, and it will look like an ordinary template you can
edit. It is not. Treat it the way you would a generated file: leave it alone, and put
your changes in a view override instead (below).

The case that surprises people: **an upgrade replaces the template even when the
template itself has not changed between the two versions.** The package re-imports its
whole manifest whenever any part of it changes, so a release that only adds a document
type property still rewrites the template.

Renaming the Booking Page document type, or changing its icon or description, is
reverted the same way. If you want a different name in your content tree, rename the
*page*, not the type.

### You own these — an upgrade never touches them

| | |
|---|---|
| **Your view overrides** | uBookIt installs nothing on that path |
| **Properties you add** to the Booking Page type | Nothing is ever removed |
| **Your pages** and their content | uBookIt installs a document *type*, never a document |
| Which of your page types allow a Booking Page as a child | Yours entirely |

### Changing how the booking flow looks

Override uBookIt's views. Place your own file at the same path under
`Views/Shared/UBookIt/` and yours wins — this is ordinary ASP.NET Core view
resolution.

That path is the **supported** way to customise, and it is safe by construction rather
than by care: uBookIt installs nothing there, so there is nothing for an upgrade to
overwrite. Editing the shipped template is the unsupported way, and it is the one that
loses your work.

## A note on the Umbraco documentation

Umbraco's package-migration documentation states that *"existing schema or content will
not be overwritten"* during a package migration. That holds for **content** — your pages
are safe — but not for **schema**. Templates and document type settings are overwritten,
as described above.

This was measured directly against Umbraco 17.6.2 rather than taken from either the
documentation or the source, because the two disagreed. If you check the upstream docs
and think this page is wrong, this is why it is not.
