using UBookIt.Core.Stores;

namespace UBookIt.Web.Rendering;

/// <summary>One thing a visitor may choose to book.</summary>
/// <param name="Subject">What it is, as the flow's URL carries it.</param>
/// <param name="Name">What the visitor sees.</param>
public sealed record CatalogueEntry(BookingSubject Subject, string Name);

/// <summary>
/// View model for the catalogue: the site's bookable things as one set of
/// choices.
/// <para>
/// The entries carry no kind for the view to render. A visitor books "a massage"
/// or "meeting room A"; which of those is a service is our concern, not theirs,
/// and a list that grouped or badged them would be answering a question nobody
/// asked.
/// </para>
/// </summary>
public sealed class CatalogueModel
{
    public required IReadOnlyList<CatalogueEntry> Entries { get; init; }

    public bool HasEntries => Entries.Count > 0;
}

/// <summary>
/// Composes what can be booked from the reads the rest of the front end already
/// uses: every service, plus every resource that permits direct booking.
/// <para>
/// There is deliberately <b>no</b> stored notion of "the bookable catalogue", no
/// ordering field and no visibility flag. Adding one would create a second answer
/// to a question already answered, and the first thing to go stale. A headless
/// consumer builds the same list from <c>GET /services</c> and
/// <c>GET /resources</c>, which is why this change adds no endpoint (design D6).
/// </para>
/// </summary>
public sealed class BookingCatalogue(IServiceStore serviceStore, IResourceStore resourceStore)
{
    /// <summary>
    /// How many of each kind the catalogue asks for. The read ports are paged and
    /// clamp to this, so a site with more bookable things than this would list a
    /// prefix of them. Stated as a constant rather than left implicit: it is a
    /// real bound, and a catalogue that silently truncated would read as complete.
    /// </summary>
    public const int PageSize = 500;

    public async Task<CatalogueModel> BuildAsync(CancellationToken cancellationToken = default)
    {
        var services = await serviceStore.ListAsync(0, PageSize, cancellationToken).ConfigureAwait(false);
        var resources = await resourceStore.ListAsync(0, PageSize, cancellationToken).ConfigureAwait(false);

        var entries = services.Items
            .Select(service => new CatalogueEntry(BookingSubject.Service(service.Id), service.Name))
            .Concat(resources.Items

                // A resource that withholds permission to be booked on its own
                // does not appear. Listing it would offer a choice that leads only
                // to the statement that it is not offered on its own.
                .Where(resource => resource.DirectlyBookable)
                .Select(resource => new CatalogueEntry(
                    BookingSubject.Resource(resource.Id), resource.DisplayName)))

            // One ordering across both kinds, so the two are genuinely presented
            // alike rather than merely rendered with the same markup in two
            // blocks. The token breaks ties so the order is deterministic when
            // two things share a name.
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => entry.Subject.Token, StringComparer.Ordinal)
            .ToList();

        return new CatalogueModel { Entries = entries };
    }
}
