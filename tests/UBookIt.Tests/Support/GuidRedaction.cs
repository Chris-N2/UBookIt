using System.Text.RegularExpressions;

namespace UBookIt.Tests.Support;

/// <summary>
/// Removes GUID-shaped tokens from a haystack before personal data is looked for in it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists:</b> "Ada" is three hex digits, so it occurs inside a random
/// 32-hex-digit GUID roughly 0.7% of the time — and several guards assert
/// <c>DoesNotContain("Ada", …)</c> over payloads and log lines that legitimately carry a
/// booking or resource id. Those guards failed at random (~2% of full-suite runs across
/// the class), were misdiagnosed once as a build race, and got re-run until green.
/// "07700" and "01234" have the same shape: every character is a hex digit.
/// </para>
/// <para>
/// <b>Every GUID, not just the one the test created.</b> Redacting the known id fixes
/// today and leaves the class: the next id added to the message — a resource id in a
/// warning, a service id in a payload — reintroduces the collision while looking exactly
/// like a PII leak. The shape is what collides, so the shape is what is redacted.
/// </para>
/// <para>
/// <b>The needles stay granular.</b> The rejected alternative was matching
/// "Ada Lovelace" instead of "Ada" — rejected because a line carrying only a first name
/// is a real leak the guard must still catch. Booker names, emails and phone numbers are
/// not GUID-shaped, so this redaction cannot mask one.
/// </para>
/// <para>
/// <b>Not for guards whose claim is about ids.</b> A guard asserting a message does NOT
/// leak a raw id (for example the service-frontend wording tests) must read the raw
/// text — redacting first would make it pass vacuously.
/// </para>
/// <para>
/// One implementation, extracted when the third copy appeared — this suite has already
/// paid for a copied helper once: the copy was fixed after a false failure and the
/// original stood defective beside it for a change and a half.
/// </para>
/// </remarks>
public static class GuidRedaction
{
    private static readonly Regex AnyGuid = new(
        "[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}",
        RegexOptions.Compiled);

    /// <summary>The text with every GUID-shaped token replaced by <c>{guid}</c>.</summary>
    public static string WithoutGuids(string text) => AnyGuid.Replace(text, "{guid}");
}
