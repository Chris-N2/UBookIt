namespace UBookIt.Core;

/// <summary>
/// Site-wide booking settings. v1 constraint: one time zone per site;
/// availability rules are wall-clock in this zone.
/// </summary>
public sealed record SiteBookingSettings
{
    /// <summary>IANA time zone id (e.g. "Europe/London").</summary>
    public required string TimeZoneId { get; init; }
}
