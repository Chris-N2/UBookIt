using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The backoffice's refusal messages say where a permission is granted, and they say it truly.
/// </summary>
/// <remarks>
/// <para>
/// The <c>site-settings</c> and <c>site-closures</c> specs require a refused user to be told
/// "where it is granted". Three terms told them "Users → User Groups → Default permissions, by
/// ticking …". That was never true of the Umbraco 17 group editor: the permissions are switches
/// in a box headed "uBookIt". The docs were corrected in <c>docs-single-source</c>, and the terms
/// went on contradicting them, because the retired-claims sweep reads shipped markdown only and
/// these terms are shipped text in a TypeScript file.
/// </para>
/// <para>
/// So the claims retired about that screen are held out of the localization files as well, and
/// each refusal is pinned to name the box. The negative half alone would pass over a term that
/// had been emptied, or reworded to say nothing about where.
/// </para>
/// </remarks>
public class PermissionGrantWordingTests
{
    private const string LocalizationDirectory = "src/UBookIt.Backoffice/Client/src/localization";

    /// <summary>The terms that refuse somebody a uBookIt permission, by section and key.</summary>
    public static TheoryData<string, string> RefusalTerms() => new()
    {
        { "ubookitSettings", "notPermitted" },
        { "ubookitClosures", "notPermittedWrite" },
        { "ubookitClosures", "notPermittedRead" },
    };

    [Fact]
    public void No_backoffice_term_repeats_a_retired_claim_about_the_user_group_editor()
    {
        var files = RepoFiles.Paths(LocalizationDirectory, "*.ts");

        // Anti-vacuity: either list empty makes the loop below prove nothing.
        Assert.NotEmpty(files);
        Assert.NotEmpty(RetiredClaims.AboutTheUserGroupEditor);

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var name = Path.GetRelativePath(RepoFiles.Root, file);

            foreach (var claim in RetiredClaims.AboutTheUserGroupEditor)
            {
                DocumentationAssert.DoesNotSay(text, claim.Needle, name);
            }
        }
    }

    [Theory]
    [MemberData(nameof(RefusalTerms))]
    public void Every_refusal_names_the_box_the_grant_is_in(string section, string key)
    {
        var term = TermText(RepoFiles.Read($"{LocalizationDirectory}/en-us.ts"), section, key);

        DocumentationAssert.Says(term, "Users → User Groups");
        DocumentationAssert.Says(term, "in the box headed “uBookIt”, switch on");
    }

    /// <summary>
    /// The string value of <paramref name="section"/>.<paramref name="key"/>, which this file
    /// writes either on the key's line or on the line after it.
    /// </summary>
    private static string TermText(string localization, string section, string key)
    {
        var sectionStart = Regex.Match(
            localization, $@"^  {Regex.Escape(section)}:\s*{{", RegexOptions.Multiline);
        Assert.True(sectionStart.Success, $"en-us.ts has no section \"{section}\".");

        var rest = localization[(sectionStart.Index + sectionStart.Length)..];
        var nextSection = Regex.Match(rest, @"^  [A-Za-z0-9]+:\s*{", RegexOptions.Multiline);
        var body = nextSection.Success ? rest[..nextSection.Index] : rest;

        var entry = Regex.Match(
            body, $@"^\s*{Regex.Escape(key)}:\s*""(?<text>[^""]*)""", RegexOptions.Multiline);
        Assert.True(entry.Success, $"en-us.ts has no term {section}.{key}, or it is not a plain string.");

        return entry.Groups["text"].Value;
    }
}
