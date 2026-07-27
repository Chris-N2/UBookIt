using UBookIt.Core.Common;

namespace UBookIt.Core.Availability;

/// <summary>
/// Per-resource booking constraints. Defaults per the availability spec:
/// 15 min granularity, 30 min–8 h duration, zero lead time, 90-day horizon.
/// </summary>
public sealed record BookingConstraints
{
    private BookingConstraints(
        TimeSpan granularity, TimeSpan minDuration, TimeSpan maxDuration, TimeSpan leadTime, int horizonDays)
    {
        Granularity = granularity;
        MinDuration = minDuration;
        MaxDuration = maxDuration;
        LeadTime = leadTime;
        HorizonDays = horizonDays;
    }

    public TimeSpan Granularity { get; }

    public TimeSpan MinDuration { get; }

    public TimeSpan MaxDuration { get; }

    public TimeSpan LeadTime { get; }

    public int HorizonDays { get; }

    public static BookingConstraints Default { get; } = new(
        granularity: TimeSpan.FromMinutes(15),
        minDuration: TimeSpan.FromMinutes(30),
        maxDuration: TimeSpan.FromHours(8),
        leadTime: TimeSpan.Zero,
        horizonDays: 90);

    public static DomainResult<BookingConstraints> Create(
        TimeSpan? granularity = null,
        TimeSpan? minDuration = null,
        TimeSpan? maxDuration = null,
        TimeSpan? leadTime = null,
        int? horizonDays = null)
    {
        var g = granularity ?? Default.Granularity;
        var min = minDuration ?? Default.MinDuration;
        var max = maxDuration ?? Default.MaxDuration;
        var lead = leadTime ?? Default.LeadTime;
        var horizon = horizonDays ?? Default.HorizonDays;

        if (g <= TimeSpan.Zero)
        {
            return Incoherent("Granularity must be positive.");
        }

        if (min <= TimeSpan.Zero || max <= TimeSpan.Zero)
        {
            return Incoherent("Durations must be positive.");
        }

        if (min.Ticks % g.Ticks != 0 || max.Ticks % g.Ticks != 0)
        {
            return Incoherent("Minimum and maximum durations must be multiples of the granularity.");
        }

        if (min > max)
        {
            return Incoherent("Minimum duration must not exceed maximum duration.");
        }

        if (lead < TimeSpan.Zero)
        {
            return Incoherent("Lead time must not be negative.");
        }

        if (horizon <= 0)
        {
            return Incoherent("Booking horizon must be positive.");
        }

        return DomainResult<BookingConstraints>.Success(new BookingConstraints(g, min, max, lead, horizon));

        static DomainResult<BookingConstraints> Incoherent(string message)
            => DomainResult<BookingConstraints>.Failure(FailureCodes.ConstraintsIncoherent, message);
    }
}
