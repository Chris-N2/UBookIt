using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
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

    /// <summary>
    /// Every route that composes a booking row states the visibility decision, and cannot
    /// decline to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This states the guarantee. <see cref="Only_recorded_files_can_compose_a_booking_row"/>
    /// states a proxy for it.</b> Three successive versions of that scan were blind — to the
    /// literal it matched, then to the construction idiom, then to comments — and each repair
    /// corrected the mechanism the previous one got wrong while leaving the requirement itself
    /// unobserved. What <c>sensitive-data</c> requires is that <i>no route composes a row
    /// without the decision</i>. A file is not a route, and the place a second route is likeliest
    /// to appear is inside the one file that scan whitelists.
    /// </para>
    /// <para>
    /// Measured: adding <c>ToModel(BookingSummary) =&gt; ToModel(summary, Shown)</c> to the
    /// mapper — a route composing a row carrying the booker's name and email, defaulting to
    /// disclosure — passed all 1820 tests.
    /// </para>
    /// <para>
    /// Reflection rather than source, because the property is about which members exist, and an
    /// assembly carries exactly that. A <i>defaulted</i> parameter is refused as well as a
    /// missing one: <c>BookerVisibility visibility = BookerVisibility.Shown</c> satisfies "takes
    /// the decision" while letting every caller omit it, which is the same disclosure by another
    /// spelling.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_route_that_composes_a_booking_row_requires_the_decision()
    {
        // EVERY type in the assembly, not just the mapper. Scoping this to
        // `typeof(BookingModelMapper)` would have left the same hole one file over: a static
        // factory on `BookingModel` itself lives in `BookingModels.cs`, which the file scan
        // already whitelists, so neither guard would have seen it. That is precisely the shape
        // of defect the file-versus-route distinction exists to close, and closing it in one
        // place while leaving it open in another is how this guard has failed three times.
        var routes = typeof(BookingModelMapper).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.ReturnType == typeof(BookingModel))
            // Compiler-generated members are excluded, and this was measured rather than
            // assumed: the controller's `Select(summary => ToModel(summary, visibility))`
            // compiles to a closure method returning a BookingModel whose visibility is
            // CAPTURED rather than passed, so the guard fired on the very call site that is
            // correct. Excluding them costs nothing — a lambda is not a route a caller can
            // reach, and the method it calls is checked on its own.
            .Where(method => method.GetCustomAttribute<CompilerGeneratedAttribute>() is null
                && method.Name.Contains('<', StringComparison.Ordinal) is false)
            .ToArray();

        // A scan over nothing passes every assertion made about it — and this guard exists
        // because three that found nothing each certified a defect.
        Assert.NotEmpty(routes);

        foreach (var route in routes)
        {
            var decision = route.GetParameters()
                .SingleOrDefault(parameter => parameter.ParameterType == typeof(BookerVisibility));

            Assert.True(
                decision is not null,
                $"{route.DeclaringType?.Name}.{route.Name} returns a BookingModel without taking a "
                + "BookerVisibility. Every route composing a row that carries booker contact "
                + "details must state whether they may be seen; one that does not reaches every "
                + "caller, including one with no sensitive-data access.");

            Assert.False(
                decision!.HasDefaultValue,
                $"{route.DeclaringType?.Name}.{route.Name} takes a BookerVisibility with a default value, "
                + "so a caller may omit the decision and receive whichever answer the default "
                + "happens to be. The decision must be required, not merely available.");
        }
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
        //
        // AND the properties of complex parameters, RECURSIVELY. A query surface grows by
        // acquiring a filter object: `[FromQuery] BookerFilter? filter` binds
        // `?filter.BookerEmail=`, and a scan of parameter names sees only "filter". A nested one
        // binds `?filter.Contact.Email=` and a one-level scan sees only "filter" and "Contact".
        // Both shapes were measured passing earlier versions of this test.
        //
        // TWO SCOPES, because the two rules justify different ones:
        //
        //   `booker`/`email` apply to EVERY http method. The spec says "no endpoint", and a
        //     POST named `bookings/search` taking a booker's email is a read whatever its verb —
        //     measured passing while this was GET-only. Safe to widen: no parameter or model
        //     property anywhere in the Backoffice contract carries either word except on the
        //     booking row itself, which is not a parameter.
        //
        //   The generic search words apply to GETs only. `name` is an ordinary field on a
        //     create or update body, and firing there would be a false positive on every
        //     legitimate write — a guard that fires for the wrong reason is read as noise and
        //     then relaxed, which is how the previous round lost `term` altogether.
        var readIdentifiers = new List<string>();
        var allIdentifiers = new List<string>();
        var scannedControllers = new List<string>();

        foreach (var controller in typeof(BookingsController).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract))
        {
            scannedControllers.Add(controller.Name);

            foreach (var method in controller.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.GetCustomAttributes<HttpMethodAttribute>().Any() is false)
                {
                    continue;
                }

                var isRead = method.GetCustomAttributes<HttpGetAttribute>().Any();

                foreach (var parameter in method.GetParameters())
                {
                    foreach (var identifier in Identifiers(parameter.Name, parameter.ParameterType, depth: 0))
                    {
                        allIdentifiers.Add(identifier);

                        if (isRead)
                        {
                            readIdentifiers.Add(identifier);
                        }
                    }
                }
            }
        }

        // ANTI-VACUITY, and it names the controller this guard exists for. `NotEmpty` plus a
        // count was not enough: blinding the scan to BookingsContoller specifically still left
        // plenty of identifiers from Resources and Services, and both checks passed.
        Assert.Contains(nameof(BookingsController), scannedControllers);
        Assert.Contains("resourceIds", readIdentifiers);

        foreach (var identifier in allIdentifiers)
        {
            Assert.False(
                identifier.Contains("booker", StringComparison.OrdinalIgnoreCase)
                || identifier.Contains("email", StringComparison.OrdinalIgnoreCase),
                $"A management endpoint exposes '{identifier}'. Answering questions about a "
                + "booker's contact details is not withholding them: a caller who may not read "
                + "an email but may filter by one can confirm it by watching whether a row "
                + "comes back.");
        }

        foreach (var identifier in readIdentifiers)
        {
            Assert.False(
                new[] { "name", "search", "searchterm", "term", "query", "q", "keyword" }
                    .Contains(identifier, StringComparer.OrdinalIgnoreCase),
                $"A management read exposes '{identifier}', a free-text search surface over "
                + "bookings. If it cannot reach booker contact details, name it for what it "
                + "searches and record why here.");
        }
    }

    /// <summary>
    /// A bound parameter's own name plus, for a complex type, the names it binds beneath it.
    /// </summary>
    /// <remarks>
    /// Depth-bounded and cycle-safe. Three levels because `?filter.Contact.Email=` is two, and
    /// the next shape somebody writes will be one deeper than whatever is guarded — the cost of
    /// a level is a reflection walk over a handful of view models.
    /// </remarks>
    private static IEnumerable<string> Identifiers(string? name, Type type, int depth)
    {
        if (name is not null)
        {
            yield return name;
        }

        if (depth >= 3)
        {
            yield break;
        }

        var bound = Nullable.GetUnderlyingType(type) ?? type;

        // A primitive, a string, a Guid, a CancellationToken or a collection of them binds no
        // names of its own.
        if (bound.IsPrimitive || bound.IsEnum || bound == typeof(string)
            || bound.Namespace?.StartsWith("System", StringComparison.Ordinal) is true)
        {
            yield break;
        }

        foreach (var property in bound.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            foreach (var identifier in Identifiers(property.Name, property.PropertyType, depth + 1))
            {
                yield return identifier;
            }
        }
    }
}
