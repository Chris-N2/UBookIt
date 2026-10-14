using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The client's localization extension is registered under Umbraco's own fallback culture, so
/// every backoffice user resolves uBookIt's terms whatever their language, and at a weight above
/// Umbraco's default, so a site's own wording in that culture overrides uBookIt's.
/// </summary>
/// <remarks>
/// <para>
/// Umbraco looks a term up in the user's regional culture, then the user's language, then its
/// fallback <c>UMB_DEFAULT_LOCALIZATION_CULTURE</c>. Until <c>backoffice-localization-fallback</c>
/// the terms were registered as <c>en-us</c>, which is none of those for anyone but a US English
/// user: a Spanish user, and a British English one, saw every uBookIt term as its RAW KEY. No
/// error is raised anywhere when that happens.
/// </para>
/// <para>
/// Sharing the fallback culture means sharing a dictionary with any wording a site registers in
/// it, and within one culture the dictionary registered LAST wins. Umbraco registers in
/// descending weight, so uBookIt's weight must be above Umbraco's default for a site's
/// default-weight dictionary to override it. At the default, uBookIt's terms overwrite the site's
/// (QA round 1).
/// </para>
/// <para>
/// Both values are compared against Umbraco's own source, read from the backoffice package the
/// client builds against, rather than literals — so an Umbraco that moved its fallback or its
/// default weight fails here instead of silently re-breaking. Both sides are read as text: the
/// client has no DOM test environment, and Umbraco's localization manager touches
/// <c>document</c> when it loads. A missing file, constant or entry FAILS; nothing skips.
/// </para>
/// <para>
/// On <c>dev/v18</c> the client still builds against the 17.x backoffice package, so there this
/// proves the registration matches the package built against, not the Umbraco 18 code that runs.
/// </para>
/// </remarks>
public class LocalizationCultureTests
{
    private const string ManifestFile = "src/UBookIt.Backoffice/Client/src/section/manifest.ts";

    private const string UmbracoLocalizationManagerFile =
        "src/UBookIt.Backoffice/Client/node_modules/@umbraco-cms/backoffice/dist-cms/libs/localization-api/localization.manager.js";

    private const string UmbracoLocalizationRegistryFile =
        "src/UBookIt.Backoffice/Client/node_modules/@umbraco-cms/backoffice/dist-cms/packages/core/localization/registry/localization.registry.js";

    [Fact]
    public void The_localization_extension_is_registered_under_Umbracos_fallback_culture()
    {
        var registered = Field(LocalizationEntry(RepoFiles.Read(ManifestFile)), "culture", @"""([^""]+)""");
        var fallback = UmbracoFallbackCulture(RepoFiles.Read(UmbracoLocalizationManagerFile));

        Assert.True(
            string.Equals(registered, fallback, StringComparison.OrdinalIgnoreCase),
            $"The client registers its terms under culture '{registered}', but Umbraco's fallback "
            + $"culture (UMB_DEFAULT_LOCALIZATION_CULTURE) is '{fallback}'. Umbraco looks a term up "
            + "only in the user's region, the user's language and the fallback, so every user for "
            + "whom the registered culture is none of those would see uBookIt's terms as RAW KEYS.");
    }

    [Fact]
    public void The_localization_extension_weighs_more_than_Umbracos_default_so_a_site_overrides_it()
    {
        var weight = int.Parse(Field(LocalizationEntry(RepoFiles.Read(ManifestFile)), "weight", @"(\d+)"));
        var defaultWeight = UmbracoDefaultWeight(RepoFiles.Read(UmbracoLocalizationRegistryFile));

        Assert.True(
            weight > defaultWeight,
            $"The client's localization weight is {weight}, but Umbraco's default weight is "
            + $"{defaultWeight}. Umbraco registers in descending weight and the LAST dictionary "
            + "registered in a culture wins, so at or below the default uBookIt's terms would "
            + "overwrite a site's own wording in the same culture.");
    }

    private static string LocalizationEntry(string manifest)
    {
        var entries = Regex.Matches(manifest, @"\btype:\s*""localization""");

        Assert.True(
            entries.Count == 1,
            $"Expected exactly one localization extension in {ManifestFile}; found {entries.Count}.");

        // The entry is the object containing that `type:`: from the `{` that opens it to the
        // `type:` of the next extension, or the end of the file.
        var start = manifest.LastIndexOf('{', entries[0].Index);
        var rest = manifest[(entries[0].Index + entries[0].Length)..];
        var next = Regex.Match(rest, @"\btype:\s*""");
        var end = next.Success ? entries[0].Index + entries[0].Length + next.Index : manifest.Length;

        return manifest[start..end];
    }

    private static string Field(string entry, string name, string valuePattern)
    {
        var field = Regex.Match(entry, $@"\b{name}:\s*{valuePattern}");

        Assert.True(field.Success, $"The localization extension in {ManifestFile} declares no {name}.");

        return field.Groups[1].Value;
    }

    private static string UmbracoFallbackCulture(string localizationManager)
    {
        var constant = Regex.Match(
            localizationManager,
            @"\bUMB_DEFAULT_LOCALIZATION_CULTURE\s*=\s*['""]([^'""]+)['""]");

        Assert.True(
            constant.Success,
            $"UMB_DEFAULT_LOCALIZATION_CULTURE is not assigned in {UmbracoLocalizationManagerFile}; "
            + "Umbraco has moved or renamed its fallback culture, and this guard must follow it.");

        return constant.Groups[1].Value;
    }

    private static int UmbracoDefaultWeight(string localizationRegistry)
    {
        var fallback = Regex.Match(localizationRegistry, @"\$weight:\s*extension\.weight\s*\?\?\s*(\d+)");

        Assert.True(
            fallback.Success,
            $"The default localization weight (`$weight: extension.weight ?? N`) is not found in "
            + $"{UmbracoLocalizationRegistryFile}; Umbraco has changed how it weighs localizations, "
            + "and this guard must follow it.");

        return int.Parse(fallback.Groups[1].Value);
    }
}
