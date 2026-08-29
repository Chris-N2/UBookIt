using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Web.Common.Routing;

namespace UBookIt.Backoffice.Controllers
{
    /// <summary>
    /// The shared base every uBookIt management endpoint derives from, so authorization is
    /// applied in one place rather than per controller.
    /// </summary>
    /// <remarks>
    /// The policy grants access on the basis of <b>uBookIt's own section</b>. It previously
    /// used Umbraco's <c>SectionAccessContent</c>, which was wrong in both directions: a
    /// user granted uBookIt but not Content was refused an API for a section they could
    /// see, and a user granted Content but not uBookIt could call every uBookIt endpoint
    /// for a section they could not. See <see cref="Security.UBookItSectionHandler"/>.
    /// </remarks>
    [ApiController]
    [BackOfficeRoute("ubookitbackoffice/api/v{version:apiVersion}")]
    [Authorize(Policy = Constants.SectionAccessPolicy)]
    [MapToApi(Constants.ApiName)]
    public class UBookItBackofficeApiControllerBase : ControllerBase
    {
    }
}
