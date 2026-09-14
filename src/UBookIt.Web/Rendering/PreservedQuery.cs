using System.Reflection;
using Microsoft.Extensions.Primitives;

namespace UBookIt.Web.Rendering;

/// <summary>One host-page query parameter a GET form carries forward.</summary>
/// <param name="Name">The parameter's name, exactly as the request spelled it.</param>
/// <param name="Value">One value. A multi-valued parameter yields one pair per value.</param>
public readonly record struct PreservedQueryPair(string Name, string Value);

/// <summary>
/// Computes which of the current request's query parameters the shipped GET forms carry
/// forward as hidden inputs — a GET form <em>replaces</em> the query string on
/// submission, and uBookIt is a component inside somebody else's page, so that page's
/// parameters are not uBookIt's to discard.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only parameters the site has named are preserved</b>
/// (<see cref="FrontendSettings.PreservedQueryParameters"/>, empty by default).
/// Preserving unlisted parameters is declined on the record: every preserved value is
/// visitor-controlled input echoed back into the markup, and the configured bound is
/// what makes the reflection acceptable — encoding alone does not make an unbounded
/// reflector a good idea.
/// </para>
/// <para>
/// <b>uBookIt's own query parameters are excluded even when listed.</b> A hidden input
/// duplicating a live control's name would submit both values and leave the winner to
/// model binding — the exact accident <see cref="BookingKeys"/> documents against
/// <c>ubDate</c>/<c>ubDateOther</c>. The exclusion set is derived from
/// <see cref="BookingKeys"/> by reflection over its <c>*Query</c> constants rather than
/// restated, so a key added there joins the exclusion without anyone remembering to.
/// </para>
/// <para>
/// Pure over its inputs: the ViewComponents compute the pairs from the request and hand
/// them to the view models, the same route every other request-derived value takes, so
/// the flows stay host-free and the rendering suite drives every case by setting the
/// model.
/// </para>
/// </remarks>
public static class PreservedQuery
{
    /// <summary>
    /// The query-parameter names uBookIt itself owns — every <c>const string</c> on
    /// <see cref="BookingKeys"/> whose name ends in <c>Query</c>.
    /// </summary>
    public static readonly IReadOnlySet<string> OwnKeys = typeof(BookingKeys)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string)
            && field.Name.EndsWith("Query", StringComparison.Ordinal))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The pairs to render, in request order, every value of a multi-valued parameter
    /// kept. Name matching is case-insensitive, matching how ASP.NET query lookup
    /// behaves — a site configuring <c>Culture</c> must not silently lose
    /// <c>culture</c>.
    /// </summary>
    public static IReadOnlyList<PreservedQueryPair> Compute(
        IEnumerable<KeyValuePair<string, StringValues>> query,
        IReadOnlyCollection<string> preservedNames)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(preservedNames);

        if (preservedNames.Count == 0)
        {
            return [];
        }

        // Always rebuilt with this comparer, never taken as-is: a caller's set with a
        // different comparer would silently change the matching rule.
        var wanted = preservedNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return query
            .Where(parameter => wanted.Contains(parameter.Key) && !OwnKeys.Contains(parameter.Key))
            .SelectMany(parameter => parameter.Value.Select(value =>
                new PreservedQueryPair(parameter.Key, value ?? string.Empty)))
            .ToList();
    }
}
