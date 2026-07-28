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
}
