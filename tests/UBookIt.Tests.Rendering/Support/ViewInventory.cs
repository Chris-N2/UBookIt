using UBookIt.Tests.Support;

namespace UBookIt.Tests.Rendering.Support;

/// <summary>
/// Which views this suite renders, and which it cannot yet.
/// <para>
/// Discovered by scanning the shipped <c>Views</c> directory and subtracting the
/// deferred set, rather than by a hardcoded list. A list would decay: a view added
/// later would sit outside the suite while every rule reported green, which is the
/// same shape as the defect this suite exists to catch.
/// </para>
/// </summary>
public static class ViewInventory
{
    private const string ViewsRoot = "src/UBookIt.Web/Views";

    public const string ServiceConfirmation =
        "~/Views/Shared/Components/BookingFlow/ServiceConfirmation.cshtml";

    public const string ServiceUnavailable =
        "~/Views/Shared/Components/BookingFlow/ServiceUnavailable.cshtml";

    public const string DateAndLength = "~/Views/Shared/UBookIt/_DateAndLength.cshtml";

    public const string Times = "~/Views/Shared/UBookIt/_Times.cshtml";

    public const string ErrorSummary = "~/Views/Shared/UBookIt/_ErrorSummary.cshtml";

    public const string YourDetails = "~/Views/Shared/UBookIt/_YourDetails.cshtml";

    public const string Catalogue = "~/Views/Shared/Components/BookingFlow/Catalogue.cshtml";

    /// <summary>
    /// The three views this suite cannot render yet, and why.
    /// <para>
    /// The partition is by <b>transitive</b> dependency on Umbraco, not by which
    /// file mentions it. <c>BookingFlow/Default.cshtml</c> reads as Umbraco-free
    /// and is not: it is a one-line <c>PartialAsync</c> into
    /// <c>Booking/Default.cshtml</c>, which calls <c>Html.BeginUmbracoForm</c>. A
    /// list by name alone would not say that, so
    /// <c>ViewInventoryTests</c> asserts the reason rather than the names.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> Deferred { get; } =
    [
        "~/Views/Shared/Components/Booking/Default.cshtml",
        "~/Views/Shared/Components/BookingFlow/Default.cshtml",
        "~/Views/Shared/Components/BookingFlow/Service.cshtml",
    ];

    /// <summary>Every view the package ships, as an absolute view path.</summary>
    public static IReadOnlyList<string> All { get; } = Scan();

    /// <summary>The views this suite renders: everything shipped, less the deferred three.</summary>
    public static IReadOnlyList<string> InScope { get; } =
        [.. All.Where(path => !Deferred.Contains(path))];

    /// <summary>The repository path of a view, from its absolute view path.</summary>
    public static string SourcePathOf(string viewPath)
        => "src/UBookIt.Web/" + viewPath.TrimStart('~', '/');

    private static IReadOnlyList<string> Scan()
    {
        var root = Path.Combine(RepoFiles.Root, ViewsRoot.Replace('/', Path.DirectorySeparatorChar));

        return
        [
            .. Directory
                .EnumerateFiles(root, "*.cshtml", SearchOption.AllDirectories)
                .Select(file => "~/Views/"
                    + Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'))
                .Order(StringComparer.Ordinal),
        ];
    }
}
