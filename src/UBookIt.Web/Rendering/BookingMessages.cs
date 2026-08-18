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
        [FailureCodes.DurationTooShort] = "That booking length is too short for this resource. Please choose another length.",
        [FailureCodes.DurationTooLong] = "That booking length is too long for this resource. Please choose another length.",
        [FailureCodes.IntervalInvalid] = "Please choose a valid time.",
        [FailureCodes.EmailInvalid] = "Please enter a valid email address.",
        [FailureCodes.NameRequired] = "Please enter your name.",
        [FailureCodes.ResourceNotFound] = "This resource is not available for booking.",

        // Reachable even though the form is never rendered for such a resource: a
        // visitor holding a page from before the permission was withdrawn can
        // still submit it. The fallback ("please try again") would be actively
        // wrong here — trying again cannot work — so it says what happened and
        // where the resource may still be bookable.
        [FailureCodes.ResourceNotDirectlyBookable] =
            "This resource is not offered for booking on its own. It may still be available as part of a service.",
        [FailureCodes.DateRangeInvalid] = "Please choose a valid date.",
        [FailureCodes.DateRangeTooLarge] = "Please choose a single date.",

        // A placement refusal is about THAT INSTANT, and this message says so.
        //
        // It is tempting to render `service-unavailable` as "this service is not
        // available for booking" — the code sounds permanent, and an earlier
        // version of this map did exactly that. It is wrong, and Core says so in
        // as many words: the placement-time refusal is "about that instant and
        // nothing more… this may not claim a configuration can never be fulfilled
        // — that is the configuration-time check's claim to make, over a
        // different graph" (ServiceBookingService.Shortfall). A structurally
        // impossible service and one whose resources merely happen to be busy
        // fail identically here.
        //
        // So the permanent claim is made in exactly one place — the flow's
        // configuration-time refusal page, which asks the different graph — and
        // never from this code. QA found the alternative live: a perfectly
        // bookable service, submitted with a start outside its hours, told the
        // visitor it was not available for booking while nine bookable times were
        // listed underneath.
        //
        // The domain's own message for this code names the roles that were short
        // and how many resources could provide them — written for the backoffice,
        // where someone can act on it. It is not carried here, and cannot be: the
        // map is from the code, and the code alone.
        [FailureCodes.ServiceUnavailable] =
            "That time is not available for this service. Please choose another.",
        // Worded away from the placement refusal above and from the
        // configuration-time page: this one means the service could not be found
        // at all, which is a fault rather than an answer about times.
        [FailureCodes.ServiceNotFound] = "Sorry, this service could not be found.",
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
                // Duration bounds concern the length control specifically.
                FailureCodes.DurationTooShort
                    or FailureCodes.DurationTooLong => BookingFieldIds.Duration,

                // `granularity` is deliberately left on the time list: Core
                // raises it both for a misaligned start and for a length off
                // the grid, and the code alone cannot tell them apart. The
                // length select only ever offers grid multiples, so through
                // the shipped form it always means the start.
                FailureCodes.Conflict
                    or FailureCodes.OutsideOpenHours
                    or FailureCodes.LeadTime
                    or FailureCodes.Horizon
                    or FailureCodes.Granularity
                    or FailureCodes.IntervalInvalid

                    // A placement refusal concerns the chosen start, like the
                    // codes above it — so it points at the time list, which is
                    // where the visitor acts on it.
                    or FailureCodes.ServiceUnavailable => BookingFieldIds.Times,
                _ => null,
            },
        };
}
