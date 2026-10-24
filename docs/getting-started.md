# Getting started

This page takes you from an Umbraco site without uBookIt to a visitor's booking arriving in
an inbox, using the booking page uBookIt ships. Each step does the least that works and links the
page that covers it in full, so you can come back for the detail once the basics run.

If you are building your own front end on uBookIt's JSON API instead of using its booking page,
start with [the delivery API](delivery-api.md).

## 1. Check the requirements

| | |
|---|---|
| **Umbraco** | 18.2.1 or later, and below 19. For Umbraco 17, install uBookIt `17.x`. |
| **.NET** | 10.0 |
| **Database** | **SQL Server.** SQLite is not supported, and that includes the SQLite database a new `dotnet new umbraco` site uses by default. |

A site on SQLite fails when uBookIt installs its schema, rather than quietly misbehaving.

If your site is below 18.2.1, upgrade `Umbraco.Cms` first, so that every Umbraco package stays on
one version.

## 2. Install the package

```
dotnet add package UBookIt --version "18.*"
```

**The version is not optional.** uBookIt's major follows Umbraco's, so without it your package
manager picks the newest uBookIt, which is for a later Umbraco, and the restore fails with a
version conflict. `18.*` takes the newest uBookIt for Umbraco 18; give an exact version instead if
you prefer to choose when to move.

Start the site. uBookIt installs its schema, its **Booking Page** document type and that page's
template on the first start. Nothing is sent to anybody and nothing is published until you do the
steps below.

## 3. Give people the uBookIt section

In the backoffice, open **Users → User Groups**, choose a group, and:

1. under **Sections**, tick **uBookIt Section**;
2. under **Default permissions**, tick what the group may do: **See bookings**, **Act on
   bookings**, and **Configure resources and services**.

Save the group, then reload the backoffice. The **uBookIt** section appears.

**Who can see a booker's name and email address is a separate question.** Umbraco's built-in
**Sensitive data** group answers it, and at first that group holds only the site's original
super user. Add the people who need those details to it.

See [the backoffice section](backoffice.md#who-can-use-it) for what each permission allows, and
[who can see booker contact details](backoffice.md#who-can-see-booker-contact-details).

## 4. Create a resource and its opening hours

A **resource** is something that can be booked: a room, a desk, a piece of equipment, a person.

In the **uBookIt** section, open **Resources** and choose **Create resource**.

1. Give it a **Name**, such as *Studio 1*.
2. Give it a **Type**, such as *studio*. Services find resources by type, in step 5.
3. Tick **Can be booked on its own**, so that visitors can book it directly as well as through a
   service.
4. Under **Opening hours**, use **Add window** on each day it is open, and set **From** and **To**.
5. **Save**.

Opening hours are in the site's time zone, which is UTC until you change it. To change it, see
`UBookIt:TimeZoneId` in [configuration](configuration.md#editable-in-the-backoffice). Exceptions,
minimum notice, length limits and capabilities can all wait. See
[what is in the section](backoffice.md#what-is-in-the-section).

## 5. Create a service

A **service** is what a visitor books by name, such as *A one-hour session*. It says which kind
of resource it needs, and uBookIt finds one that is free.

In **Services**, choose **Create service**.

1. Give it a **Name**.
2. Under **Duration**, choose **Fixed length** and enter the minutes, such as `60`.
3. Under **Service Requirements**, set **Resource type** to the type you gave the resource in
   step 4. Leave **How many at once** at `1`.
4. **Save**. The editor's summary says how many resources can provide the service. It should be 1.

## 6. Create the booking page

In **Content**, create a page of the **Booking Page** type at the root of the content tree, call
it *Book*, and **Save and publish**. Visit it: it lists your service and your resource, and either
can be booked.

The page uses your site's layout through your `_ViewStart.cshtml`, and it needs nothing else from
you.

**Before you deploy, commit `Views/uBookItBookingPage.cshtml`.** uBookIt writes it into your
project when it installs. A site that precompiles its views will not pick it up on the server.
See [commit the template before you deploy](booking-page.md#commit-the-template-before-you-deploy).

## 7. Turn emails on, and check one arrives

**uBookIt sends nothing until you turn emails on**, even when your site can send mail. Add this to
`appsettings.json`:

```json
{
  "UBookIt": {
    "Notifications": {
      "SendBookerEmails": true,
      "InternalRecipients": [ "bookings@example.com" ]
    }
  }
}
```

`SendBookerEmails` writes to the person who booked. Listing addresses in `InternalRecipients` tells
your own people about every booking placed or cancelled. Both can also be changed on uBookIt's
**Settings** screen. That screen needs the **Change site settings** permission, which nobody has
until you grant it, administrators included.

uBookIt sends through your site's mail configuration, so Umbraco needs a `From` address and a way
to send. **To check that messages are being sent without a mail server**, write them to a folder:

```json
{
  "Umbraco": {
    "CMS": {
      "Global": {
        "Smtp": {
          "From": "bookings@example.com",
          "DeliveryMethod": "SpecifiedPickupDirectory",
          "PickupDirectoryLocation": "C:\\temp\\mail"
        }
      }
    }
  }
}
```

That folder is a way to check, not a way to run a site. When the check has passed, replace it with
your real SMTP settings (`Host`, `Port`, `Username`, `Password`), as for Umbraco's own emails.

Restart the site, then make a booking on your booking page with your own address. A message to you
and one to `InternalRecipients` appear in the folder, each as an `.eml` file you can open.

If nothing appears, look in the log: uBookIt says once at startup if sending is on and there is no
usable mail configuration. See [turning it on](notifications.md#turning-it-on).

## 8. Where next

- **Change what the emails say**, or give a service its own emails:
  [email templates](email-templates.md).
- **Require approval** before a booking is confirmed: `UBookIt:AutoConfirm`, in
  [configuration](configuration.md#editable-in-the-backoffice) and
  [approving bookings](backoffice.md#approving-bookings).
- **Tell the people responsible** for a resource or service, not only a fixed list:
  [responsibility](notifications.md#telling-the-people-responsible).
- **Restyle the booking flow**: [styling the booking flow](booking-page.md#styling-the-booking-flow),
  or replace its views with [a theme](theming.md).
- **Let bookers cancel for themselves**:
  [self-service cancellation](configuration.md#self-service-cancellation).
- **Close the whole organisation** for holidays: [closures](backoffice.md#closures).
- **Build your own front end**, in any framework: [the delivery API](delivery-api.md).
- **React to bookings in your own code**, to update a CRM or send an SMS:
  [the notifications](notifications.md#the-notifications).
