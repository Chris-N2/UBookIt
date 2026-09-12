using Microsoft.Extensions.Logging;
using UBookIt.Persistence.Composing;
using UBookIt.Persistence.Stores;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace UBookIt.Backoffice.Composers;

/// <summary>
/// Registers the one-time permissions seed. <c>[ComposeAfter]</c> the persistence
/// composer, because handlers of one notification run in registration order and the
/// seed's marker table arrives with the migrations handler registered there — the seed
/// must run after it.
/// </summary>
[ComposeAfter(typeof(UBookItPersistenceComposer))]
public sealed class UBookItPermissionSeedComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, UBookItPermissionSeed>();
}

/// <summary>
/// Grants the three permission verbs, exactly once per installation, to every user group
/// that holds the uBookIt section and no uBookIt verb — so an installation upgrading to
/// the permissions model keeps, group for group, exactly the access it had.
/// </summary>
/// <remarks>
/// <para>
/// <b>Chosen over "no verb means full access"</b>, which needs no write but makes
/// toggling off a group's last verb a silent full grant. After this seed, the toggles
/// mean what they show: empty is nothing, exactly as document permissions read.
/// </para>
/// <para>
/// <b>The flag is written only when every group succeeded.</b> A partial failure logs
/// loudly, fails nothing (a permissions seed is not worth a site's boot), and leaves the
/// flag absent so the next startup retries — and the retry is idempotent because the
/// selection rule skips any group that already holds a <c>UBookIt.</c>-prefixed verb,
/// including the ones the failed run already granted. For the same reason a group an
/// administrator has since edited is never touched twice.
/// </para>
/// <para>
/// <b>The one-shot boundary, stated:</b> once the flag exists the seed never runs again,
/// so a group emptied of its verbs by an administrator stays emptied, and a group granted
/// the section AFTER this version starts from nothing until its verbs are ticked — "tick
/// the section, then tick what they may do", per the docs.
/// </para>
/// <para>
/// Updates are performed as the super-user (the Umbraco super-user key):
/// the seed is a system operation with no acting person, and the audit trail should say
/// so rather than borrow whoever booted the site.
/// </para>
/// </remarks>
internal sealed class UBookItPermissionSeed(
    IFlagStore flags,
    IUserGroupService userGroupService,
    IRuntimeState runtimeState,
    ILogger<UBookItPermissionSeed> logger) : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    internal const string FlagKey = "permissions-seed";

    private static readonly string[] AllVerbs =
    [
        Backoffice.Constants.Verbs.BookingsRead,
        Backoffice.Constants.Verbs.BookingsManage,
        Backoffice.Constants.Verbs.Configure,
    ];

    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level is not (RuntimeLevel.Run or RuntimeLevel.Upgrade))
        {
            return;
        }

        try
        {
            if (await flags.ExistsAsync(FlagKey, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await SeedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Not rethrown: a permissions seed is not worth a site's boot, and the absent
            // flag makes the next boot retry. Loud, because until it succeeds a group
            // holding the section may be refused endpoints it could reach before upgrade.
            logger.LogError(
                exception,
                "uBookIt could not seed the permission verbs. Groups holding the uBookIt "
                + "section may lack access until the next successful start. The seed will retry.");
        }
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        var groups = await userGroupService.GetAllAsync(0, int.MaxValue).ConfigureAwait(false);
        var failed = 0;

        foreach (var group in groups.Items)
        {
            if (!ShouldSeed(group.AllowedSections, group.Permissions))
            {
                continue;
            }

            foreach (var verb in AllVerbs)
            {
                group.Permissions.Add(verb);
            }

            var updated = await userGroupService
                .UpdateAsync(group, Umbraco.Cms.Core.Constants.Security.SuperUserKey).ConfigureAwait(false);

            if (updated.Success)
            {
                logger.LogInformation(
                    "uBookIt granted its permission verbs to the user group '{Group}', which held "
                    + "the uBookIt section before the permissions model existed.", group.Name);
            }
            else
            {
                failed++;
                logger.LogError(
                    "uBookIt could not grant its permission verbs to the user group '{Group}': "
                    + "{Status}. The seed will retry at the next start.", group.Name, updated.Status);
            }
        }

        if (failed == 0)
        {
            await flags.SetAsync(FlagKey, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The selection rule, alone and static so tests exercise it arm by arm: the group
    /// holds the uBookIt section, and holds no uBookIt verb at all — the latter is what
    /// makes a retry idempotent and keeps the seed's hands off any group somebody edited.
    /// </summary>
    internal static bool ShouldSeed(
        IEnumerable<string> allowedSections, IEnumerable<string> permissions)
        => allowedSections.Contains(Backoffice.Constants.SectionAlias, StringComparer.OrdinalIgnoreCase)
            && !permissions.Any(p => p.StartsWith("UBookIt.", StringComparison.Ordinal));
}
