namespace UBookIt.Core;

/// <summary>
/// Whether a booker may cancel their own booking from a link in the message the package sends them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Off until a site turns it on, and the default instance is off.</b> Installing or upgrading
/// the package therefore opens no route and issues no credential — the same rule the notification
/// settings follow, and for the same reason: a package must not begin doing something on a site's
/// behalf that the site never asked for.
/// </para>
/// <para>
/// <b>This is an exposure switch, not a policy one.</b> It opens a route that changes a site's data
/// for a caller the package cannot identify beyond a secret, which is the same class of decision as
/// whether the delivery API answers at all. That is why it sits in configuration and in the
/// settings screen's <i>read-only</i> tier: the boundary exists to keep irreversible erasure and the
/// package's anonymous exposure out of an operator's reach.
/// </para>
/// <para>
/// <b>It cannot work where the booker is sent no message, and that is mechanical rather than a
/// second rule.</b> The cancellation link travels in the booker's own message; where
/// <see cref="BookingNotificationSettings.SendBookerEmails"/> is off there is no message, so there
/// is no vehicle and no link. The settings screen states that dependency where the value is shown,
/// because a setting that reports itself on while doing nothing describes a configuration the site
/// does not have.
/// </para>
/// </remarks>
public sealed record SelfServiceCancellationSettings
{
    /// <summary>The configuration section the composer binds this from.</summary>
    public const string SectionKey = "UBookIt:SelfServiceCancellation";

    /// <summary>
    /// Whether the package issues cancellation links and serves the route that redeems them.
    /// </summary>
    /// <remarks>
    /// Turning this off stops new links being issued and stops the route being served, which
    /// strands anyone still holding one. That consequence is documented rather than mitigated: the
    /// fallback is the position before the feature existed — the booker contacts the site.
    /// </remarks>
    public bool Enabled { get; init; }
}
