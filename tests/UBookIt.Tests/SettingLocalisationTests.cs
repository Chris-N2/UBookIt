using System.Text.RegularExpressions;
using UBookIt.Backoffice.Settings;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Every catalogued setting has a name and a description the screen can render.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written because a setting shipped without them and the screen rendered its raw keys</b> —
/// `ubookitSettings_selfServiceCancellationEnabledLabel` in place of a label, and the same for the
/// description. Nothing failed: the catalogue was complete, the tier was right, the server refused
/// writes correctly, and every test passed. The defect lived entirely in the gap between a C#
/// catalogue and a TypeScript dictionary, which nothing joined.
/// </para>
/// <para>
/// So this guard crosses that gap, on the same terms as
/// <see cref="BookingReferenceAlphabetTests"/>: a C# test reading the client's source, because the
/// two halves are in different languages and only the test project sees both. The alternative —
/// noticing on screen — is what happened this time, and only because somebody looked.
/// </para>
/// </remarks>
public class SettingLocalisationTests
{
    private const string LocalisationFile = "src/UBookIt.Backoffice/Client/src/localization/en-us.ts";

    /// <summary>
    /// The client's <c>settingSlug</c>, ported: drop the <c>UBookIt</c> prefix, lower-case the
    /// first segment's initial, and join.
    /// </summary>
    private static string Slug(string key)
    {
        var parts = key.Split(':').Skip(1).ToArray();

        return string.Concat(parts.Select((part, index) =>
            index == 0 ? char.ToLowerInvariant(part[0]) + part[1..] : part));
    }

    /// <summary>
    /// The body of one top-level dictionary in the localisation file.
    /// </summary>
    /// <remarks>
    /// <b>The block matters, and the first version of this guard ignored it.</b> The screen asks
    /// for <c>ubookitSettings_&lt;slug&gt;Label</c>, so a term declared in a different dictionary
    /// satisfies a whole-file scan and still renders a raw key on screen — which is the very defect
    /// this file exists to prevent, wearing a different hat. Narrowing to the block is what makes
    /// the assertion mean what it says.
    /// </remarks>
    private static string Block(string source, string name)
    {
        var start = source.IndexOf($"{name}: {{", StringComparison.Ordinal);
        Assert.True(start >= 0, $"The localisation file no longer declares a '{name}' dictionary.");

        var depth = 0;

        for (var i = source.IndexOf('{', start); i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}' && --depth == 0)
            {
                return source[start..i];
            }
        }

        throw new InvalidOperationException($"The '{name}' dictionary is unbalanced.");
    }

    [Fact]
    public void Every_catalogued_setting_has_a_label_and_a_description()
    {
        // Scoped to the dictionary the screen actually reads from.
        var source = Block(RepoFiles.Read(LocalisationFile), "ubookitSettings");

        var missing = new List<string>();

        foreach (var descriptor in SettingCatalogue.All)
        {
            var slug = Slug(descriptor.Key);

            foreach (var suffix in new[] { "Label", "Description" })
            {
                // Matched as a declaration, not merely as text: the term has to be a key in the
                // dictionary, and a name appearing only inside somebody's prose would otherwise
                // satisfy this.
                if (!Regex.IsMatch(source, $@"^\s*{Regex.Escape(slug + suffix)}:", RegexOptions.Multiline))
                {
                    missing.Add(slug + suffix);
                }
            }
        }

        Assert.True(
            missing.Count == 0,
            "Catalogued but not localised, so the screen renders the raw key: " + string.Join(", ", missing));
    }

    [Fact]
    public void The_scan_can_tell_a_present_term_from_an_absent_one()
    {
        // POSITIVE CONTROL, and it comes first in spirit: a scan that matched nothing anywhere
        // would report every setting as localised. This project has shipped exactly that.
        var whole = RepoFiles.Read(LocalisationFile);
        var block = Block(whole, "ubookitSettings");

        Assert.NotEmpty(SettingCatalogue.All);
        Assert.Contains("retentionDaysLabel:", block, StringComparison.Ordinal);
        Assert.DoesNotContain("aTermNobodyDeclared:", block, StringComparison.Ordinal);

        // AND THE BLOCK IS A REAL NARROWING, not the whole file under another name. A term in a
        // neighbouring dictionary must NOT satisfy the scan — that is the hole the first version
        // had, and this is what holds it shut.
        Assert.Contains("unnamedParty:", whole, StringComparison.Ordinal);
        Assert.DoesNotContain("unnamedParty:", block, StringComparison.Ordinal);
    }

    [Fact]
    public void The_slug_matches_the_clients_own_rule()
    {
        // The port above is the drift risk, so it is pinned against the shape the client produces
        // for keys that already work on screen.
        Assert.Equal("retentionDays", Slug("UBookIt:RetentionDays"));
        Assert.Equal("notificationsSendBookerEmails", Slug("UBookIt:Notifications:SendBookerEmails"));
        Assert.Equal("deliveryApiEnableReads", Slug("UBookIt:DeliveryApi:EnableReads"));
        Assert.Equal("selfServiceCancellationEnabled", Slug("UBookIt:SelfServiceCancellation:Enabled"));
    }
}
