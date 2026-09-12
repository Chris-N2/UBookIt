using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using UBookIt.Web.Controllers;

namespace UBookIt.Web.Composing;

/// <summary>
/// Makes a disabled delivery direction <b>absent</b>. Runs once, when MVC builds its
/// application model at startup: every action on a delivery controller whose direction
/// the site has not enabled loses its selectors — so no route ever matches and a
/// request receives the host's own not-found response, indistinguishable from a path
/// that never existed — and its ApiExplorer visibility, so it does not appear in the
/// delivery OpenAPI document.
/// </summary>
/// <remarks>
/// <para>
/// <b>Absence over refusal, structurally.</b> A request-time filter would have to
/// fabricate its own 404 (a second not-found shape to keep identical to the host's
/// forever), would leave the operations in the OpenAPI document unless a second
/// mechanism hid them, and — worse — a 403 or a "disabled" body would tell an
/// unauthenticated stranger that the package is installed and the endpoint exists to
/// be turned on. With no selector there is no code of ours that could answer at all,
/// so nothing can leak the difference.
/// </para>
/// <para>
/// <b>An unclassified action is treated as belonging to no enabled direction</b> —
/// removed whichever switches are on. Exposure must never be the default for an
/// endpoint nobody classified; the classification-totality test is what turns that
/// situation into a named failure instead of a silent removal.
/// </para>
/// </remarks>
public sealed class DeliveryApiExposureConvention(DeliveryApiSettings settings) : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            if (!typeof(UBookItDeliveryApiControllerBase).IsAssignableFrom(controller.ControllerType))
            {
                continue;
            }

            foreach (var action in controller.Actions)
            {
                if (!IsEnabled(action))
                {
                    action.Selectors.Clear();
                    action.ApiExplorer.IsVisible = false;
                }
            }
        }
    }

    private bool IsEnabled(ActionModel action)
    {
        var isRead = action.ActionMethod.GetCustomAttribute<DeliveryReadAttribute>() is not null;
        var isPlacement = action.ActionMethod.GetCustomAttribute<DeliveryPlacementAttribute>() is not null;

        // Exactly one attribute names a direction; anything else — none, or both — is
        // unclassified and stays absent whatever is enabled. See the class remarks.
        return (isRead, isPlacement) switch
        {
            (true, false) => settings.EnableReads,
            (false, true) => settings.EnablePlacement,
            _ => false,
        };
    }
}
