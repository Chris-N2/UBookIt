namespace UBookIt.Web;

/// <summary>
/// Delivery API constants. The route base is a public, versioned path distinct
/// from the backoffice route — it mirrors Umbraco's own anonymous content
/// Delivery API location convention (<c>umbraco/…/api/v{version}</c>).
/// </summary>
public static class Constants
{
    /// <summary>The <c>MapToApi</c> name and OpenAPI document name for the delivery API.</summary>
    public const string DeliveryApiName = "ubookitdelivery";

    /// <summary>Public, versioned route base. No backoffice authorization applies.</summary>
    public const string RouteBase = "umbraco/ubookit/api/v{version:apiVersion}";

    /// <summary>
    /// Stable code for a request rejected at the transport/model-binding layer
    /// (malformed body, unparseable or missing parameter) — as opposed to a
    /// domain rule. Keeps the <c>errors[]</c> envelope uniform (design D7).
    /// </summary>
    public const string InvalidRequestCode = "invalid-request";

    /// <summary>
    /// Where a booker cancels their own booking, using the link sent to them.
    /// </summary>
    /// <remarks>
    /// <b>Must agree with the path the link builder writes into messages</b>, and a guard holds
    /// the two equal — a link that is built one way and routed another is broken only for the
    /// person who needs it, days later, with nothing to tell them why.
    /// <para>
    /// A package route rather than a page a site must create: the link has to work on every
    /// installation, including one whose visitor-facing front end is headless.
    /// </para>
    /// </remarks>
    public const string CancellationPath = "umbraco/ubookit/cancel";
}
