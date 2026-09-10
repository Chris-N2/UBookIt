namespace UBookIt.Core;

/// <summary>
/// What, if anything, the package sends when a booking is placed or cancelled.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything here is off until a site turns it on, and the default instance is off.</b>
/// Installing or upgrading the package therefore sends nobody anything, which is the one change
/// a package must never make on a site's behalf: the people who would receive those messages
/// booked while it sent nothing, under a privacy notice that said so.
/// </para>
/// <para>
/// <b>These settings say what the site wants, not what the host can do.</b> Whether mail can be
/// sent at all is a separate question, answered by the host at the moment of sending — an Umbraco
/// site configures SMTP for password resets and backoffice invites long before it has any opinion
/// about booking confirmations, so a configured mail server is not permission to write to that
/// site's customers. Both must hold, and neither implies the other.
/// </para>
/// <para>
/// <b>The two directions are independent.</b> A site can be told about its own bookings without
/// anything being sent to the person who booked, and the reverse — see
/// <see cref="SendBookerEmails"/> and <see cref="InternalRecipients"/>.
/// </para>
/// </remarks>
public sealed record BookingNotificationSettings
{
    /// <summary>
    /// Whether the person who booked is sent a message when their booking is placed or cancelled.
    /// </summary>
    /// <remarks>
    /// <b>This is the setting the booking form's privacy notice is written against</b>, together
    /// with the host's own ability to send. A site that turns this on says a confirmation will be
    /// sent, because on that site one will be; a site that leaves it off says only that it is able
    /// to make contact. The notice and the behaviour are decided by the same expression so that
    /// they cannot come to disagree.
    /// </remarks>
    public bool SendBookerEmails { get; init; }

    /// <summary>
    /// The site's own addresses to tell about bookings. Empty when the site has configured none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Presence is the switch.</b> There is deliberately no second flag enabling internal
    /// messages: a site that supplies addresses has said what it wants by supplying them, and a
    /// separate on/off would add a way for the two to disagree — a list configured but disabled,
    /// or enabled but empty, neither of which has an obvious reading. This is the same shape the
    /// retention setting already uses.
    /// </para>
    /// <para>
    /// <b>Messages to these addresses carry no booker contact details.</b> Who may see a booker's
    /// name, address and telephone number is decided by the backoffice group the sensitive-data
    /// handling tests, and a list of addresses in configuration is a different population, gated
    /// by who can edit configuration. Putting contact details in these messages would route
    /// personal data around a control this package built on purpose, so they carry a reference,
    /// a time, what was booked, and a link to where the booking can be seen under that control.
    /// </para>
    /// <para>
    /// A flat list is knowingly the wrong shape for a site where different people are responsible
    /// for different resources or services. That needs a model of who owns what, and inventing
    /// half of one here to avoid a future migration would cost more than the migration.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> InternalRecipients { get; init; } = [];

    /// <summary>True when the site has configured at least one usable internal recipient.</summary>
    public bool HasInternalRecipients => InternalRecipients.Count > 0;

    /// <summary>
    /// Whether the person who booked will actually be sent a message, given whether the host can
    /// send mail at all.
    /// </summary>
    /// <param name="hostCanSendMail">
    /// Whether the host reports it can send mail. Supplied by the caller rather than read here
    /// because this assembly has no dependencies and therefore no way to ask.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>This exists so that the sentence and the behaviour cannot come to disagree.</b> The
    /// booking form's privacy notice may say a confirmation will be sent only where one will be,
    /// and the sending path must send only under the same condition — so both call this, and it is
    /// the whole of the condition rather than a restatement of it. A site that asked for messages
    /// on a host that cannot send them sends nothing, and its notice must therefore promise
    /// nothing; keeping the two in step by writing the conjunction out twice is how they drift.
    /// </para>
    /// <para>
    /// It lives here, on a settings record in an assembly with no package references at all,
    /// because it is the only place the front end and the sending path can both reach: the
    /// rendering assembly does not reference the persistence one.
    /// </para>
    /// </remarks>
    public bool WillEmailBooker(bool hostCanSendMail) => SendBookerEmails && hostCanSendMail;
}
