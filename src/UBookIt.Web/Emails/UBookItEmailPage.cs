using Microsoft.AspNetCore.Mvc.Razor;

namespace UBookIt.Web.Emails;

/// <summary>
/// The base page a uBookIt email template inherits, so that stating a subject or declaring HTML
/// is a typed assignment rather than a dictionary key.
/// </summary>
/// <remarks>
/// <para>
/// A template opens with
/// <c>@inherits UBookIt.Web.Emails.UBookItEmailPage&lt;UBookIt.Core.Notifications.BookerMessageModel&gt;</c>
/// and may then write <c>@{ Subject = "Your appointment at Acme"; IsHtml = true; }</c>.
/// </para>
/// <para>
/// <b>Typed properties over a dictionary, because this contract is frozen at the first full
/// release.</b> <c>ViewData["Subject"]</c> would put a magic string into every template a site
/// ever writes, and a misspelling would be ignored in silence — the message would simply go out
/// with the package's subject and nothing anywhere would say why. Assigning a property that does
/// not exist does not compile.
/// </para>
/// <para>
/// <b>The transport underneath is <c>HttpContext.Items</c>, and it is that rather than
/// <c>ViewData</c> because of something measured rather than assumed.</b> The first version used
/// <c>ViewData</c> — the renderer owns the dictionary it passes in, so reading it back looked
/// reliable. It is not: MVC activates a <see cref="RazorPage{TModel}"/> with a
/// <c>ViewDataDictionary&lt;TModel&gt;</c> **copied** from the one supplied, so a template's
/// writes land in the copy and the renderer reads an empty entry. A stated subject silently
/// became no subject, and the round-trip test is the only thing that showed it.
/// </para>
/// <para>
/// <c>HttpContext.Items</c> is one dictionary shared by everything holding the context, and the
/// renderer creates a fresh context per message — so a value written by one template cannot leak
/// into another. The author still sees two typed properties; only the plumbing moved.
/// </para>
/// <para>
/// <b><see cref="RazorPage{TModel}"/>, deliberately not <c>UmbracoViewPage&lt;T&gt;</c>.</b>
/// Umbraco's page brings <c>IUmbracoContextAccessor</c>, <c>IPublishedUrlProvider</c> and the rest
/// of a request's worth of services with it. Messages are composed from work that has no request
/// — the retention sweep already runs that way, and a reminder would — so inheriting it would
/// make templates render in a controller and fail in a job. What a template loses is Umbraco's
/// helpers, which have no business in an email body anyway.
/// </para>
/// </remarks>
/// <typeparam name="TModel">
/// The model this template receives — <c>BookerMessageModel</c> or <c>InternalMessageModel</c>,
/// matching the message's audience. A template that declares the wrong one for its message will
/// not bind, which is the failure being sought: it is reported when the template is used rather
/// than by rendering something addressed to the wrong reader.
/// </typeparam>
public abstract class UBookItEmailPage<TModel> : RazorPage<TModel>
{
    /// <summary>
    /// The key the subject travels under. Internal: a template sets
    /// <see cref="Subject"/>, and the renderer reads this — nobody else should need the string.
    /// </summary>
    internal const string SubjectKey = "UBookIt.Subject";

    /// <summary>The key the HTML flag travels under. Internal, on the same reasoning.</summary>
    internal const string IsHtmlKey = "UBookIt.IsHtml";

    /// <summary>
    /// Where the two values above travel. <c>ViewData</c> cannot carry them — see the remarks on
    /// this type — because the page is activated with a copy of it.
    /// </summary>
    private IDictionary<object, object?> Carrier => ViewContext.HttpContext.Items;

    /// <summary>
    /// The subject to send this message with, or <c>null</c> to keep the package's own.
    /// </summary>
    /// <remarks>
    /// Leaving it unset is a supported choice rather than an oversight: supplying a body is not
    /// the same as taking responsibility for the whole message, and a site that only wants
    /// different wording in the body should not have to restate a subject it was happy with.
    /// </remarks>
    public string? Subject
    {
        get => Carrier.TryGetValue(SubjectKey, out var value) ? value as string : null;
        set => Carrier[SubjectKey] = value;
    }

    /// <summary>
    /// Whether this template's output is HTML. <c>false</c> unless the template says otherwise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Never inferred from the output.</b> The package's own messages are plain text, so that
    /// is what a template inherits by saying nothing, and an author writing HTML declares it.
    /// Inspecting a body for angle brackets — which Umbraco's own health-check notification does
    /// — guesses at intent, and a wrong guess is wrong silently in somebody's inbox.
    /// </para>
    /// <para>
    /// <b>Declaring HTML means sending no plain-text alternative.</b> Umbraco's
    /// <c>EmailMessage</c> carries one body and a flag; there is no multipart message to be had
    /// without bypassing the site's own mail configuration. See the documentation.
    /// </para>
    /// </remarks>
    public bool IsHtml
    {
        get => Carrier.TryGetValue(IsHtmlKey, out var value) && value is true;
        set => Carrier[IsHtmlKey] = value;
    }
}
