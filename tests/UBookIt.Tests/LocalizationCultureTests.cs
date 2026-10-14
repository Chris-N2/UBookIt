using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The client's localization extension is registered under Umbraco's own fallback culture,
/// so every backoffice user resolves uBookIt's terms whatever their language.
/// </summary>
/// <remarks>
/// <para>
/// Umbraco loads, for the current user, only the localization extensions whose culture is the
/// user's regional culture, the user's language, or its fallback
/// <c>UMB_DEFAULT_LOCALIZATION_CULTURE</c>. Until <c>backoffice-localization-fallback</c> the
/// terms were registered as <c>en-us</c>, which is none of those for anyone but a US English
/// user: a Spanish user, and a British English one, saw every uBookIt term as its RAW KEY. No
/// error is raised anywhere when that happens.
/// </para>
/// <para>
/// The test compares against Umbraco's constant, read from the backoffice package the client
/// builds against, rather than the literal <c>"en"</c> — so an Umbraco that moved its fallback
/// fails here instead of silently re-breaking every other language. Both sides are read as text:
/// the client has no DOM test environment, and Umbraco's localization manager touches
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

    [Fact]
    public void The_localization_extension_is_registered_under_Umbracos_fallback_culture()
    {
        var registered = RegisteredCulture(RepoFiles.Read(ManifestFile));
        var fallback = UmbracoFallbackCulture(RepoFiles.Read(UmbracoLocalizationManagerFile));

        Assert.True(
            string.Equals(registered, fallback, StringComparison.OrdinalIgnoreCase),
            $"The client registers its terms under culture '{registered}', but Umbraco's fallback "
            + $"culture (UMB_DEFAULT_LOCALIZATION_CULTURE) is '{fallback}'. Every backoffice user "
            + "whose language is not exactly the registered culture would see uBookIt's terms as "
            + "RAW KEYS.");
    }

    private static string RegisteredCulture(string manifest)
    {
        var entries = Regex.Matches(manifest, @"\btype:\s*""localization""");

        Assert.True(
            entries.Count == 1,
            $"Expected exactly one localization extension in {ManifestFile}; found {entries.Count}.");

        // The entry runs to the next extension's `type:` or the end of the file.
        var rest = manifest[(entries[0].Index + entries[0].Length)..];
        var next = Regex.Match(rest, @"\btype:\s*""");
        var entry = next.Success ? rest[..next.Index] : rest;

        var culture = Regex.Match(entry, @"\bculture:\s*""([^""]+)""");

        Assert.True(culture.Success, $"The localization extension in {ManifestFile} declares no culture.");

        return culture.Groups[1].Value;
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
}
