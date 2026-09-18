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

    [Fact]
    public void Every_catalogued_setting_has_a_label_and_a_description()
    {
        var source = RepoFiles.Read(LocalisationFile);

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
        var source = RepoFiles.Read(LocalisationFile);

        Assert.NotEmpty(SettingCatalogue.All);
        Assert.Matches(@"^\s*retentionDaysLabel:", source.Split('\n').First(l => l.Contains("retentionDaysLabel:")));
        Assert.DoesNotMatch(@"^\s*aTermNobodyDeclared:", source);
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
