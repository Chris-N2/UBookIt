namespace UBookIt.Web.Rendering;

/// <summary>
/// What the cancellation page shows: which booking, and nothing about who made it.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is deliberately no member for the booker's name, email address or telephone number,
/// and that absence is a guarantee rather than an omission.</b> The page is reached with a secret
/// and nothing else — no session, no account — so a view that could name the booker would be one
/// forwarded email away from disclosing them. Making it structural means the guarantee holds
/// however the view is written, including by a theme the package has never seen.
/// </para>
/// <para>
/// <b>What it does carry is the description the package already trusts to a reader who may not see
/// contact details</b> — the reference, the interval and what was booked, exactly as a message to a
/// site's own recipients carries them. Refusing to name the booking at all would be the worse
/// failure: the secret is already sufficient to destroy it, so withholding a description of what is
/// about to be destroyed protects nothing and costs the reader the one fact they need — which
/// booking this is.
/// </para>
/// </remarks>
public sealed record CancellationPageModel
{
    /// <summary>The booking's reference, in the form a person is expected to quote.</summary>
    public required string Reference { get; init; }

    /// <summary>The start, already converted to the zone the booking was placed against.</summary>
    public required DateTimeOffset LocalStart { get; init; }

    /// <summary>The end, in the same zone.</summary>
    public required DateTimeOffset LocalEnd { get; init; }

    /// <summary>That zone's id, so a view can state it alongside the times.</summary>
    public required string TimeZoneId { get; init; }

    /// <summary>The service's recorded name, or <c>null</c> for a booking placed directly.</summary>
    public string? ServiceName { get; init; }

    /// <summary>The resources claimed, by name — a collection, never a joined string.</summary>
    public IReadOnlyList<string> ResourceNames { get; init; } = [];

    // THERE IS DELIBERATELY NO MEMBER FOR THE SECRET. The form posts to the URL the page was
    // fetched from, so the credential never needs to enter the document — and a model that
    // carried it would be one interpolation away from rendering it.
}
