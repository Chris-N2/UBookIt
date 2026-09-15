namespace UBookIt.Core.Stores;

/// <summary>
/// The settings a site has overridden through the backoffice, keyed by the configuration key
/// they override.
/// </summary>
/// <remarks>
/// <para>
/// <b>A key with no row means "not overridden", and there is no other way to say it.</b> No
/// sentinel value, no "cleared" marker, no row carrying null. That is forced rather than chosen:
/// several settings treat <i>no value</i> as meaningful — <c>PrivacyPolicyUrl</c> null means "no
/// link" and <c>RetentionDays</c> null means "do not erase" — so a stored representation of
/// "unset" would be indistinguishable from a stored representation of those. Absence is the only
/// spelling that cannot collide with a real value.
/// </para>
/// <para>
/// It is also what makes restoring a setting honest: removing the row lets the configured value
/// show through again, and a later change to the site's configuration takes effect. Storing the
/// configured value instead would freeze it at the moment somebody clicked, and the file would
/// stop describing what runs without anything looking wrong.
/// </para>
/// <para>
/// <b>Values are held as text, exactly as a configuration source would supply them.</b> This store
/// parses nothing and validates nothing. Its rows are composed as a configuration layer beneath
/// the package's existing resolution, so a stored value and a configured value of the same setting
/// go through the same reader and the same fallbacks — the alternative being two implementations
/// of rules whose asymmetry is load-bearing, which would drift silently.
/// </para>
/// <para>
/// <b>Nothing here is personal data, ever</b> — the same guarantee the flag store carries. These
/// are the site's own settings, not anybody's details.
/// </para>
/// </remarks>
public interface ISettingsStore
{
    /// <summary>
    /// Every stored override, as configuration key to value. Empty when nothing is overridden,
    /// which is the state of a fresh install and of an upgraded one until somebody saves.
    /// </summary>
    /// <remarks>
    /// <b>Synchronous, deliberately, and alone among these members.</b> This read participates in
    /// resolving <c>SiteBookingSettings</c>, which roughly fifteen consumers take by value in
    /// their constructors — so it happens during synchronous service resolution, which cannot
    /// await. The alternatives were both worse: blocking on an async read would put
    /// sync-over-async on every request path, and reshaping fifteen constructors to take a
    /// provider would break a public surface frozen at 17.0.0 to avoid a query against a table of
    /// fewer than ten rows. The writes below never resolve anything and stay async.
    /// </remarks>
    IReadOnlyDictionary<string, string> GetAll();

    /// <summary>
    /// Stores <paramref name="value"/> for <paramref name="key"/>, replacing any value already
    /// stored for it. At most one row per key.
    /// </summary>
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes any stored value for <paramref name="key"/>, so the configured value shows through
    /// again. Removing a key that is not stored is not an error — the requested state is the
    /// resulting state either way.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
