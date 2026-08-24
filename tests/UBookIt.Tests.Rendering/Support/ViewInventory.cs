using UBookIt.Tests.Support;

namespace UBookIt.Tests.Rendering.Support;

/// <summary>
/// Which views this suite renders — which is all of them.
/// <para>
/// Discovered by scanning the shipped <c>Views</c> directory rather than by a
/// hardcoded list. A list would decay: a view added later would sit outside the
/// suite while every rule reported green, which is the same shape as the defect
/// this suite exists to catch.
/// </para>
/// <para>
/// There is no longer anything to subtract. Three views were once held back
/// because the rig could not host <c>Html.BeginUmbracoForm</c>; it can now, so
/// the shipped set and the exercised set are one set.
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
    /// The service booking flow: a page in its own right, and the composition the
    /// shared partials are actually served in.
    /// </summary>
    public const string ServiceFlow = "~/Views/Shared/Components/BookingFlow/Service.cshtml";

    /// <summary>The resource booking flow — the same, for a single resource.</summary>
    public const string ResourceFlow = "~/Views/Shared/Components/Booking/Default.cshtml";

    /// <summary>
    /// The dispatcher's resource page: a one-line <c>PartialAsync</c> delegate into
    /// <see cref="ResourceFlow"/>, and so a page that renders a form too.
    /// </summary>
    public const string ResourceFlowViaDispatcher =
        "~/Views/Shared/Components/BookingFlow/Default.cshtml";

    /// <summary>
    /// Every view the package ships, as an absolute view path — and, since nothing
    /// is deferred, every view this suite exercises.
    /// <para>
    /// There is deliberately no "deferred" set and no <c>InScope</c> subtraction.
    /// Three views once sat outside the suite because they call
    /// <c>Html.BeginUmbracoForm</c> and the rig could not host it; the rig now can,
    /// so the shipped set and the exercised set are the same set, and their equality
    /// is asserted rather than assumed.
    /// </para>
    /// <para>
    /// Were a view ever to need excluding again, it must be excluded <b>by name with
    /// a reason and with what would lift it</b> — never by an undefined phrase like
    /// "in scope", which is how the previous deferral stayed invisible in the spec.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> All { get; } = Scan();

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
