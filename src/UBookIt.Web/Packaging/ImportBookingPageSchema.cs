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
    {
    }

    protected override Task MigrateAsync()
    {
        ImportPackage.FromEmbeddedResource(GetType()).Do();
        return Task.CompletedTask;
    }
}
