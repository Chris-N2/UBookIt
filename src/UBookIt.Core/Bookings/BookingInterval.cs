using UBookIt.Core.Common;

namespace UBookIt.Core.Bookings;

/// <summary>
/// A booking's continuous interval, half-open [StartUtc, EndUtc), held as UTC
/// instants plus the IANA time zone id it was placed against. Once placed, the
/// UTC instants never move, regardless of later zone-rule changes.
/// </summary>
public sealed record BookingInterval
{
    private BookingInterval(DateTimeOffset startUtc, DateTimeOffset endUtc, string timeZoneId)
    {
        StartUtc = startUtc;
        EndUtc = endUtc;
        TimeZoneId = timeZoneId;
    }

    public DateTimeOffset StartUtc { get; }

    public DateTimeOffset EndUtc { get; }

    /// <summary>IANA zone id in effect when the interval was created.</summary>
    public string TimeZoneId { get; }

    public TimeSpan Duration => EndUtc - StartUtc;

    /// <summary>Half-open overlap: touching endpoints do not overlap.</summary>
    public bool Overlaps(BookingInterval other)
        => StartUtc < other.EndUtc && other.StartUtc < EndUtc;

    public bool Overlaps(DateTimeOffset startUtc, DateTimeOffset endUtc)
        => StartUtc < endUtc && startUtc < EndUtc;

    public static DomainResult<BookingInterval> Create(
        DateTimeOffset start, DateTimeOffset end, string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return DomainResult<BookingInterval>.Failure(
                FailureCodes.TimeZoneInvalid, "A time zone id is required.");
        }

        if (end <= start)
        {
            return DomainResult<BookingInterval>.Failure(
                FailureCodes.IntervalInvalid, "The interval end must be after its start.");
        }

        return DomainResult<BookingInterval>.Success(
            new BookingInterval(start.ToUniversalTime(), end.ToUniversalTime(), timeZoneId));
    }
}
