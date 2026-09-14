namespace UBookIt.Web;

/// <summary>
/// Settings for the shipped front-end rendering, bound once at startup by
/// <see cref="Composing.UBookItRenderingComposer"/>.
/// </summary>
/// <remarks>
/// A render concern, so it lives here rather than on <c>SiteBookingSettings</c> in Core —
/// Core holds what a booking <em>is</em>, and knows nothing about how a page is drawn.
/// </remarks>
public sealed record FrontendSettings
{
    /// <summary>The configuration section the composer binds this from.</summary>
    public const string SectionKey = "UBookIt:Frontend";

    /// <summary>
    /// The names of host-page query parameters the shipped GET forms carry forward as
    /// hidden inputs, so submitting a form does not delete them from the URL.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Empty by default, and the default is the decision.</b> uBookIt is a component
    /// inside somebody else's page and cannot know which parameters that page depends
    /// on; preserving <em>everything</em> would turn the forms into a reflector of
    /// arbitrary visitor-controlled input, so the site names its own parameters and the
    /// list is the bound. An unconfigured site renders exactly what it rendered before
    /// this setting existed.
    /// </para>
    /// <para>
    /// Names are matched case-insensitively. uBookIt's own query parameters are never
    /// preserved through this mechanism, listed or not — a hidden input duplicating a
    /// live control's name would submit both values and leave the winner to model
    /// binding. See <see cref="Rendering.PreservedQuery"/>.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> PreservedQueryParameters { get; init; } = [];
}
