namespace UBookIt.Core;

/// <summary>
/// Site-wide booking settings. v1 constraint: one time zone per site;
/// availability rules are wall-clock in this zone.
/// </summary>
public sealed record SiteBookingSettings
{
    /// <summary>IANA time zone id (e.g. "Europe/London").</summary>
    public required string TimeZoneId { get; init; }

    /// <summary>
    /// Maximum inclusive span, in days, an availability free-time or slot
    /// query may cover. An admin guardrail: free-time computation walks the
    /// range day-by-day, so an unbounded span is a cost hole. Queries wider
    /// than this are rejected with <see cref="Common.FailureCodes.DateRangeTooLarge"/>.
    /// Defaults to 31.
    /// </summary>
    public int MaxQueryRangeDays { get; init; } = 31;

    /// <summary>
    /// How many days after a booking's <b>end</b> its personal data is erased
    /// automatically, or <c>null</c> when the site has configured no retention.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Null means no retention, and no other value does.</b> Not <c>0</c>, and not some
    /// large number standing in for "never": a later feature states the configured period in a
    /// privacy notice, and it has to be able to tell <i>"we erase after N days"</i> from
    /// <i>"we do not erase"</i> without being able to publish the second as the first.
    /// </para>
    /// <para>
    /// <b>Resolved on different terms from <see cref="MaxQueryRangeDays"/>, deliberately.</b>
    /// That setting falls back to a working default when it cannot be read, because the cost of
    /// being wrong is a rejected query. This one falls back to <c>null</c> — off — because the
    /// cost of being wrong is the irreversible destruction of personal data. The failure
    /// direction is chosen to keep data rather than to keep the feature.
    /// </para>
    /// </remarks>
    public int? RetentionDays { get; init; }

    /// <summary>
    /// A link to the site's own privacy policy, presented by the booking form's privacy notice,
    /// or <c>null</c> when the site has configured none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The package writes the sentences it can keep true; this is where everything else
    /// lives.</b> What is collected, why, for how long and who can see it are facts about this
    /// package's own code. Jurisdiction, the identity of the data controller, other processing a
    /// site performs and how to complain are not — and a package that guessed at them would be
    /// putting words a site never wrote onto its public pages.
    /// </para>
    /// <para>
    /// <b>Null means no link, and the notice renders without one.</b> It is not a placeholder to
    /// be filled in with "#": a link that goes nowhere is worse on a privacy notice than no link,
    /// because it looks like the policy exists.
    /// </para>
    /// <para>
    /// <b>A value that cannot be used as a link resolves to null.</b> This is the one setting
    /// whose value reaches an <c>href</c> on a public page, so what is refused matters more here
    /// than for the others — see the resolution in the persistence composer.
    /// </para>
    /// </remarks>
    public string? PrivacyPolicyUrl { get; init; }
}
