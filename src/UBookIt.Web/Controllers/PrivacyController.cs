using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Core;
using UBookIt.Web.Models;

namespace UBookIt.Web.Controllers;

/// <summary>
/// Publishes the one fact about a booker's personal data that a consumer building its own
/// booking UI cannot obtain any other way: how long this site keeps it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The number, and no prose.</b> A headless consumer writes its own page and its own privacy
/// wording, in its own language — the package's English sentences would be of no use to it, and
/// publishing them here would make untranslatable prose part of a versioned contract we would
/// then have to localise. What such a consumer genuinely cannot work out is the retention
/// period, and without it the notice it writes cannot state how long the data it is collecting
/// will be kept, which is precisely the failure this feature exists to prevent.
/// </para>
/// <para>
/// <b>Its own endpoint, because retention is site-wide.</b> Carrying it on the resource or
/// service read models would repeat one value on every row of a paged read and invite a consumer
/// to believe it varies by resource.
/// </para>
/// <para>
/// <b>Anonymous, and that is not a disclosure.</b> A retention period is a policy a site
/// publishes to its visitors deliberately — the shipped Razor front end already prints it on a
/// public page. It names no person and identifies no booking.
/// </para>
/// </remarks>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UBookIt.Delivery")]
public sealed class PrivacyController(SiteBookingSettings settings) : UBookItDeliveryApiControllerBase
{
    [HttpGet("privacy")]
    [ProducesResponseType<PrivacyModel>(StatusCodes.Status200OK)]
    public IActionResult GetPrivacy()
        => Ok(new PrivacyModel
        {
            RetentionDays = settings.RetentionDays,
        });
}
