using System.Text.RegularExpressions;

namespace UBookIt.Core.Common;

/// <summary>
/// The normalized-key form shared by every extensible string key in the domain:
/// resource type keys, service role resource-type keys, and capability keys.
/// Lower-case kebab-case, non-empty.
/// <para>
/// Deliberately a bare predicate rather than a helper that decides anything. The
/// caller owns which failure code a non-matching key produces, because the same
/// shape means different things in different places — a malformed type key and a
/// malformed capability key must stay separately reportable.
/// </para>
/// </summary>
public static partial class NormalizedKey
{
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex Pattern();

    /// <summary>Whether <paramref name="key"/> is a well-formed normalized key.</summary>
    public static bool IsValid(string? key) => key is not null && Pattern().IsMatch(key);
}
