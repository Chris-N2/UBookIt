using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using UBookIt.Core;
using UBookIt.Web.Controllers;

namespace UBookIt.Web.Composing;

/// <summary>
/// Removes the cancellation route entirely where self-service cancellation is off.
/// </summary>
/// <remarks>
/// <para>
/// <b>ABSENT, NOT REFUSED</b>, on the same terms as the delivery API's directions. A route that
/// exists and answers "no" is a route that tells a caller the feature exists, has a shape, and
/// might be enabled somewhere — and it is one handler's bug away from answering something else.
/// A site that has not turned this on has no such endpoint at all.
/// </para>
/// <para>
/// Applied as a convention rather than checked inside the controller for the reason the
/// permissions design already records: a condition somebody must remember to write leaves a route
/// that reaches the handler having forgotten it.
/// </para>
/// </remarks>
public sealed class CancellationExposureConvention(SelfServiceCancellationSettings settings)
    : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        if (settings.Enabled)
        {
            return;
        }

        foreach (var controller in application.Controllers)
        {
            if (controller.ControllerType != typeof(CancellationController).GetTypeInfo())
            {
                continue;
            }

            foreach (var action in controller.Actions)
            {
                action.Selectors.Clear();
            }
        }
    }
}
