# Writing a uBookIt theme

A **theme** replaces uBookIt's rendering with your own. Not a restyle — a restyle is
[the stylesheet and its tokens](booking-page.md#styling-the-booking-flow), and it is
the cheaper answer to most questions. A theme is for when you want *different
controls*: a drag-select calendar instead of a list of start times, a card grid instead
of a catalogue, a vendor scheduler instead of anything uBookIt would ever ship.

There is **one theme per site**, chosen at startup.

## What a theme is

An ordinary Razor class library. Its views are precompiled into its own assembly and
found through the standard application-part mechanism, so a theme is versionable,
testable and distributable as a NuGet package like anything else. Nothing is installed
into the consuming site — no files are written, and there is nothing for a site owner
to edit or accidentally delete.

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="UBookIt.Web" />
  </ItemGroup>
</Project>
```

## Where the views go

```
Views/Shared/UBookIt/Themes/<your-theme-name>/Components/<Component>/<View>.cshtml
```

`<your-theme-name>` is the name the site registers. It is in the path so that two
themes present in one application cannot collide.

## What a complete theme supplies

Ten views, across uBookIt's two public view components. Each is handed the same
strongly-typed view model uBookIt's own view for that state receives — a theme is an
alternative *rendering* of uBookIt's data, never an implementation of a uBookIt-defined
control abstraction, and there is no widget interface to implement.

| View | Model |
|---|---|
| `Components/Booking/Default.cshtml` | `BookingFormModel` |
| `Components/Booking/Confirmation.cshtml` | `BookingConfirmationModel` |
| `Components/Booking/Unavailable.cshtml` | `BookingUnavailableModel` |
| `Components/BookingFlow/Catalogue.cshtml` | `CatalogueModel` |
| `Components/BookingFlow/Default.cshtml` | `BookingFormModel` |
| `Components/BookingFlow/Confirmation.cshtml` | `BookingConfirmationModel` |
| `Components/BookingFlow/Service.cshtml` | `ServiceFormModel` |
| `Components/BookingFlow/ServiceConfirmation.cshtml` | `ServiceConfirmationModel` |
| `Components/BookingFlow/ServiceUnavailable.cshtml` | `ServiceUnavailableModel` |
| `Components/BookingFlow/Unavailable.cshtml` | `BookingUnavailableModel` |

All types are in `UBookIt.Web.Rendering`. The authoritative list is
`UBookItThemeContract.RequiredViews` — read it from code rather than copying this
table, and the completeness check below reads the same list.

You may declare a base type or an interface the model implements — `@model
IBookingFormView` for a view handed a `BookingFormModel` is correct and renders.

## Check your theme is complete, in your own build

```csharp
[Fact]
public void The_theme_is_complete()
    => Assert.Empty(
        UBookItThemeCompleteness.Check("your-theme-name", typeof(AnyTypeInYourTheme).Assembly));
```

**Write this test.** uBookIt falls back per view, which is ASP.NET Core's default
behaviour and is entirely silent: a theme supplying `Booking/Default` and not
`Booking/Unavailable` renders a mixture of two designs, with nothing anywhere reporting
a problem. A site running an incomplete theme gets one error in its log at boot naming
each missing view, and stays up — but a log line is a poor substitute for a failing
build, which is why this check is public API.

The check verifies **declared models as well as presence**. A view at the right path
declaring the wrong `@model` passes a name-only check and then throws when a visitor
renders it, which is worse than the failure it was meant to prevent.

## Registering the theme

In the consuming site's `Program.cs`:

```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .AddUBookItTheme("your-theme-name")
    .Build();
```

Anywhere before `.Build()` works, and the order relative to `AddWebsite()` and
`AddComposers()` genuinely does not matter. That is not a promise resting on where
uBookIt happens to register: the view location is added as an
`IPostConfigureOptions<RazorViewEngineOptions>`, and ASP.NET Core runs every
`IConfigureOptions<T>` before any post-configure regardless of registration order.
Umbraco's own view-location setups are `IConfigureOptions`, so uBookIt is always
behind them. Both call orders are covered by tests that assert what actually resolves.

Do **not** register a view-location expander yourself. uBookIt's has to end up after
Umbraco's, and getting that wrong produces a site that renders correctly and is simply
not themed — no error, no warning, no missing view.

As a backstop, uBookIt checks at boot that its theme location really is the first one
the view engine will search, and logs an error naming what won instead if it is not.
So if a theme is registered and the site comes out unthemed, the log will say so
rather than leaving you to spot it.

The theme is **fixed at startup**. Changing it while the application is running leaves
a populated view-location cache keyed without it.

## Building blocks you may reuse

A theme is free to call uBookIt's shared partials by path, and equally free to call
none of them. These paths, and the model they declare, are a compatibility promise:
changing one is a breaking change and will be called out as such in a release note.

| Partial | Model |
|---|---|
| `~/Views/Shared/UBookIt/_AvailableDates.cshtml` | `IBookingFormView` |
| `~/Views/Shared/UBookIt/_DateAndLength.cshtml` | `IBookingFormView` |
| `~/Views/Shared/UBookIt/_ErrorSummary.cshtml` | `IBookingFormView` |
| `~/Views/Shared/UBookIt/_PrivacyNotice.cshtml` | `IBookingFormView` |
| `~/Views/Shared/UBookIt/_Times.cshtml` | `IBookingFormView` |
| `~/Views/Shared/UBookIt/_YourDetails.cshtml` | `IBookingFormView` |

```cshtml
@model BookingFormModel
<div class="my-theme-panel">
    @await Html.PartialAsync("~/Views/Shared/UBookIt/_YourDetails.cshtml", Model)
</div>
```

These partials are **not individually replaceable**, and that is deliberate: a design
that protected `_Times` and `_DateAndLength` would let a theme change headings and
nothing else, which is the opposite of a theme's purpose. A theme replaces a view by
supplying its own and not calling them.

## The stylesheet

When a theme is active, uBookIt's stylesheet partial emits **nothing**. Your theme owns
the markup, so uBookIt's classes largely do not exist in the rendered document and its
CSS would style nothing while still being able to collide with yours.

If your theme reuses the shared partials above, ask for it back:

```csharp
.AddUBookItTheme("your-theme-name", usePackageStylesheet: true)
```

That emits exactly what an unthemed site emits. The site's own layout keeps its one
line — `@await Html.PartialAsync("~/Views/Shared/UBookIt/_Styles.cshtml")` — and does
not change when a theme is added or removed.

## Accessibility is yours

uBookIt's markup guarantees — every WCAG 2.2 AA criterion determined by markup,
programmatically associated labels, grouped choices, keyboard operability, reading and
focus order, and usability with no author stylesheet at all — are guarantees about
**the views uBookIt ships**. Where your theme supplies the view, the markup is yours
and so are they.

uBookIt makes **no claim about a theme in either direction**. It does not assert that
your theme is accessible, and it does not require anything of it. If you want a bar to
build to, uBookIt's own views are the worked example, and the shared partials above
carry a good deal of that work already.

For every view your theme does not supply, uBookIt's own view renders and every one of
its guarantees holds unchanged.

### One omission worth singling out: the privacy notice

`_YourDetails` renders a short notice telling the visitor what happens to the contact
details it is collecting — what is taken, why, how long it is kept, and who can see it.
The retention sentence is read from the site's configured retention period, so it cannot
say something the code does not do.

**If your theme supplies its own view for the step that collects contact details, whether
that notice appears is your decision.** uBookIt hands your view the facts on
`IBookingFormView.PrivacyNotice` — the retention period, or its absence, and the site's
privacy policy link — so you can render them in your own markup and your own wording.
It cannot make you, and it does not try.

This is the same rule as every other markup guarantee on this page, and it is called out
separately for one reason: **the failure is invisible.** A theme that drops the time
picker produces a site that visibly does not work, and somebody reports it within the
hour. A theme that drops the privacy notice produces a site that works perfectly and
tells its visitors nothing about their personal data — and nobody reports that at all.

If your theme replaces `_YourDetails`, either call `_PrivacyNotice` from it or render the
same four facts yourself.

## What has been measured

A theme's views winning over uBookIt's own has been measured in two configurations:
under real Umbraco page rendering on a development site, and in a test host with
`Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation` absent from the dependency
closure — which is the configuration a fully precompiled production site runs.

Nothing beyond those two is claimed.
