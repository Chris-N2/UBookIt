using UBookIt.Tests.Rendering.Support;
using UBookIt.Web.Rendering;
using UBookIt.Web.Theming;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// The package's shared partials as a theme's optional building blocks.
/// <para>
/// Precompiled views cannot be <i>replaced</i>, but they are perfectly
/// <i>callable</i>. That is what lets a theme simply not call them, which is what
/// keeps ⑪'s rendering tests untouched by theming — and it is also the reason the
/// four paths and the models they declare become a compatibility promise rather than
/// remaining public by accident.
/// </para>
/// </summary>
public class ThemeBuildingBlockTests
{
    [Fact]
    public async Task A_theme_view_can_render_a_package_partial_by_absolute_path()
    {
        // The fixture's `complete` theme renders ~/Views/Shared/UBookIt/_YourDetails.cshtml
        // with the model that partial declares. It is loaded across an assembly
        // boundary, from a view in one precompiled assembly into a view in another.
        var renderer = new ViewRenderer(new ThemedRendering(ThemeFixture.ThemeFixture.Complete));

        var html = await renderer.RenderByNameAsync(
            "Components/Booking/Default",
            ViewFixtures.For(ViewInventory.ResourceFlow)[0].Model);

        Assert.Contains("data-theme=\"complete\"", html, StringComparison.Ordinal);

        // The partial's own markup, not merely the absence of an exception.
        Assert.Contains("class=\"ubookit-details\"", html, StringComparison.Ordinal);
        Assert.Contains("Email (required)", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_theme_view_calling_none_of_them_is_equally_valid()
    {
        // No completeness or resolution rule requires the partials to be used, and a
        // theme replacing the time picker outright is the entire point of theming.
        var renderer = new ViewRenderer(new ThemedRendering(ThemeFixture.ThemeFixture.Complete));

        var html = await renderer.RenderByNameAsync(
            "Components/BookingFlow/Catalogue",
            ViewFixtures.For(ViewInventory.Catalogue)[0].Model);

        Assert.Contains("data-theme=\"complete\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ubookit-", html, StringComparison.Ordinal);

        Assert.Empty(UBookItThemeCompleteness.Check(
            ThemeFixture.ThemeFixture.Complete, ThemedRendering.ThemeAssemblyUnderTest));
    }

    [Fact]
    public void The_promised_surface_is_published_and_is_what_the_package_actually_ships()
    {
        // Tied to the shipped files rather than restated, so a partial renamed or
        // moved fails here — which is what makes "changing one is a breaking change"
        // a check rather than a sentence.
        var shipped = ViewInventory.Shipped
            .Where(view => view.StartsWith("~/Views/Shared/UBookIt/_", StringComparison.Ordinal)
                && view != "~/Views/Shared/UBookIt/_Styles.cshtml")
            .Order(StringComparer.Ordinal);

        Assert.Equal(shipped, UBookItThemeContract.SharedPartials.Order(StringComparer.Ordinal));

        // The model half of the promise. All four declare it, and a theme passes its
        // form model as this type.
        Assert.Equal(typeof(IBookingFormView), UBookItThemeContract.SharedPartialModel);

        foreach (var partial in UBookItThemeContract.SharedPartials)
        {
            var source = UBookIt.Tests.Support.RepoFiles.Read(ViewInventory.SourcePathOf(partial));

            Assert.Contains("@model IBookingFormView", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_view_models_a_theme_receives_are_public()
    {
        // A theme is another package: a model it cannot name is a model it cannot
        // render. Public-by-promise now, so a later change is a breaking change to be
        // called out as one.
        foreach (var required in UBookItThemeContract.RequiredViews)
        {
            Assert.True(
                required.ModelType.IsPublic,
                $"{required} receives {required.ModelType.FullName}, which is not public.");
        }

        Assert.True(typeof(IBookingFormView).IsPublic);
        Assert.True(typeof(ServiceFormModel).IsPublic);
    }
}
