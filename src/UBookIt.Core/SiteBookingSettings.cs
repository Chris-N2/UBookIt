namespace UBookIt.Core;

/// <summary>
/// Site-wide booking settings. v1 constraint: one time zone per site;
/// availability rules are wall-clock in this zone.
/// </summary>
public sealed record SiteBookingSettings
{
    /// <summary>IANA time zone id (e.g. "Europe/London").</summary>
    public required string TimeZoneId { get; init; }

    /// <summary>
    /// Maximum inclusive span, in days, an availability free-time or slot
    /// query may cover. An admin guardrail: free-time computation walks the
    /// range day-by-day, so an unbounded span is a cost hole. Queries wider
    /// than this are rejected with <see cref="Common.FailureCodes.DateRangeTooLarge"/>.
    /// Defaults to 31.
    /// </summary>
    public int MaxQueryRangeDays { get; init; } = 31;
}
