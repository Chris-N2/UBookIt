using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Web.Composing;
using Umbraco.Cms.Api.Common.Attributes;

namespace UBookIt.Web.Controllers;

/// <summary>
/// Base for the public delivery API — <b>off by default</b>, per direction:
/// <see cref="DeliveryApiExposureConvention"/> removes the routes of any direction the
/// site has not enabled, so deriving from this base is what places an action under
/// that regime, and every action carries exactly one direction attribute. Where a
/// direction is enabled, the API is anonymous by design (delivery-api spec,
/// "Anonymous access and auth stance"): no backoffice authorization policy, no
/// cookie anti-forgery, and no ambient identity is trusted for writes. Every
/// <b>alternative</b> UI — a separate-repo DevExpress UI, a SPA, a mobile client
/// — is a symmetric consumer of this same contract.
/// <para>
/// The default Razor front end is deliberately <b>not</b> among them, and used to
/// be listed here. It reads Core in-process, which `default-frontend` requires of
/// it: an in-process read cannot be told apart from the caller's own, and routing
/// a server render through the public API would buy a network hop, an auth
/// surface and a second failure mode for nothing.
/// </para>
/// </summary>
[ApiController]
[AllowAnonymous]
[Route(Constants.RouteBase)]
[MapToApi(Constants.DeliveryApiName)]
public abstract class UBookItDeliveryApiControllerBase : ControllerBase
{
}
