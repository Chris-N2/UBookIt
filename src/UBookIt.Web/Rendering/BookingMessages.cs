using UBookIt.Core.Bookings;
using UBookIt.Core.Common;

namespace UBookIt.Web.Rendering;

/// <summary>
/// Maps stable domain failure codes to user-facing messages for the default
/// front-end. Codes are the contract (see <see cref="FailureCodes"/>);
/// consumers own the wording. An unknown code falls back to a safe generic
/// message rather than leaking a raw code.
/// </summary>
public static class BookingMessages
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
    {
        [FailureCodes.Conflict] = "That time is no longer available. Please choose another.",
        [FailureCodes.OutsideOpenHours] = "That time is outside the available hours. Please choose another.",
        [FailureCodes.LeadTime] = "That time is too soon to book. Please choose a later time.",
        [FailureCodes.Horizon] = "That date is too far ahead to book.",
        [FailureCodes.Granularity] = "Please choose one of the offered times.",
        [FailureCodes.DurationTooShort] = "The booking length is too short.",
        [FailureCodes.DurationTooLong] = "The booking length is too long.",
        [FailureCodes.IntervalInvalid] = "Please choose a valid time.",
        [FailureCodes.EmailInvalid] = "Please enter a valid email address.",
        [FailureCodes.NameRequired] = "Please enter your name.",
        [FailureCodes.ResourceNotFound] = "This resource is not available for booking.",
        [FailureCodes.DateRangeInvalid] = "Please choose a valid date.",
        [FailureCodes.DateRangeTooLarge] = "Please choose a single date.",
    };

    public const string Fallback = "Sorry, your booking could not be completed. Please try again.";

    public static string ForCode(string code)
        => Map.TryGetValue(code, out var message) ? message : Fallback;

    /// <summary>
    /// Maps failures to user-facing errors, each associated with the control it
    /// belongs to so the view can link the summary and set per-field aria
    /// (WCAG 2.2 AA). Deduped by message.
    /// </summary>
    public static IReadOnlyList<BookingError> ForFailures(IEnumerable<DomainFailure> failures)
        => failures
            .Select(f => new BookingError(ForCode(f.Code), FieldIdFor(f)))
            .DistinctBy(e => e.Message, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Resolves the offending control id: the domain field where present,
    /// otherwise the time selection for placement-pipeline codes (which all
    /// concern the chosen slot). Null = a general error with no single control.
    /// </summary>
    private static string? FieldIdFor(DomainFailure failure)
        => failure.Field switch
        {
            nameof(Booker.Name) => BookingFieldIds.Name,
            nameof(Booker.Email) => BookingFieldIds.Email,
            _ => failure.Code switch
            {
                FailureCodes.Conflict
                    or FailureCodes.OutsideOpenHours
                    or FailureCodes.LeadTime
                    or FailureCodes.Horizon
                    or FailureCodes.Granularity
                    or FailureCodes.IntervalInvalid
                    or FailureCodes.DurationTooShort
                    or FailureCodes.DurationTooLong => BookingFieldIds.Times,
                _ => null,
            },
        };
}
