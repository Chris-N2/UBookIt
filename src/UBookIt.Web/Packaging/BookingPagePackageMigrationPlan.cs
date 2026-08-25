using Umbraco.Cms.Infrastructure.Packaging;

namespace UBookIt.Web.Packaging;

/// <summary>
/// Installs uBookIt's schema — the Booking Page document type and its template —
/// into a site, from the <c>package.xml</c> embedded beside this class.
/// </summary>
/// <remarks>
/// <para>
/// The manifest is resolved as an embedded resource named for <b>this type's
/// namespace</b>: <c>UBookIt.Web.Packaging.package.xml</c>. Move either the class or
/// the file, rename the namespace, or drop the <c>&lt;EmbeddedResource&gt;</c> entry
/// from the project file, and the plan finds nothing — <b>with no error</b>. The site
/// starts, reports success, and simply has no schema. <c>PackagingTests</c> asserts
/// the coupling for that reason.
/// </para>
/// <para>
/// The plan's final state is a <b>hash of the manifest</b>, so it re-runs whenever
/// that file changes. Everything the manifest declares is then re-imported, which is
/// why nothing in it may carry anything a site would mind losing — see the comment at
/// the top of <c>package.xml</c> for the measured behaviour.
/// </para>
/// <para>
/// <see cref="AutomaticPackageMigrationPlan"/> rather than a custom
/// <c>PackageMigrationPlan</c> deliberately. The custom route offers finer control and
/// defaults <c>IgnoreCurrentState</c> to <c>true</c>, re-executing every migration —
/// so here the "more control" option is the more dangerous one, and the simple one is
/// also the one whose behaviour has been measured.
/// </para>
/// </remarks>
public sealed class BookingPagePackageMigrationPlan : AutomaticPackageMigrationPlan
{
    /// <summary>
    /// Named for the product, which is what an editor looks for under installed
    /// packages — not <c>UBookIt.Backoffice</c>, which is the id of the backoffice
    /// <i>extension</i> manifest and a different concern from what a site has
    /// installed. The base class exposes it as <c>PackageName</c>.
    /// </summary>
    public BookingPagePackageMigrationPlan()
        : base("uBookIt")
    {
    }
}
