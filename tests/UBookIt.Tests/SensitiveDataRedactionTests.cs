using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Mapping;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Bookings;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// That booker contact details reach only a caller permitted to see them.
/// <para>
/// The endpoint's two callers are exercised in <see cref="BookingsEndpointTests"/>, where the
/// controller and its security accessor already live. What is here is the part that is not
/// about one request: the mapping decision itself, and the two guards whose job is to fail
/// when somebody later adds something nobody decided about.
/// </para>
/// </summary>
public class SensitiveDataRedactionTests
{
    private static readonly Guid RoomId = Guid.NewGuid();
    private static readonly Guid TherapistId = Guid.NewGuid();
    private static readonly Guid ServiceId = Guid.NewGuid();

    /// <summary>
    /// A summary carrying <b>everything a row can carry</b>, deliberately.
    /// </summary>
    /// <remarks>
    /// It previously passed an empty resource collection and a null service, which made
    /// <see cref="Everything_that_is_not_personal_data_survives_withholding"/> compare zero to
    /// zero and never compare the service at all — a test written to prove withholding subtracts
    /// nothing but the booker, built so that it could not observe the subtraction. A mutation
    /// emitting no resources and no service when withholding passed all 1820 tests.
    /// <para>
    /// <b>Two resources rather than one, and a named service.</b> A single resource would pass a
    /// mutation returning only the first, and the service is what the delta's scenario names
    /// alongside resources — so both are populated, and both are compared by content.
    /// </para>
    /// </remarks>
    private static BookingSummary Summary()
        => new(
            Guid.NewGuid(),
            References.Any(),
            BookingInterval.Create(
                new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero),
                "Europe/London").Value,
            BookingStatus.Confirmed,
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            "Ada Lovelace",
            "ada@example.com",
            [new BookedResource(RoomId, "Meeting Room A"), new BookedResource(TherapistId, "MRC Therapist")],
            new ServiceAttribution(ServiceId, "Initial Consultation"));

    [Fact]
    public void The_mapper_carries_the_booker_when_it_is_shown()
    {
        var model = BookingModelMapper.ToModel(Summary(), BookerVisibility.Shown);

        Assert.NotNull(model.Booker);
        Assert.Equal("Ada Lovelace", model.Booker.Name);
        Assert.Equal("ada@example.com", model.Booker.Email);
    }

    [Fact]
    public void The_mapper_withholds_the_booker_when_it_is_not()
    {
        var model = BookingModelMapper.ToModel(Summary(), BookerVisibility.Withheld);

        Assert.Null(model.Booker);
    }

    [Fact]
    public void Everything_that_is_not_personal_data_survives_withholding()
    {
        // Withholding must subtract the booker and nothing else. A mapper that returned a
        // sparser row for an unprivileged caller would give them a screen that is not merely
        // missing names but missing the reference, the time and the status — and the failure
        // would look like withholding working.
        var summary = Summary();

        var shown = BookingModelMapper.ToModel(summary, BookerVisibility.Shown);
        var withheld = BookingModelMapper.ToModel(summary, BookerVisibility.Withheld);

        Assert.Equal(shown.BookingId, withheld.BookingId);
        Assert.Equal(shown.Reference, withheld.Reference);
        Assert.Equal(shown.StartUtc, withheld.StartUtc);
        Assert.Equal(shown.EndUtc, withheld.EndUtc);
        Assert.Equal(shown.TimeZoneId, withheld.TimeZoneId);
        Assert.Equal(shown.Status, withheld.Status);
        Assert.Equal(shown.CreatedUtc, withheld.CreatedUtc);

        // Compared by CONTENT, not by count. This said `Resources.Count == Resources.Count`
        // against a fixture with no resources — zero equals zero — so a mapper that dropped
        // every resource when withholding satisfied it, and did.
        Assert.Equal(
            shown.Resources.Select(resource => (resource.ResourceId, resource.DisplayName)),
            withheld.Resources.Select(resource => (resource.ResourceId, resource.DisplayName)));

        Assert.NotNull(withheld.Service);
        Assert.Equal(shown.Service!.ServiceId, withheld.Service.ServiceId);
        Assert.Equal(shown.Service.DisplayName, withheld.Service.DisplayName);

        // The fixture is load-bearing, so it is asserted rather than assumed: an empty one
        // satisfies every comparison above while observing none of them.
        Assert.NotEmpty(shown.Resources);
        Assert.NotNull(shown.Service);
    }

    [Fact]
    public void The_default_visibility_withholds()
    {
        // `default(BookerVisibility)` is reachable — a field, an array element, a struct member
        // — and the value it lands on decides whether personal data is disclosed. It is pinned
        // to the safe answer here so that reordering the enum for tidiness cannot quietly make
        // disclosure the default.
        Assert.Equal(BookerVisibility.Withheld, default(BookerVisibility));
    }

    /// <summary>
    /// The members of the models that carry a booking to the backoffice.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this detects is CHANGE, not personal data.</b> Nothing in a property's name or
    /// type makes it personal, and a guard claiming to recognise that would be checking a
    /// mechanism while appearing to promise a guarantee. What it buys is that a member cannot
    /// join these models silently: the recorded set must be edited, and whoever edits it is
    /// asked the question in the failure message.
    /// </para>
    /// <para>
    /// <b>The set, not a list of known fields.</b> Asserting that <c>Booker</c> is withheld
    /// passes unchanged when a fourth member arrives carrying a phone number — which is the
    /// only case worth catching, since the members that exist today are already covered by
    /// tests that exercise them.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(typeof(BookingModel),
        "BookingId,Reference,StartUtc,EndUtc,TimeZoneId,Status,CreatedUtc,Booker,Resources,Service")]
    [InlineData(typeof(BookerModel), "Name,Email")]
    public void The_booking_response_models_have_not_gained_a_member_nobody_decided_about(
        Type model, string expected)
    {
        var actual = string.Join(
            ',',
            model.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal));

        var recorded = string.Join(
            ',',
            expected.Split(',').OrderBy(name => name, StringComparer.Ordinal));

        Assert.True(
            actual == recorded,
            $"{model.Name}'s members have changed.\n"
            + $"  recorded: {recorded}\n"
            + $"  actual:   {actual}\n"
            + "Before updating the recorded set, answer this: is the difference personal data, "
            + "and who may see it? If it is, it belongs inside the withheld object rather than "
            + "beside it, and BookingModelMapper decides it — a member added outside that "
            + "object reaches every caller, including one with no sensitive-data access.");
    }

    [Fact]
    public void Only_recorded_files_can_compose_a_booking_row()
    {
        // The `sensitive-data` capability requires that composing a response carrying contact
        // details REQUIRES the visibility decision, and that there is no route which skips it.
        // The mapper's required argument guarantees that for the mapper — and `BookingModel` is
        // a public class with a settable `Booker`, so `new BookingModel { Booker = ... }`
        // elsewhere would be exactly the unguarded route the requirement forbids.
        //
        // That was checked once by hand while this change was applied. A one-time check is not
        // a guard, which is the argument this change itself makes about the field-addition
        // risk — so the second-composition-site risk gets a standing check too rather than
        // being left to whoever reads the diff next.
        //
        // Source-level because the property is an ABSENCE: no compiled artefact carries "there
        // is no other constructor call", and a test that exercised the one call site would
        // prove only that it maps correctly, never that it is alone.
        //
        // MATCHES THE TYPE, NOT THE CONSTRUCTION. The first version of this guard scanned for
        // the literal `new BookingModel`, which appears NOWHERE in src/ — the mapper composes
        // the row target-typed, as `=> new()`. Every file hit the `continue`, the offender list
        // was unconditionally empty, and the whitelist below was never evaluated once. A second
        // composition site written in the mapper's own idiom passed it. Naming the type is the
        // property composition cannot avoid: target-typed `new()` requires the enclosing member
        // to declare `BookingModel` as its return type, so a reference is unavoidable where any
        // particular spelling of `new` is not.
        //
        // Recording the SET rather than asserting an absence is what supplies a positive
        // control. An absence-assertion is satisfied by a scan that sees nothing, which is
        // exactly how the previous version certified a defect.
        var referencing = new List<string>();

        foreach (var path in RepoFiles.Paths("src", "*.cs"))
        {
            // COMMENTS STRIPPED FIRST, and this is the whole difference between a guard that
            // works and one that does not. `BookingsController` names the type twice in `///`
            // prose and nowhere in code, so a set built from the raw text records it as a
            // legitimate referencer — and then a composition site added to that very file, which
            // is exactly where one would be added, changes the set not at all. Measured: it
            // passed. Only code counts.
            var code = string.Join(
                '\n',
                File.ReadAllLines(path).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

            // Word-bounded, so neither `BookingModelMapper` nor `PagedBookingsModel` matches.
            if (Regex.IsMatch(code, @"\bBookingModel\b"))
            {
                referencing.Add(Path.GetFileName(path));
            }
        }

        string[] recorded = ["BookingModelMapper.cs", "BookingModels.cs"];

        // POSITIVE CONTROL, and it comes first. The version this replaces reported a clean bill
        // of health precisely because it matched nothing at all; a scan that has lost sight of
        // the one site that legitimately exists must fail rather than report an empty offender
        // list.
        Assert.Contains("BookingModelMapper.cs", referencing);

        Assert.True(
            referencing.Order(StringComparer.Ordinal).SequenceEqual(recorded.Order(StringComparer.Ordinal)),
            "The set of files referencing BookingModel has changed.\n"
            + $"  recorded: {string.Join(", ", recorded.Order(StringComparer.Ordinal))}\n"
            + $"  actual:   {string.Join(", ", referencing.Order(StringComparer.Ordinal))}\n"
            + "If a new file composes a booking row it must take the visibility decision — only "
            + "BookingModelMapper.ToModel requires it, and a row composed anywhere else reaches "
            + "every caller, including one with no sensitive-data access. Compose through the "
            + "mapper, then record the file here.");
    }

    [Fact]
    public void No_booking_endpoint_can_be_asked_about_a_booker()
    {
        // A caller who may not read an email but may filter by one can confirm an address by
        // watching whether a row comes back, and enumerate candidates the same way. Withholding
        // a value while answering questions about it is not withholding it.
        //
        // EVERY management controller, not just this one. The comment here used to claim the
        // guard would catch "a search endpoint added tomorrow" while scanning
        // `typeof(BookingsController)` alone — which is precisely the endpoint it would not see.
        // The section-access tests already enumerate the assembly; this does the same.
        //
        // AND the properties of complex parameters, not just parameter names. A query surface
        // grows by acquiring a filter object — `[FromQuery] BookerFilter? filter` binds
        // `?filter.BookerEmail=`, and a scan of parameter names sees only "filter". Measured:
        // that shape passed the previous version of this test.
        //
        // GET actions only, deliberately. The risk is an oracle — asking about a value you were
        // not given — which requires a read. A write that carries booker details is placement,
        // and forbidding "name" there would fail on every legitimate create endpoint.
        var identifiers = new List<string>();
        var getActions = 0;

        foreach (var controller in typeof(BookingsController).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract))
        {
            foreach (var method in controller.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.GetCustomAttributes<HttpGetAttribute>().Any() is false)
                {
                    continue;
                }

                getActions++;

                foreach (var parameter in method.GetParameters())
                {
                    identifiers.Add(parameter.Name ?? string.Empty);

                    // A complex bound type contributes its own property names to the query
                    // surface; a primitive, a Guid or a CancellationToken contributes nothing.
                    var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;

                    if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type.Namespace?.StartsWith("System", StringComparison.Ordinal) is true)
                    {
                        continue;
                    }

                    identifiers.AddRange(
                        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Select(property => property.Name));
                }
            }
        }

        // A scan over nothing passes every assertion made about it.
        Assert.NotEmpty(identifiers);
        Assert.True(getActions > 1, "Only one GET action was found; this guard is no longer scanning the assembly.");

        // Two rules, because they fail differently. A booker field is recognisable by name
        // wherever it appears, so "booker" and "email" match as substrings. The generic
        // search words match EXACTLY — `term`, `query`, `q` — because as substrings they hit
        // ordinary unrelated names (`queryMode`), and a guard that fires for the wrong reason
        // is read as noise and then relaxed. Dropping them entirely was my own over-correction:
        // measured, `[FromQuery] string? term` then passed.
        foreach (var identifier in identifiers)
        {
            Assert.False(
                identifier.Contains("booker", StringComparison.OrdinalIgnoreCase)
                || identifier.Contains("email", StringComparison.OrdinalIgnoreCase),
                $"A management GET exposes '{identifier}'. Answering questions about a booker's "
                + "contact details is not withholding them: a caller who may not read an email "
                + "but may filter by one can confirm it by watching whether a row comes back.");

            Assert.False(
                new[] { "name", "search", "searchterm", "term", "query", "q", "keyword" }
                    .Contains(identifier, StringComparer.OrdinalIgnoreCase),
                $"A management GET exposes '{identifier}', a free-text search surface over "
                + "bookings. If it cannot reach booker contact details, name it for what it "
                + "searches and record why here.");
        }
    }
}
