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

    public const string PrivacyNotice = "~/Views/Shared/UBookIt/_PrivacyNotice.cshtml";

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
    /// <summary>
    /// Every <c>.cshtml</c> the package ships, including any this suite's rendering
    /// rules do not govern. Kept separate from <see cref="All"/> so the scan's decay
    /// guard still counts everything: a view added later fails the count whichever
    /// set it belongs in.
    /// </summary>
    public static IReadOnlyList<string> Shipped { get; } = Scan();

    /// <summary>
    /// The one shipped view deliberately outside the rendering rules — named, with a
    /// reason and with what would lift it, exactly as this class's own guidance
    /// demands and never by an undefined phrase.
    /// <para>
    /// <c>_Styles.cshtml</c> emits a single <c>link</c> element into the document
    /// head. It has no model, renders no control, carries no id or class, and
    /// contributes nothing to the document body — so every rule in this suite is
    /// about a property it cannot have. Forcing it in would mean special-casing it
    /// inside each rule, which weakens the rules for the one view they were never
    /// about.
    /// </para>
    /// <para>
    /// <b>Re-decided when theming shipped, against the lifting condition below rather
    /// than by inertia.</b> The partial now emits either that one <c>link</c> or
    /// nothing at all, depending on whether a theme is active and whether it asked for
    /// the package's stylesheet. Emitting less is still nothing a visitor can perceive
    /// in the body, so the grounds are unchanged and it stays here. Its themed
    /// behaviour is covered by the emission rule, which now asserts both states.
    /// </para>
    /// <para>
    /// It is <b>not untested</b>: it is covered by the emission rule instead, which
    /// asserts that exactly one view emits package styling, that it is this one, and
    /// that it names the shipped asset. That is a stronger check on this file than
    /// any markup rule would be.
    /// </para>
    /// <para>
    /// <b>What would lift it:</b> the moment this partial emits anything a visitor
    /// can perceive in the body — an inline <c>style</c> block, a <c>noscript</c>
    /// fallback, any element with an id or class — it belongs back in
    /// <see cref="All"/> and the rules apply to it.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> NotRendered { get; } =
        ["~/Views/Shared/UBookIt/_Styles.cshtml"];

    /// <summary>
    /// Declared after <see cref="NotRendered"/> deliberately: static initialisers run
    /// in textual order, so referencing the exclusion list from above it would read a
    /// null and every rule would then run over a set built from nothing.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
        [.. Shipped.Where(view => !NotRendered.Contains(view, StringComparer.Ordinal))];

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
