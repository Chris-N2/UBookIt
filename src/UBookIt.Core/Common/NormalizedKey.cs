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

    /// <summary>
    /// The longest key any storage column accepts. Shape validity alone is not
    /// enough: a well-formed key longer than the column truncates or throws at
    /// INSERT, which surfaces as a 500 rather than the stable validation code
    /// the specs promise.
    /// </summary>
    public const int MaxLength = 64;

    /// <summary>Whether <paramref name="key"/> is a well-formed normalized key.</summary>
    /// <remarks>
    /// Shape only — <see cref="MaxLength"/> is checked separately by the caller,
    /// so a malformed key and an over-long one can carry different messages.
    /// </remarks>
    public static bool IsValid(string? key) => key is not null && Pattern().IsMatch(key);
}
