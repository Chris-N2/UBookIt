using System.Reflection;
using UBookIt.Core.Bookings;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// How a booking comes to name a service — and how it cannot.
/// <para>
/// The guarantee here is structural rather than behavioural, which is unusual and
/// deliberate. Recording the wrong service is not a wrong answer to a query that someone
/// notices; it is a wrong <b>fact</b>, stored permanently and indistinguishable afterwards
/// from a real attribution. Nothing downstream can detect it, so the only place to prevent
/// it is the shape of the API that writes it.
/// </para>
/// </summary>
public class ServiceAttributionTests
{
    private static BookingInterval Interval() => BookingInterval.Create(
        TestData.Utc(TestData.BaseDate, "09:00"),
        TestData.Utc(TestData.BaseDate, "10:00"),
        TestData.LondonZoneId).Value;

    [Fact]
    public void The_general_multi_claim_contract_cannot_name_a_service()
    {
        // If this ever fails, the failure is not "a test needs updating" — it is that any
        // caller can now attribute a booking to a service whose roles its resources do not
        // satisfy. The direct-placement overload already refuses to take a "this is a
        // direct booking" flag for exactly this reason, and this is that reasoning applied
        // to the other end of the same interface.
        var offenders = typeof(MultiClaimBookingRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property =>
                property.PropertyType == typeof(ServiceAttribution)
                || property.Name.Contains("Service", StringComparison.OrdinalIgnoreCase))
            .Select(property => property.Name)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "MultiClaimBookingRequest now carries a service, so a caller can assert an "
            + "attribution the placement did not make: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Naming_a_service_requires_the_entry_point_that_places_through_one()
    {
        // The other half: the capability exists, and it exists in exactly one place. A
        // structural absence test alone would still pass if the feature had been dropped.
        var entryPoint = typeof(IBookingService).GetMethod(nameof(IBookingService.PlaceForServiceAsync));

        Assert.NotNull(entryPoint);
        Assert.Equal(typeof(ServiceAttribution), entryPoint!.GetParameters()[0].ParameterType);

        // And no OTHER member of the contract accepts one, so this stays the single door.
        var others = typeof(IBookingService)
            .GetMethods()
            .Where(method => method.Name != nameof(IBookingService.PlaceForServiceAsync))
            .Where(method => method.GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(ServiceAttribution)))
            .Select(method => method.Name)
            .ToList();

        Assert.True(
            others.Count == 0,
            "A second way to name a service has appeared on IBookingService: "
            + string.Join(", ", others));
    }

    [Fact]
    public async Task Asking_for_a_service_booking_without_a_service_throws()
    {
        // Not "places an unattributed booking". A caller reaching this overload has asked
        // for a service booking; giving them an ordinary one that succeeds and looks right
        // is the quiet failure this whole change exists to prevent. The general overload
        // is what "no service" means, and it is one call away.
        var service = new BookingService(
            new InMemoryResourceStore(),
            new InMemoryBookingStore(),
            new FixedTimeProvider(TestData.Now),
            TestData.Settings);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.PlaceForServiceAsync(
            null!,
            new MultiClaimBookingRequest
            {
                ResourceIds = [Guid.NewGuid()],
                Start = TestData.Utc(TestData.BaseDate, "09:00"),
                Duration = TimeSpan.FromMinutes(60),
                Booker = TestData.Booker(),
            }));
    }

    [Fact]
    public void The_package_no_longer_claims_a_booking_forgets_its_service()
    {
        // A statement of a limit outlived the limit. `IBookingManagementStore` explained the
        // missing service filter by saying a booking does not record the service that
        // produced it — true when written, and false the moment this change landed. It was
        // published on the read port itself, so anyone inspecting the package would have
        // been told bookings forget their service by the package that records it.
        //
        // Neither my own falsified-sentence sweep nor two QA rounds caught it; the sync-time
        // sweep did. Asserted here so the next contradiction is a failing test rather than a
        // third pair of eyes.
        //
        // This IS a source grep, which the previous change deleted one of — and the
        // distinction is the point. That one asserted BEHAVIOUR through source text ("the
        // file must not contain the word Confirmed") and broke when a comment mentioned a
        // status name. This asserts PROSE, which is what the requirement is about: the spec
        // scenario says the package must *state* something. A prose guarantee is the one
        // case where reading the prose is the direct test rather than a proxy for one.
        // Scoped to the port's own comment, and with the source unwrapped first: the claim
        // is a sentence, and a sentence in this file is broken across ~90-character lines,
        // so a search over raw text only finds phrasings that happen not to wrap. The first
        // draft of this test had both faults — file-scoped and line-sensitive — and passed
        // for the wrong reason.
        var source = Support.RepoFiles.Read("src/UBookIt.Core/Stores/Stores.cs");

        // The forbidden claims are searched across the WHOLE file, unwrapped. Scoping them
        // to the interface's summary — which an earlier draft did — narrowed the guard
        // without saying so: the same false sentence on `ListAsync`'s own doc comment five
        // lines below, or in a `<remarks>` above the summary, passed silently. A negative
        // assertion has no false-positive cost here, so it has no reason to be scoped.
        var whole = Unwrapped(source);

        // The positive assertions ARE scoped, and that scoping is the point: file-wide they
        // would pass on the phrase appearing anywhere in a thousand-line file, which is the
        // fault that retired the previous `Contains("scope decision")`.
        var comment = Unwrapped(Between(
            source,
            from: "public interface IBookingManagementStore",
            back: "/// <summary>"));

        // Several phrasings of the one false claim, because the obligation is on the
        // MEANING and a single literal only forbids one way of saying it. Still not
        // exhaustive — no string test is — so this is a floor rather than a proof.
        foreach (var claim in new[]
                 {
                     "does not record the service",
                     "do not record the service",
                     "is not retained",
                     "are not retained",
                     "do not retain",
                     "does not retain",
                     "no record of which service",
                     "the service is discarded",
                     "the originating service",
                     "forgets the service",
                 })
        {
            Assert.DoesNotContain(claim, whole, StringComparison.OrdinalIgnoreCase);
        }

        // And the true statement is present: the filter is absent by choice, not because
        // the data is. Asserted on the substance rather than on a form of words — an
        // earlier draft pinned the exact phrase "scope decision", which fails on a correct
        // rewording and passes on the phrase appearing anywhere in a thousand-line file.
        Assert.Contains("no filter by service", comment, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("records the service it was placed for", comment, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Source with comment markers and line breaks removed, so a wrapped sentence reads as
    /// a sentence. A guard on prose that cannot see across a line break is a guard on line
    /// breaks.
    /// </summary>
    private static string Unwrapped(string source)
        => string.Join(' ', source
            .Split('\n')
            .Select(line => line.Trim().TrimStart('/').Trim()));

    /// <summary>
    /// The doc comment immediately above <paramref name="from"/>.
    /// </summary>
    /// <remarks>
    /// Fails loudly when either anchor is gone. A prose guard that quietly starts reading an
    /// empty string is worse than no guard, because it keeps passing.
    /// </remarks>
    private static string Between(string source, string from, string back)
    {
        var anchor = source.IndexOf(from, StringComparison.Ordinal);
        Assert.True(anchor >= 0, $"'{from}' is no longer in the file; this guard is looking at nothing.");

        var start = source.LastIndexOf(back, anchor, StringComparison.Ordinal);
        Assert.True(start >= 0, $"No doc comment found above '{from}'.");

        return source[start..anchor];
    }

    [Fact]
    public void A_booking_created_without_a_service_reports_none()
    {
        var booking = Booking.Rehydrate(
            Guid.NewGuid(),
            Interval(),
            TestData.Booker(),
            [new ResourceClaim(Guid.NewGuid())],
            BookingStatus.Confirmed,
            TestData.Now);

        Assert.Null(booking.Value.Service);
    }

    [Fact]
    public void Rehydration_accepts_a_service_it_cannot_verify()
    {
        // The stored attribution is historical fact, on the same terms as the stored
        // status. A service that no longer exists must not make an old booking unreadable
        // for having been successful — and rehydration has no store to check against
        // anyway, which is the point: it is a persistence boundary, not a validator.
        var attribution = new ServiceAttribution(Guid.NewGuid(), "A service since deleted");

        var result = Booking.Rehydrate(
            Guid.NewGuid(),
            Interval(),
            TestData.Booker(),
            [new ResourceClaim(Guid.NewGuid())],
            BookingStatus.Confirmed,
            TestData.Now,
            attribution);

        Assert.True(result.Succeeded);
        Assert.Equal(attribution, result.Value.Service);
    }
}
