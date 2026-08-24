using System.Text.RegularExpressions;

namespace UBookIt.Tests.Rendering.Support;

/// <summary>
/// The values that differ between two renders of the <b>same</b> view and the
/// <b>same</b> model, and the one place they are removed.
/// <para>
/// A view whose form comes from <c>Html.BeginUmbracoForm</c> emits three such
/// values: a per-render GUID form id, an anti-forgery token, and the encrypted
/// <c>ufprt</c> route token. Nothing about them is a function of the model.
/// </para>
/// <para>
/// <b>Why this exists, and why it is not snapshot infrastructure.</b> Rule 2 decides
/// whether a model member is live by rendering twice and comparing. On a view
/// carrying one of these values the two renders are <i>never</i> equal, so every
/// member is reported live and the rule asserts nothing — vacuous on exactly the
/// views hardest to check by reading. That is not a hypothetical: a provably dead
/// branch in <c>Service.cshtml</c> passed the whole suite, while the identical
/// shape in the deterministic <c>_DateAndLength.cshtml</c> failed.
/// </para>
/// <para>
/// Stripping happens <b>at the comparison</b>, never in the renderer. Every other
/// rule reads the real output: the id-uniqueness rule still sees the form id, and
/// the markup rules still see the tokens. Only the question "did changing the model
/// change the page" is asked of a projection, because only that question is
/// corrupted by an answer that changes on its own.
/// </para>
/// </summary>
public static class RenderNondeterminism
{
    /// <summary>The auto-generated form id: <c>id="form&lt;32 hex&gt;"</c>.</summary>
    private static readonly Regex FormId =
        new(@"id=""form[0-9a-f]{32}""", RegexOptions.Compiled);

    /// <summary>
    /// The two hidden token fields, matched by <b>name</b> rather than by the shape
    /// of their payload: a data-protection payload's prefix is an implementation
    /// detail of ASP.NET Core, while these two names are the contract the form
    /// posts under.
    /// </summary>
    private static readonly Regex Token =
        new(
            @"<input name=""(?:__RequestVerificationToken|ufprt)""[^>]*?value=""[^""]*""",
            RegexOptions.Compiled);

    /// <summary>
    /// How many varying values a rendered document carries. Exposed so a test can
    /// assert the stripping actually fires: if Umbraco changes either shape, the
    /// patterns silently stop matching and rule 2 silently returns to vacuity, which
    /// is the failure this type exists to end rather than to reintroduce quietly.
    /// </summary>
    public static int CountIn(string html)
        => FormId.Matches(html).Count + Token.Matches(html).Count;

    /// <summary>
    /// The document with those values replaced by fixed text, so two renders of one
    /// model compare equal and a difference means the model made it.
    /// </summary>
    public static string Strip(string html)
        => Token.Replace(
            FormId.Replace(html, @"id=""form-render-invariant"""),
            @"<input name=""token"" value=""render-invariant""");
}
