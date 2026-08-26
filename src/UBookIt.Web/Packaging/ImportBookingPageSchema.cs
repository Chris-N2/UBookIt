using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Packaging;

namespace UBookIt.Web.Packaging;

/// <summary>
/// Imports <c>package.xml</c> — the Booking Page document type and its template.
/// <para>
/// The manifest is resolved from <b>this type's</b> namespace, so it must stay
/// alongside this class.
/// </para>
/// </summary>
public sealed class ImportBookingPageSchema : AsyncPackageMigrationBase
{
    private readonly PackageMigrationSettings _packageMigrationSettings;

    public ImportBookingPageSchema(
        IPackagingService packagingService,
        IMediaService mediaService,
        MediaFileManager mediaFileManager,
        MediaUrlGeneratorCollection mediaUrlGenerators,
        IShortStringHelper shortStringHelper,
        IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
        IMigrationContext context,
        IOptions<PackageMigrationSettings> packageMigrationSettings)
        : base(
            packagingService,
            mediaService,
            mediaFileManager,
            mediaUrlGenerators,
            shortStringHelper,
            contentTypeBaseServiceProvider,
            context,
            packageMigrationSettings)
        => _packageMigrationSettings = packageMigrationSettings.Value;

    protected override Task MigrateAsync()
    {
        // This migration runs ONCE. If the import is skipped now, it is skipped
        // forever: Umbraco records the migration as complete either way, and nothing
        // re-runs it — so the site is left with uBookIt installed and no schema, and
        // turning the setting back on does not help.
        //
        // Umbraco logs its own skip at INFO, which is not enough. Whoever set this
        // flag is typically configuring a platform, not installing uBookIt, and will
        // not connect an INFO line to a booking page that never appears. Warn, name
        // the package, and say what to do — this is the only moment the problem is
        // observable.
        if (_packageMigrationSettings.RunSchemaAndContentMigrations is false)
        {
            Logger.LogWarning(
                "uBookIt's schema was NOT installed because "
                + "Umbraco:CMS:PackageMigration:RunSchemaAndContentMigrations is false. "
                + "This migration runs once, so it will not retry: the Booking Page "
                + "document type and template will never be created, and re-enabling "
                + "the setting will not install them. To recover, delete the "
                + "'Umbraco.Core.Upgrader.State+uBookIt' row from umbracoKeyValue and "
                + "restart with the setting enabled.");
        }

        ImportPackage.FromEmbeddedResource(GetType()).Do();
        return Task.CompletedTask;
    }
}
