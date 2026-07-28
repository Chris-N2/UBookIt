using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;

namespace UBookIt.Web.Controllers;

/// <summary>
/// Base for the public delivery API. Anonymous by design (delivery-api spec,
/// "Anonymous access and auth stance"): no backoffice authorization policy, no
/// cookie anti-forgery, and no ambient identity is trusted for writes. Every UI
/// — the default front-end, a separate-repo DevExpress UI, a SPA, mobile — is a
/// symmetric consumer of this same contract.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route(Constants.RouteBase)]
[MapToApi(Constants.DeliveryApiName)]
public abstract class UBookItDeliveryApiControllerBase : ControllerBase
{
}
