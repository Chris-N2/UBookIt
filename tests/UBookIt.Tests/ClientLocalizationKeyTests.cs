using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Every localization key the client manifest references resolves to an entry in
/// <c>en-us.ts</c> — the class the permissions live check caught one instance of: the
/// entity-type permission-group heading rendered as the RAW KEY
/// <c>user_permissionsEntityGroup_ubookit</c> in the group editor, because Umbraco
/// derives that key per entity type and nothing had supplied it. A key that fails to
/// resolve does not error anywhere; it renders as itself, in production, to an
/// administrator.
/// </summary>
/// <remarks>
/// The resolution rule is Umbraco's: <c>#section_key</c> (or a derived
/// <c>section_key</c>) means section <c>section</c>, entry <c>key</c>, where the section
/// is the first underscore-delimited segment. The check is against the source of
/// <c>en-us.ts</c> as text — a section header <c>section: {</c> must appear, and the
/// entry <c>key:</c> must appear after it and before the next section header. Coarse,
/// but in the safe direction: it can false-FAIL on an exotic layout (loudly, fixably),
/// never false-pass on a missing entry.
/// </remarks>
public class ClientLocalizationKeyTests
{
    [Fact]
    public void Every_manifest_localization_key_resolves()
    {
        var manifest = RepoFiles.Read("src/UBookIt.Backoffice/Client/src/section/manifest.ts");
        var localization = RepoFiles.Read("src/UBookIt.Backoffice/Client/src/localization/en-us.ts");

        // Every "#section_key" the manifest hands to the localization system, PLUS the
        // derived key the group editor builds for our permission entity type — which no
        // manifest names, which is exactly how it went unnoticed.
        var keys = Regex.Matches(manifest, "\"#([A-Za-z0-9]+)_([A-Za-z0-9]+)\"")
            .Select(m => (Section: m.Groups[1].Value, Key: m.Groups[2].Value))
            .Distinct()
            .ToList();
        keys.Add((Section: "user", Key: "permissionsEntityGroup_ubookit"));

        Assert.NotEmpty(keys);

        var offenders = keys
            .Where(k => !Resolves(localization, k.Section, k.Key))
            .Select(k => $"#{k.Section}_{k.Key}")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "These localization keys do not resolve in en-us.ts and would render AS RAW "
            + "KEYS in the backoffice:\n  " + string.Join("\n  ", offenders));
    }

    private static bool Resolves(string localization, string section, string key)
    {
        var sectionStart = Regex.Match(localization, $@"^\s*{Regex.Escape(section)}:\s*{{", RegexOptions.Multiline);

        if (!sectionStart.Success)
        {
            return false;
        }

        // The section runs to the next top-level section header (two-space indent in
        // this file) or the end. The entry must sit inside it.
        var rest = localization[(sectionStart.Index + sectionStart.Length)..];
        var nextSection = Regex.Match(rest, @"^  [A-Za-z0-9]+:\s*{", RegexOptions.Multiline);
        var body = nextSection.Success ? rest[..nextSection.Index] : rest;

        return Regex.IsMatch(body, $@"^\s*{Regex.Escape(key)}:", RegexOptions.Multiline);
    }
}
