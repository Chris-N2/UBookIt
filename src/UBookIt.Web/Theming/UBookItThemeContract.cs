using UBookIt.Web.Rendering;

namespace UBookIt.Web.Theming;

/// <summary>
/// One view a complete theme supplies: which view component renders it, what it is
/// called, and the view model it is handed.
/// </summary>
/// <param name="ComponentName">
/// The view component — <c>Booking</c> or <c>BookingFlow</c>.
/// </param>
/// <param name="ViewName">The view name the component asks for.</param>
/// <param name="ModelType">
/// The model the component passes. A theme's view SHALL declare this type or a type
/// it is assignable to; declaring anything else renders once and throws on the first
/// visitor's request.
/// </param>
public sealed record UBookItThemeView(string ComponentName, string ViewName, Type ModelType)
{
    /// <summary>
    /// Where this view lives in a theme called <paramref name="themeName"/> — the
    /// exact identifier the compiled view carries, so it can be compared against a
    /// <c>ViewDescriptor.RelativePath</c> without reformatting either side.
    /// </summary>
    public string PathIn(string themeName)
        => $"{UBookItThemeContract.ThemesRoot}/{themeName}/Components/{ComponentName}/{ViewName}.cshtml";

    /// <summary>Names the component and view, for a diagnostic a reader can act on.</summary>
    public override string ToString() => $"{ComponentName}/{ViewName}";
}

/// <summary>
/// What a complete uBookIt theme supplies.
/// <para>
/// Published so a theme author learns it from the package rather than by reading the
/// package's source or by observing which pages come out wrong. It is deliberately
/// data and not an interface: a theme is an alternative <i>rendering</i> of the
/// package's view models, never an implementation of a package-defined control
/// abstraction.
/// </para>
/// </summary>
public static class UBookItThemeContract
{
    /// <summary>The path every theme's views live under.</summary>
    public const string ThemesRoot = UBookItThemeViewLocationExpander.ThemesRoot;

    /// <summary>
    /// Every view a complete theme supplies, with the model each receives.
    /// <para>
    /// Ten views across the package's two public view components. Both components
    /// are entry points a site author may place, so a theme that covers one and not
    /// the other is incomplete — which is the case
    /// <see cref="UBookItThemeCompleteness"/> exists to name.
    /// </para>
    /// </summary>
    public static IReadOnlyList<UBookItThemeView> RequiredViews { get; } =
    [
        new("Booking", "Default", typeof(BookingFormModel)),
        new("Booking", "Confirmation", typeof(BookingConfirmationModel)),
        new("Booking", "Unavailable", typeof(BookingUnavailableModel)),
        new("BookingFlow", "Catalogue", typeof(CatalogueModel)),
        new("BookingFlow", "Default", typeof(BookingFormModel)),
        new("BookingFlow", "Confirmation", typeof(BookingConfirmationModel)),
        new("BookingFlow", "Service", typeof(ServiceFormModel)),
        new("BookingFlow", "ServiceConfirmation", typeof(ServiceConfirmationModel)),
        new("BookingFlow", "ServiceUnavailable", typeof(ServiceUnavailableModel)),
        new("BookingFlow", "Unavailable", typeof(BookingUnavailableModel)),
    ];

    /// <summary>
    /// The package's shared partials, which a theme MAY render as building blocks by
    /// absolute path — and is equally free to ignore.
    /// <para>
    /// <b>These paths, and the models they declare, are a compatibility promise.</b>
    /// They were already <c>public</c>; publishing them here is what turns
    /// public-by-accident into public-by-promise, so that changing one is thereafter
    /// a breaking change to be called out as one. A theme wanting the package's
    /// accessible time picker calls it; a theme replacing it does not, and no rule
    /// requires a theme to call any of them.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> SharedPartials { get; } =
    [
        "~/Views/Shared/UBookIt/_DateAndLength.cshtml",
        "~/Views/Shared/UBookIt/_ErrorSummary.cshtml",
        "~/Views/Shared/UBookIt/_PrivacyNotice.cshtml",
        "~/Views/Shared/UBookIt/_Times.cshtml",
        "~/Views/Shared/UBookIt/_YourDetails.cshtml",
    ];

    /// <summary>
    /// The model every one of <see cref="SharedPartials"/> declares. Part of the same
    /// promise: a theme passes its form model to a partial as this type.
    /// </summary>
    public static Type SharedPartialModel => typeof(IBookingFormView);

    /// <summary>The path a theme's views live under, for one theme.</summary>
    public static string RootFor(string themeName) => $"{ThemesRoot}/{themeName}";
}
