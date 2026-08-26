using Umbraco.Cms.Core.Packaging;

namespace UBookIt.Web.Packaging;

/// <summary>
/// Installs uBookIt's schema — the Booking Page document type and its template —
/// into a site, from the <c>package.xml</c> embedded beside this plan.
/// </summary>
/// <remarks>
/// <para>
/// <b>A custom <see cref="PackageMigrationPlan"/>, deliberately, and NOT an
/// <c>AutomaticPackageMigrationPlan</c>.</b> The difference is the whole design.
/// </para>
/// <para>
/// An automatic plan's final state is a <b>hash of the manifest</b>, so it re-runs
/// every time that file changes for any reason — and the import overwrites a
/// template's contents wholesale. A site that edited the shipped template would lose
/// that work on any release that touched schema, including one that did not mention
/// templates. Measured against 17.6.2, so this is not a theoretical objection.
/// </para>
/// <para>
/// A custom plan's states are the explicit ids below. Once a site reaches the final
/// one the import never runs again, however the manifest changes. The shipped
/// template therefore becomes <b>the site's own file</b> after install, which is what
/// makes it safe to edit — and it is what the Clean starter kit relies on for the
/// same reason.
/// </para>
/// <para>
/// <b>Adding a step re-imports everything the manifest it names declares.</b> That is
/// the cost, and it is the right one: overwriting becomes a decision someone makes
/// rather than a side effect of editing a file.
/// </para>
/// <para>
/// A future step that must add schema <i>without</i> disturbing a site's template can
/// import a <b>separate, narrower manifest</b> declaring only the new schema — it is
/// the manifest a step names that gets re-imported, not this one by default. That is a
/// real option and it is why this is worth stating; but until such a step exists,
/// <c>docs/booking-page.md</c> deliberately tells site authors the conservative worst
/// case — that a release carrying a step replaces the template — because promising
/// them a narrower one commits every future maintainer to it.
/// </para>
/// </remarks>
public sealed class BookingPagePackageMigrationPlan : PackageMigrationPlan
{
    /// <summary>
    /// Named for the product, which is what an editor looks for under installed
    /// packages — not <c>UBookIt.Backoffice</c>, which is the id of the backoffice
    /// <i>extension</i> manifest and a different concern from what a site has
    /// installed.
    /// <para>
    /// <b>DO NOT RENAME THIS ONCE RELEASED.</b> Umbraco keys a plan's stored state on
    /// its name, so a rename makes every existing install look uninstalled and
    /// re-imports — silently replacing whatever the site had edited, with one INFO
    /// line and no error. Changing the state id below, or the plan's type, breaks
    /// existing installs too; those at least fail loudly.
    /// </para>
    /// </summary>
    public BookingPagePackageMigrationPlan()
        : base("uBookIt")
    {
    }

    protected override void DefinePlan()
        => To<ImportBookingPageSchema>(new Guid("3f7c9d21-5a48-4c6e-9b03-0d2a6f1e8c40"));
}
