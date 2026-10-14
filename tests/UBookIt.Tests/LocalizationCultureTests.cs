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
/// it, and within one culture the dictionary registered LAST wins. Umbraco orders localizations
/// by weight twice: the extension registry by <c>weight || 0</c>, then the localization registry
/// by <c>weight ?? 100</c>, keeping ties in order. Without a weight of its own, whether uBookIt or a
/// site registers last is decided by the order the extensions arrived in, which nobody controls
/// (QA rounds 1-2). Above Umbraco's default, uBookIt sorts first under both orderings, so a site's
/// default-weight dictionary always overrides it.
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
            + "registered in a culture wins, so at or below the default a site's own wording in "
            + "the same culture is no longer guaranteed to override uBookIt's: below it, uBookIt's "
            + "wins; at it, the outcome depends on the site's own weight and on the order the "
            + "extensions arrived in.");
    }

    private static string LocalizationEntry(string manifest)
    {
        var entries = Regex.Matches(manifest, @"\btype:\s*""localization""");

        Assert.True(
            entries.Count == 1,
            $"Expected exactly one localization extension in {ManifestFile}; found {entries.Count}.");

        // The entry is the object containing that `type:`: from the `{` that opens it (so a field
        // written before `type:` is still seen) to the `type:` of the next extension, or the end
        // of the file.
        var start = manifest.LastIndexOf('{', entries[0].Index);
        var rest = manifest[(entries[0].Index + entries[0].Length)..];
        var next = Regex.Match(rest, @"\btype:\s*""");
        var end = next.Success ? entries[0].Index + entries[0].Length + next.Index : manifest.Length;

        // Comments are removed before any field is read. The comment above this entry discusses
        // `culture:` and `weight:` by name, and a field read out of a comment would let a wrong or
        // missing real field pass (QA round 2: both guards went green with the real culture set
        // to `en-us`, or the real weight deleted, because a comment line still matched).
        var entry = manifest[start..end];
        entry = Regex.Replace(entry, @"/\*.*?\*/", "", RegexOptions.Singleline);
        entry = Regex.Replace(entry, @"//[^\r\n]*", "");

        return entry;
    }

    private static string Field(string entry, string name, string valuePattern)
    {
        // A field counts only as the first thing on its line, so text that merely mentions it
        // (a string, or anything a comment filter missed) cannot satisfy it.
        var field = Regex.Match(entry, $@"^\s*{name}:\s*{valuePattern}", RegexOptions.Multiline);

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
