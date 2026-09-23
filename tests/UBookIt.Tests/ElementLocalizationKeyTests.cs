using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Every term a section ELEMENT asks for at runtime resolves to an entry in
/// <c>en-us.ts</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gap this closes.</b> <see cref="ClientLocalizationKeyTests"/> covers the keys the
/// MANIFEST names, and <see cref="SettingLocalisationTests"/> covers the keys derived from the
/// C# settings catalogue. Neither sees a key an element asks for through its own
/// <c>#term(...)</c> helper — which is most of the text on every screen. A missing entry
/// there errors nowhere: it renders as the raw key, in production, to whoever opened the
/// screen. That has already happened twice in this package, once from the manifest side and
/// once from the settings catalogue, and each time a guard was written for that one route.
/// </para>
/// <para>
/// This one is written for the class rather than the instance: it derives the section prefix
/// from each element's own <c>#term</c> definition, so an element added later is covered
/// without anybody remembering to extend a list.
/// </para>
/// <para>
/// Coarse in the safe direction, like the manifest guard it borrows its resolution from: it
/// can false-FAIL on an exotic layout, loudly and fixably, and cannot false-pass on a missing
/// entry. Keys built by interpolation are skipped and counted, because a dynamic key cannot
/// be resolved from source — <c>dayMonday</c> and friends are covered by the manifest guard's
/// own literals where they matter.
/// </para>
/// </remarks>
public class ElementLocalizationKeyTests
{
    private const string LocalisationFile = "src/UBookIt.Backoffice/Client/src/localization/en-us.ts";

    private const string SectionDirectory = "src/UBookIt.Backoffice/Client/src/section";

    [Fact]
    public void Every_term_an_element_asks_for_resolves()
    {
        var localization = RepoFiles.Read(LocalisationFile);
        var offenders = new List<string>();
        var checkedKeys = 0;

        foreach (var file in RepoFiles.Paths(SectionDirectory, "*.element.ts"))
        {
            var source = File.ReadAllText(file);

            // The element's own prefix, from the helper every one of them defines:
            //   return this.localize.term(`ubookitClosures_${key}`);
            var prefix = Regex.Match(source, @"this\.localize\.term\(`([A-Za-z0-9]+)_\$\{key\}`\)");
            if (!prefix.Success)
            {
                continue;
            }

            var section = prefix.Groups[1].Value;

            foreach (Match call in Regex.Matches(source, @"#term\(""([A-Za-z0-9]+)""\)"))
            {
                var key = call.Groups[1].Value;
                checkedKeys++;

                if (!Resolves(localization, section, key))
                {
                    offenders.Add($"{Path.GetFileName(file)}: {section}_{key}");
                }
            }

            // A term taking PARAMETERS cannot go through the `#term` helper, so it is written
            // as a direct `localize.term("section_key", …)` call — which the pattern above
            // cannot see. Found while adding the first such term to this package: the guard
            // reported green over a key it had never looked at. Both shapes render a raw key
            // when the entry is missing, so both are scanned.
            //
            // `\s*` after the paren is load-bearing: a parameterised call is long enough to wrap,
            // and the first version of this pattern required the quote immediately after the
            // bracket. It matched nothing, reported green, and was caught only by deleting the
            // term it was written for and watching the guard stay silent.
            foreach (Match call in Regex.Matches(source, @"localize\.term\(\s*""([A-Za-z0-9]+)_([A-Za-z0-9]+)"""))
            {
                checkedKeys++;

                if (!Resolves(localization, call.Groups[1].Value, call.Groups[2].Value))
                {
                    offenders.Add($"{Path.GetFileName(file)}: {call.Groups[1].Value}_{call.Groups[2].Value}");
                }
            }
        }

        // The guard's own precondition. A refactor that renamed the helper, or moved the
        // elements, would otherwise leave this passing while checking nothing at all.
        Assert.True(checkedKeys > 50, $"Only {checkedKeys} element terms were checked — the scan has stopped finding them.");

        Assert.True(
            offenders.Count == 0,
            "These terms are asked for by an element and do not resolve in en-us.ts, so they "
            + "would render AS RAW KEYS in the backoffice:\n  " + string.Join("\n  ", offenders));
    }

    private static bool Resolves(string localization, string section, string key)
    {
        var sectionStart = Regex.Match(localization, $@"^\s*{Regex.Escape(section)}:\s*{{", RegexOptions.Multiline);

        if (!sectionStart.Success)
        {
            return false;
        }

        var rest = localization[(sectionStart.Index + sectionStart.Length)..];
        var nextSection = Regex.Match(rest, @"^  [A-Za-z0-9]+:\s*{", RegexOptions.Multiline);
        var body = nextSection.Success ? rest[..nextSection.Index] : rest;

        return Regex.IsMatch(body, $@"^\s*{Regex.Escape(key)}:", RegexOptions.Multiline);
    }
}
