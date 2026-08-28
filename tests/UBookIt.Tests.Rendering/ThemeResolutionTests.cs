using Microsoft.AspNetCore.Mvc.Razor;
using UBookIt.Tests.Rendering.Support;
using UBookIt.Web.Theming;
using Xunit.Sdk;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// The resolution guard: with a theme registered, the view that resolves is the
/// theme's — in every order a site could write.
///
/// <para>
/// <b>What this asserts, and what it deliberately does not.</b> It does not assert that an
/// expander is registered, or where from, or by what mechanism. A guard of that kind passes
/// on precisely the broken configurations this exists to catch — and two of them have now
/// been real, not hypothetical. So the guard calls the package's own entry point, runs the
/// whole chain through the same <c>FindView</c> call view-component resolution makes, and
/// asserts what comes out.
/// </para>
///
/// <para>
/// <b>Call order is swept rather than assumed.</b> The first version of this suite fixed
/// the order to "package after host" and documented that the package guaranteed it. The
/// documented reasoning was false — composers run inside <c>AddComposers()</c>, not at
/// <c>Build()</c> — and this suite could not have noticed, because it never exercised the
/// entry point that had the problem. Order is now a dimension, and both supported orders
/// must win.
/// </para>
///
/// <para>
/// <b>The measured configurations, recorded here because the documentation is only allowed
/// to state what was measured.</b>
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Without runtime compilation.</b> This project references <c>UBookIt.Web</c> and
/// deliberately not <c>UBookIt.TestSite</c>, so
/// <c>Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation</c> is absent from its dependency
/// closure — asserted structurally by
/// <see cref="ThemeFixtureTests.Runtime_compilation_is_absent_from_this_suite"/> rather than
/// believed. This is the configuration a fully precompiled production site runs, and the one
/// the spike could not reach: it ran on a development site, which carries runtime
/// compilation via <c>Umbraco.Cms.DevelopmentMode.Backoffice</c>.
/// </item>
/// <item>
/// <b>Two precompiled application parts.</b> The package's views are compiled into
/// <c>UBookIt.Web.dll</c>; the theme's into <c>UBookIt.Tests.ThemeFixture.dll</c>. Nothing
/// is read from disk — the rig's content root is a <c>NullFileProvider</c>.
/// </item>
/// <item>
/// <b>The framework's own expander chain</b>, with Umbraco's real setups registered the way
/// <c>AddWebsite()</c> registers them, plus MVC's defaults, plus the package's own
/// <c>AddUBookItTheme</c>.
/// </item>
/// </list>
/// <para>
/// Under Umbraco page rendering on a development site the same result was measured during
/// the spike (2026-08-28), on both view components. Neither measurement is generalised past
/// its configuration.
/// </para>
/// </summary>
public class ThemeResolutionTests
{
    public static TheoryData<ThemeRegistrationOrder> SupportedOrders()
    {
        var data = new TheoryData<ThemeRegistrationOrder>();

        foreach (var order in ThemedRendering.SupportedOrders)
        {
            data.Add(order);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SupportedOrders))]
    public void The_theme_view_is_what_resolves_for_every_view_the_theme_supplies(
        ThemeRegistrationOrder order)
    {
        // Every required view, not one. A chain correct for one component and wrong for
        // another would pass a single-view check — and the two components resolve through
        // the same formats, so a wrong format is exactly the fault that looks fine on
        // whichever view was written first.
        AssertTheThemeWins(order);
    }

    [Fact]
    public void Where_the_site_calls_AddUBookItTheme_does_not_change_what_resolves()
    {
        // Stated as its own guarantee rather than left implicit in the theory above,
        // because it is the claim the documentation makes to a site author and it is the
        // claim that was previously false.
        var resolved = new List<string>();

        foreach (var order in ThemedRendering.SupportedOrders)
        {
            var result = new ViewRenderer(
                    new ThemedRendering(ThemeFixture.ThemeFixture.Complete, order))
                .ResolveByName("Components/Booking/Default");

            Assert.True(result.Success, $"Nothing resolved at all with order {order}.");
            resolved.Add(result.View.Path);
        }

        // Before collapsing: the sweep must actually have swept. `Assert.Single` on a
        // distinct set passes vacuously if SupportedOrders ever shrinks to one entry, so
        // the count is tied to the orders rather than to a literal.
        Assert.Equal(ThemedRendering.SupportedOrders.Count, resolved.Count);
        Assert.True(resolved.Count > 1, "There is no sweep left: only one call order is covered.");

        resolved = [.. resolved.Distinct(StringComparer.Ordinal)];

        var single = Assert.Single(resolved);

        Assert.Equal(
            "/Views/Shared/UBookIt/Themes/complete/Components/Booking/Default.cshtml",
            single);
    }

    [Fact]
    public void The_guard_fails_on_the_registration_mechanism_this_change_rejected()
    {
        // The mutation, and it is a real failure rather than an invented one: a plain
        // Configure ahead of the host's setups is what the composer version produced, and
        // its symptom was a site that rendered correctly and was simply not themed. A guard
        // that cannot see this is not a guard.
        var failure = Assert.ThrowsAny<XunitException>(
            () => AssertTheThemeWins(ThemeRegistrationOrder.RejectedConfigureBeforeHost));

        Assert.Contains("Components/", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_rejected_mechanism_resolves_the_packages_own_view_and_says_nothing_about_it()
    {
        // The other half of the same mutation, stated positively: the broken configuration
        // is not an error, a warning or a missing view. It resolves — to the package's
        // view. That is why the guard has to be about what resolves.
        var renderer = new ViewRenderer(
            new ThemedRendering(
                ThemeFixture.ThemeFixture.Complete,
                ThemeRegistrationOrder.RejectedConfigureBeforeHost));

        var result = renderer.ResolveByName("Components/Booking/Default");

        Assert.True(result.Success);
        Assert.Equal("/Views/Shared/Components/Booking/Default.cshtml", result.View.Path);
        Assert.Equal(ViewRenderer.ViewAssembly, AssemblyOf(result.View));
    }

    [Fact]
    public async Task A_themed_view_renders_its_own_markup_in_place_of_the_packages()
    {
        var renderer = new ViewRenderer(new ThemedRendering(ThemeFixture.ThemeFixture.Complete));

        var html = await renderer.RenderByNameAsync(
            "Components/BookingFlow/Catalogue",
            ViewFixtures.For(ViewInventory.Catalogue)[0].Model);

        Assert.Contains("data-theme=\"complete\"", html, StringComparison.Ordinal);

        // And the package's own markup for that view is not there: this is a replacement,
        // not an addition.
        Assert.DoesNotContain("ubookit-catalogue", html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_site_that_registers_no_theme_resolves_the_packages_own_views()
    {
        // The no-theme case, asserted rather than assumed. Everything else in this suite
        // renders by absolute path, which never consults the expander chain, so nothing
        // else would notice an expander that had started applying itself unconditionally.
        var renderer = new ViewRenderer();

        foreach (var required in UBookItThemeContract.RequiredViews)
        {
            var result = renderer.ResolveByName($"Components/{required}");

            Assert.True(result.Success, $"{required} did not resolve at all.");
            Assert.Equal($"/Views/Shared/Components/{required}.cshtml", result.View.Path);
        }
    }

    /// <summary>
    /// The guard itself, as one callable body so the mutation check can run it on the
    /// rejected mechanism and assert it fails.
    /// </summary>
    private static void AssertTheThemeWins(ThemeRegistrationOrder order)
    {
        var theme = new ThemedRendering(ThemeFixture.ThemeFixture.Complete, order);
        var renderer = new ViewRenderer(theme);

        foreach (var required in UBookItThemeContract.RequiredViews)
        {
            // Resolved the way a view component resolves: by qualified name, through the
            // whole chain. Not by absolute path, which would bypass the very mechanism
            // under test.
            var result = renderer.ResolveByName($"Components/{required}");

            Assert.True(
                result.Success,
                $"Components/{required} did not resolve at all, with order {order}. Searched: "
                + string.Join(", ", result.SearchedLocations ?? []));

            Assert.Equal(required.PathIn(theme.ThemeName), result.View.Path);

            // The path alone would be satisfied by a file the package itself shipped at the
            // theme path. The assembly is what makes this a statement about a second,
            // separately compiled package winning.
            Assert.Equal(theme.ThemeAssembly, AssemblyOf(result.View));
        }
    }

    /// <summary>The assembly the resolved view was compiled into.</summary>
    private static System.Reflection.Assembly AssemblyOf(Microsoft.AspNetCore.Mvc.ViewEngines.IView view)
        => ((RazorView)view).RazorPage.GetType().Assembly;
}
