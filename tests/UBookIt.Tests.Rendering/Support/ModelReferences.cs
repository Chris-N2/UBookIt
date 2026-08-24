using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests.Rendering.Support;

/// <summary>
/// The model members a view's source refers to.
/// <para>
/// <b>Derived, never declared.</b> A hand-maintained list fails the way the defect
/// it guards against fails: someone adds a branch, does not add it to the list, and
/// the list is silent about what it does not contain. A derived set grows when the
/// view does (design D3).
/// </para>
/// <para>
/// The derivation is itself a test asset and can fail <em>vacuously</em> — a
/// pattern that missed a reference form would report a view as referring to
/// nothing, and a view referring to nothing passes every property check trivially.
/// That is why <c>ModelPropertyTests</c> asserts both that every shipped view
/// yields at least one member, and that one known view yields exactly the set it
/// should.
/// </para>
/// </summary>
public static class ModelReferences
{
    /// <summary>
    /// Every form a Razor view can name a model member in:
    /// <c>Model.X</c>, <c>Model?.X</c>, and <c>Model!.X</c>.
    /// </summary>
    private static readonly Regex Reference = new(
        @"Model\s*[!?]?\s*\.\s*(?<member>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

    /// <summary>
    /// Views that refer to no model member of their own, with the reason. These are
    /// one-line delegates that hand the whole model to another view — there is no
    /// property for them to render, and the view they delegate to is checked in its
    /// own right.
    /// <para>
    /// Listed explicitly rather than tolerated silently: "this view refers to
    /// nothing" is exactly what a broken derivation looks like, so it has to be a
    /// decision someone made.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<string, string> DelegatingViews { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["~/Views/Shared/Components/BookingFlow/Confirmation.cshtml"] =
                "one-line delegate into Booking/Confirmation.cshtml, which is checked itself",
            ["~/Views/Shared/Components/BookingFlow/Unavailable.cshtml"] =
                "one-line delegate into Booking/Unavailable.cshtml, which is checked itself",
            ["~/Views/Shared/Components/BookingFlow/Default.cshtml"] =
                "one-line delegate into Booking/Default.cshtml, which is checked itself. "
                + "The third of this shape, and absent here only because it was deferred "
                + "out of the suite until the rig could host Html.BeginUmbracoForm — not "
                + "because anything about it differs from its two siblings.",
        };

    /// <summary>
    /// The single view a delegating view hands its whole model to, or null if its
    /// source does not have exactly that shape.
    /// <para>
    /// Used both to check an exemption's stated reason and to render the pair and
    /// compare them. Source is the right authority for "what does this file
    /// delegate to"; it is emphatically not the authority for "and adds nothing
    /// else", which is asked of the rendered output instead.
    /// </para>
    /// </summary>
    public static string? DelegationTargetOf(string viewPath)
    {
        var source = RepoFiles.Read(ViewInventory.SourcePathOf(viewPath));

        var targets = Regex
            .Matches(StripComments(source), @"PartialAsync\(""(?<target>~/[^""]+)"",\s*Model\s*\)")
            .Select(m => m.Groups["target"].Value)
            .ToList();

        return targets.Count == 1 ? targets[0] : null;
    }

    /// <summary>
    /// Every view a view renders into itself, transitively — the partials it
    /// includes, and whatever those include.
    /// </summary>
    public static IReadOnlyList<string> IncludedViewsOf(string viewPath)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { viewPath };
        var pending = new Queue<string>([viewPath]);

        while (pending.Count > 0)
        {
            var source = StripComments(RepoFiles.Read(ViewInventory.SourcePathOf(pending.Dequeue())));

            foreach (var included in Regex
                .Matches(source, @"PartialAsync\(""(?<target>~/[^""]+)""")
                .Select(m => m.Groups["target"].Value))
            {
                if (seen.Add(included))
                {
                    found.Add(included);
                    pending.Enqueue(included);
                }
            }
        }

        return found;
    }

    /// <summary>
    /// The members a view refers to that a view it renders <b>also</b> refers to.
    /// <para>
    /// These are the members rule 2 cannot judge for that view: when the page's own
    /// reference to one dies, the partial keeps it changing the output, and the
    /// rule sees a live member. Enumerated by
    /// <c>The_members_a_partial_keeps_alive_are_enumerated</c> rather than left to
    /// be discovered.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> MaskedByIncludesIn(string viewPath)
    {
        var fromIncludes = IncludedViewsOf(viewPath)
            .SelectMany(Of)
            .ToHashSet(StringComparer.Ordinal);

        return [.. Of(viewPath).Where(fromIncludes.Contains).Order(StringComparer.Ordinal)];
    }

    /// <summary>The model members one view's source refers to, in name order.</summary>
    public static IReadOnlyList<string> Of(string viewPath)
    {
        var source = RepoFiles.Read(ViewInventory.SourcePathOf(viewPath));

        return
        [
            .. Reference
                .Matches(StripComments(source))
                .Select(m => m.Groups["member"].Value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Razor comments removed before matching. A member named only inside
    /// <c>@* … *@</c> is discussed rather than rendered, and requiring it to change
    /// the output would fail on a view whose comment explains why it does not.
    /// </summary>
    private static string StripComments(string source)
        => Regex.Replace(source, @"@\*.*?\*@", string.Empty, RegexOptions.Singleline);
}
