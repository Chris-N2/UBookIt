using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace UBookIt.Web.Rendering;

/// <summary>
/// The host-bound plumbing both flows share: reading a step's choices off the
/// query string and carrying a Post-Redirect-Get payload through TempData.
/// <para>
/// Shared because it is the part most easily got subtly wrong twice — a
/// deserialization that throws on a tampered payload takes the page down with it,
/// and a second copy would only have to forget the <c>catch</c> once.
/// </para>
/// </summary>
internal static class BookingHostExtensions
{
    public static DateOnly? ReadDateQuery(this HttpRequest request)
        => DateOnly.TryParse(request.Query[BookingKeys.DateQuery], out var date) ? date : null;

    public static int? ReadDurationQuery(this HttpRequest request)
        => int.TryParse(request.Query[BookingKeys.DurationQuery], out var minutes) ? minutes : null;

    /// <summary>
    /// The resource a visitor chose to fulfil a service's selectable role, per the
    /// URL, or null when the URL does not say. A malformed value is read as no
    /// choice; a well-formed one naming a resource that cannot fulfil the service
    /// is a <em>stale</em> choice, which the flow resets and says it has (design
    /// D11) rather than this parsing step silently dropping.
    /// </summary>
    public static Guid? ReadResourceQuery(this HttpRequest request)
        => Guid.TryParse(request.Query[BookingKeys.ResourceQuery], out var id) ? id : null;

    /// <summary>What is being booked, per the URL, or null when the URL does not say.</summary>
    public static BookingSubject? ReadSubjectQuery(this HttpRequest request)
        => BookingSubject.TryParse(request.Query[BookingKeys.SubjectQuery], out var subject) ? subject : null;

    /// <summary>
    /// Reads a stashed payload, treating a malformed one as absent. TempData is
    /// carried in a cookie the visitor can edit, so a failure to deserialize is a
    /// reachable state rather than an impossible one.
    /// </summary>
    public static T? Read<T>(this ITempDataDictionary tempData, string key)
    {
        if (tempData[key] is not string json || string.IsNullOrEmpty(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public static void Stash<T>(this ITempDataDictionary tempData, string key, T value)
        => tempData[key] = JsonSerializer.Serialize(value);
}
