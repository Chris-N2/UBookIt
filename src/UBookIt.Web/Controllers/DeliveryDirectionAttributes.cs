namespace UBookIt.Web.Controllers;

/// <summary>
/// Marks a delivery action as belonging to the <b>read</b> direction of
/// <see cref="DeliveryApiSettings"/>. Every delivery action carries exactly one
/// direction attribute — declared, never inferred from the HTTP verb, because the verb
/// lies here: the management API already uses POST for a read, and a future POST-shaped
/// read on this API would otherwise be silently misclassified as placement. A test
/// enumerates every delivery action and fails on one carrying none or both.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class DeliveryReadAttribute : Attribute
{
}

/// <summary>
/// Marks a delivery action as belonging to the <b>placement</b> direction of
/// <see cref="DeliveryApiSettings"/>. See <see cref="DeliveryReadAttribute"/> for why
/// this is declared rather than inferred.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class DeliveryPlacementAttribute : Attribute
{
}
