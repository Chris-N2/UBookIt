using System.Text.Json.Serialization;
using UBookIt.Core.Availability;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace UBookIt.TestSite;

/// <summary>
/// A public holiday source reading the UK government's bank-holiday feed.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is site code, not package code.</b> It lives in the dev site because uBookIt ships no
/// holiday data for any country and will not: shipping dates means maintaining them for every
/// jurisdiction, every substitution rule, forever, and being silently wrong the year a government
/// moves one. What the package publishes is the seam; this is what one site does with it.
/// </para>
/// <para>
/// <b>It is also the seam's only proof.</b> An interface nobody has implemented is a guess about
/// what a real source needs, and this is the case the port was designed against — a feed with a
/// title, a date, and a <c>notes</c> field that is how the UK expresses a substituted bank
/// holiday. If the port could not express that without the package learning what a substitution
/// is, the port would be wrong.
/// </para>
/// <para>
/// <b>The division is this implementation's business.</b> uBookIt's port takes no region, because
/// which jurisdiction a site wants is a property of the source it registered rather than something
/// the package could validate or default. This one reads England and Wales; a Scottish site
/// changes one constant, and a site with offices in both merges two divisions here and lets the
/// import collapse any date they share.
/// </para>
/// </remarks>
public sealed class GovUkBankHolidaySource(IHttpClientFactory httpClientFactory) : IPublicHolidaySource
{
    /// <summary>The published feed. Open data: no key, no authentication, no personal data.</summary>
    private const string FeedUrl = "https://www.gov.uk/bank-holidays.json";

    /// <summary>
    /// Which of the feed's three divisions this site follows. A Scottish site says
    /// <c>scotland</c>; the package neither knows nor cares.
    /// </summary>
    private const string Division = "england-and-wales";

    public async Task<IReadOnlyList<PublicHoliday>> GetAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(nameof(GovUkBankHolidaySource));

        // The token goes through to the network call. uBookIt hands it over so a slow feed can be
        // abandoned rather than holding an operator's screen, and honouring it is this
        // implementation's side of that contract.
        var feed = await client.GetFromJsonAsync<Dictionary<string, GovUkDivision>>(
            FeedUrl, cancellationToken).ConfigureAwait(false);

        if (feed is null || !feed.TryGetValue(Division, out var division))
        {
            // Thrown rather than returned as an empty list, deliberately: uBookIt reports a
            // source that FAILED differently from a window with no holidays in it, and an empty
            // list here would tell an operator their calendar is clear when the feed is broken.
            throw new InvalidOperationException(
                $"The bank-holiday feed carried no '{Division}' division.");
        }

        return division.Events
            .Where(published => DateOnly.TryParse(published.Date, out _))
            .Select(published => new PublicHoliday(DateOnly.Parse(published.Date), NameOf(published)))
            .Where(holiday => holiday.Date >= from && holiday.Date <= to)
            .ToList();
    }

    /// <summary>
    /// What to call the day, including the substitution the UK expresses in a separate field.
    /// </summary>
    /// <remarks>
    /// <b>All of the substitution knowledge in this package's world lives in this method.</b> When
    /// Christmas Day falls at a weekend the UK observes it on the next working day, and the feed
    /// says so by repeating the title with <c>notes: "Substitute day"</c> — two entries called
    /// "Christmas Day" on different dates in different years. Folding the note into the name is
    /// what makes an operator's list readable, and uBookIt sees only a date and a name: it has no
    /// concept of a substitution, and does not need one.
    /// </remarks>
    private static string NameOf(GovUkEvent published)
        => string.IsNullOrWhiteSpace(published.Notes)
            ? published.Title
            : $"{published.Title} ({published.Notes.ToLowerInvariant()})";

    private sealed class GovUkDivision
    {
        [JsonPropertyName("division")]
        public string Division { get; set; } = string.Empty;

        [JsonPropertyName("events")]
        public List<GovUkEvent> Events { get; set; } = [];
    }

    private sealed class GovUkEvent
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;
    }
}

/// <summary>
/// Registers the holiday source — which is the entire act of turning the import on.
/// </summary>
/// <remarks>
/// There is no setting. uBookIt resolves <see cref="IPublicHolidaySource"/> optionally: a site
/// that registers one gets the feature, and a site that does not gets no trace of it — no control,
/// no explanation, and endpoints that refuse. Deleting this composer is how this site would turn
/// the import off, and is exactly what the live check for absence does.
/// </remarks>
public sealed class GovUkBankHolidayComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddHttpClient(nameof(GovUkBankHolidaySource), client =>
        {
            // A courtesy to a public service, and a backstop for this site: uBookIt sets no
            // timeout of its own because a timeout is a policy, and the policy belongs to
            // whoever owns the call.
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("uBookIt-TestSite/1.0");
        });

        builder.Services.AddScoped<IPublicHolidaySource, GovUkBankHolidaySource>();
    }
}
