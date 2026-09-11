namespace UBookIt.Core.Notifications;

/// <summary>
/// The outcome of asking for site-supplied content, and the three answers are deliberately
/// distinct.
/// </summary>
/// <remarks>
/// <b>Absent and broken must not be the same answer.</b> Absence has a defined meaning — use the
/// package's own content, which is what every site gets until it supplies any — so collapsing a
/// template that threw into "there wasn't one" would turn an author's mistake into a message
/// nobody can account for: the wrong words arrive, nothing is logged as wrong, and the only
/// symptom is that the customisation someone wrote appears not to exist.
/// </remarks>
public enum BookingTemplateOutcome
{
    /// <summary>Content was rendered and is in the result.</summary>
    Rendered,

    /// <summary>No content is supplied for this message; the package's own is to be used.</summary>
    NotSupplied,

    /// <summary>Content is supplied and failed to render; the package's own is to be used.</summary>
    Failed,
}

/// <summary>
/// Site-supplied content for one message: the outcome, and — where it rendered — the body, an
/// optional subject, and whether the body is HTML.
/// </summary>
/// <param name="Outcome">Which of the three things happened.</param>
/// <param name="Body">The rendered body, or <c>null</c> unless <see cref="Outcome"/> is <c>Rendered</c>.</param>
/// <param name="Subject">
/// The subject the content stated, or <c>null</c> where it stated none — in which case the
/// package's own subject stands. Supplying content is not the same as taking responsibility for
/// everything about the message.
/// </param>
/// <param name="IsHtml">
/// Whether <see cref="Body"/> is HTML. <b>Defaults to false and is never inferred.</b> The
/// package's own content is plain text, so that is what an author inherits by saying nothing;
/// inspecting the body to guess would be wrong silently, and the failure would appear in
/// somebody's inbox.
/// </param>
public sealed record BookingTemplateResult(
    BookingTemplateOutcome Outcome,
    string? Body = null,
    string? Subject = null,
    bool IsHtml = false)
{
    /// <summary>No content is supplied for this message.</summary>
    public static readonly BookingTemplateResult NotSupplied = new(BookingTemplateOutcome.NotSupplied);

    /// <summary>Content is supplied and failed to render.</summary>
    public static readonly BookingTemplateResult Failed = new(BookingTemplateOutcome.Failed);
}

/// <summary>
/// Renders site-supplied content for a message. Renders, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>This port is declared by <c>UBookIt.Core</c>, which carries no package reference of any
/// kind</b> — the same constraint the observation port is shaped around. It is also why the seam
/// is this narrow: the package's own composer decides the audience, establishes what was booked,
/// refuses to write to an erased booker and keeps contact details out of internal messages, and
/// every one of those is tested where it lives. Handing a whole composer to an implementation
/// would move all of it into content-author territory. What crosses this seam is a model and a
/// name, and what comes back is text.
/// </para>
/// <para>
/// <b>Implementations are optional, and that is the registration mechanism.</b> Nothing
/// registers a default. A host that supports supplied content registers one; a host that does
/// not registers nothing, and the consumer resolves <c>null</c> and uses the package's own
/// content. Because nothing is ever replaced, no composer ordering can get this wrong — which
/// matters in this package, where an ordering assumption has already produced a defect that
/// silently disabled a feature. Umbraco's own <c>EmailSender</c> detects a registered handler
/// the same way.
/// </para>
/// <para>
/// <b>Implementations SHALL NOT throw.</b> A failure to render is reported as
/// <see cref="BookingTemplateOutcome.Failed"/>, because the caller has a booking that is already
/// stored and a message that must still go out. An escaping exception would cost the message
/// entirely — the opposite of the trade every failure path in this package makes.
/// </para>
/// <para>
/// <b>Implementations SHALL NOT require an ambient web request.</b> No sender outside a request exists yet — the retention
/// sweep erases bookers and sends nothing — so this is anticipatory: a renderer that reached for
/// a request would work everywhere it is used today and fail in the first thing that sends from
/// a timer, which is the least observable place to fail.
/// </para>
/// </remarks>
public interface IBookingTemplateRenderer
{
    /// <summary>
    /// Renders the content supplied for <paramref name="kind"/>, if any.
    /// </summary>
    /// <param name="kind">Which message is being composed.</param>
    /// <param name="model">
    /// What the content is given — a <see cref="BookerMessageModel"/> or an
    /// <see cref="InternalMessageModel"/>, matching the message's audience.
    /// </param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns>
    /// The rendered content, or a result saying nothing is supplied or that rendering failed.
    /// Never throws for either of those.
    /// </returns>
    Task<BookingTemplateResult> RenderAsync(
        BookingMessageKind kind,
        BookingMessageModel model,
        CancellationToken cancellationToken = default);
}
