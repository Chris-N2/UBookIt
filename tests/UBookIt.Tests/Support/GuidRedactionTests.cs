namespace UBookIt.Tests.Support;

/// <summary>
/// The redaction the PII guards stand on. The pinned id is QA's deterministic
/// reproduction of the original defect: "ada" inside a random GUID, which made three
/// guards fail on ~2% of full-suite runs and get misdiagnosed as environmental.
/// </summary>
public class GuidRedactionTests
{
    [Fact]
    public void A_guid_containing_a_name_shaped_run_is_removed()
    {
        var haystack = GuidRedaction.WithoutGuids(
            "{\"bookingId\":\"20faadab-d4e1-4118-bc8c-d16111111111\"}");

        Assert.DoesNotContain("ada", haystack, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{guid}", haystack, StringComparison.Ordinal);
    }

    [Fact]
    public void The_undashed_form_a_log_might_render_is_also_removed()
        => Assert.DoesNotContain(
            "ada",
            GuidRedaction.WithoutGuids("id 20faadabd4e14118bc8cd16111111111 failed"),
            StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Personal_data_is_not_guid_shaped_and_survives_to_be_caught()
    {
        // The safety condition: the redaction must not eat what the guards look for.
        var haystack = GuidRedaction.WithoutGuids("Ada Lovelace <ada@example.com> 07700 900123");

        Assert.Contains("Ada Lovelace", haystack, StringComparison.Ordinal);
        Assert.Contains("ada@example.com", haystack, StringComparison.Ordinal);
        Assert.Contains("07700", haystack, StringComparison.Ordinal);
    }
}
