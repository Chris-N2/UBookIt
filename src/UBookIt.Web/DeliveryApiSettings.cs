namespace UBookIt.Web;

/// <summary>
/// Which directions of the delivery API this site exposes. <b>Both are off until the
/// site turns them on, and the default instance is off</b> — a fresh install, and an
/// upgrade that changes no configuration, serves no anonymous endpoint at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>This deliberately reversed the original always-on registration.</b> An anonymous
/// public API has no way to know who is calling — Origin and Referer are written by the
/// caller, CORS restricts browsers rather than callers, and a key shipped to front-end
/// JavaScript is public — so no request-time validation can stand in for absence. The
/// package's rule is that nothing is exposed or sent until a site asks; the delivery
/// API was its largest unasked-for surface.
/// </para>
/// <para>
/// <b>The two directions are independent, and neither implies the other.</b> Reads
/// (discovery, availability, slots, the retention read) serve a site that shows
/// availability but takes bookings only through its own pages; placement alone is
/// unusual but legal. The shipped Razor front end consults neither: it renders
/// in-process from the Core ports, as <c>default-frontend</c> requires, and behaves
/// identically whatever these say.
/// </para>
/// <para>
/// Read once at startup, like every other uBookIt setting: exposure is decided when
/// the application model is built, and a value that changed while the site ran would
/// silently not apply — so it is not consulted per request, and the documentation says
/// a change needs a restart.
/// </para>
/// </remarks>
public sealed record DeliveryApiSettings
{
    /// <summary>The configuration section the composer binds this from.</summary>
    public const string SectionKey = "UBookIt:DeliveryApi";

    /// <summary>
    /// Whether the read direction is exposed: resource and service discovery,
    /// availability, slots, bookable-starts, and the retention read.
    /// </summary>
    public bool EnableReads { get; init; }

    /// <summary>
    /// Whether the placement direction is exposed: anonymous booking placement,
    /// direct and via service.
    /// </summary>
    public bool EnablePlacement { get; init; }
}
