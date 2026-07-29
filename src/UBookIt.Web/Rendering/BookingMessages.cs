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

    public static IReadOnlyList<string> ForFailures(IEnumerable<DomainFailure> failures)
        => failures.Select(f => ForCode(f.Code)).Distinct(StringComparer.Ordinal).ToList();
}
