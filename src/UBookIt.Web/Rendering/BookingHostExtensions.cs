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
    /// <summary>
    /// The chosen date: the "another date" field where one was filled in, otherwise the date
    /// selected from the list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two parameters, and the precedence is decided here rather than by model binding.</b>
    /// The list and the field both set "which date", and they deliberately do not share a query
    /// parameter: a form submitting one name from two controls sends both values, and which one
    /// wins is then an accident of how the binder happens to read a duplicated key.
    /// </para>
    /// <para>
    /// <b>The typed date wins.</b> It is the more deliberate act — a visitor who selects from the
    /// list and then types is correcting themselves, and the opposite rule would silently discard
    /// what they typed in favour of the radio they had already moved past. An unparseable typed
    /// value falls through to the list rather than blanking the choice, because a malformed date
    /// is not an instruction to forget the one already made.
    /// </para>
    /// </remarks>
    public static DateOnly? ReadDateQuery(this HttpRequest request)
        => DateOnly.TryParse(request.Query[BookingKeys.OtherDateQuery], out var typed)
            ? typed
            : DateOnly.TryParse(request.Query[BookingKeys.DateQuery], out var listed)
                ? listed
                : null;

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
