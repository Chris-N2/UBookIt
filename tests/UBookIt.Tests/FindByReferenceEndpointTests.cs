using System.Reflection;
using UBookIt.Backoffice.Mapping;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Models;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.Membership.Permissions;
using Umbraco.Cms.Core.Security;

namespace UBookIt.Tests;

/// <summary>
/// The by-reference endpoint (booking-management spec, "A booking can be found by its
/// reference"): what it parses, what it refuses, and — the claim that matters — that a found
/// row is withheld or shown by the list's own decision rather than by a second one.
/// </summary>
/// <remarks>
/// The store is a small in-memory <see cref="IBookingManagementStore"/> holding summaries, because
/// the endpoint's job is translation and mapping and the seek itself is integration-tested
/// against SQL Server in <c>FindByReferenceStoreTests</c>. The visibility decision, by contrast,
/// runs for real: the accessor carries an Umbraco <c>User</c> in a real group, so "withheld for a
/// caller outside the group" is the production path and not a stubbed answer.
/// </remarks>
public class FindByReferenceEndpointTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    private static BookingSummary Summary(BookingReference reference, SummaryBooker? booker = null, BookingStatus status = BookingStatus.Confirmed)
        => new(
            Guid.NewGuid(),
            reference,
            BookingInterval.Create(TestData.Utc(Date, "10:00"), TestData.Utc(Date, "11:00"), TestData.LondonZoneId).Value,
            status,
            TestData.Now,
            booker ?? SummaryBooker.Of(new SummaryContact("Ada Lovelace", "ada@example.com")),
            [new BookedResource(Guid.NewGuid(), "Meeting Room A")],
            Service: null);

    private static (BookingsController Controller, InMemoryManagementStore Store) Endpoint(bool sensitiveData)
    {
        var store = new InMemoryManagementStore();
        var settings = new SiteBookingSettings { TimeZoneId = TestData.LondonZoneId, MaxQueryRangeDays = 31 };
        var bookings = new BookingService(
            new InMemoryResourceStore(), new InMemoryBookingStore(), new FixedTimeProvider(TestData.Now), settings);

        return (
            new BookingsController(
                store,
                bookings,
                new UnusedServiceBookingService(),
                new InMemoryResourceStore(),
                new InMemoryServiceStore(),
                settings,
                Security(sensitiveData)),
            store);
    }

    private static BookingModel Found(IActionResult result)
        => Assert.IsType<BookingModel>(Assert.IsType<OkObjectResult>(result).Value);

    private static (int Status, IReadOnlyList<ApiErrorModel> Errors) Refused(IActionResult result)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        var errors = Assert.IsType<ApiErrorModel[]>(problem.Extensions["errors"]);
        Assert.NotEmpty(errors);
        return (objectResult.StatusCode ?? 0, errors);
    }

    // ---- what it finds ----------------------------------------------------------------------

    [Fact]
    public async Task Spec_scenario_a_booking_is_found_by_its_reference()
    {
        var (controller, store) = Endpoint(sensitiveData: true);
        var reference = new RandomBookingReferenceFactory().Next();
        store.Add(Summary(reference));

        var found = Found(await controller.FindBookingByReference(reference.Display));

        Assert.Equal(reference.Value, found.Reference);
    }

    [Fact]
    public async Task Spec_scenario_the_reference_is_accepted_as_typed()
    {
        var (controller, store) = Endpoint(sensitiveData: true);
        var reference = new RandomBookingReferenceFactory().Next();
        store.Add(Summary(reference));

        foreach (var typed in new[] { reference.Value, reference.Value.ToLowerInvariant(), reference.Display, $" {reference.Display.ToLowerInvariant()} " })
        {
            Assert.Equal(reference.Value, Found(await controller.FindBookingByReference(typed)).Reference);
        }
    }

    [Fact]
    public async Task Spec_scenario_a_cancelled_booking_is_still_found()
    {
        var (controller, store) = Endpoint(sensitiveData: true);
        var reference = new RandomBookingReferenceFactory().Next();
        store.Add(Summary(reference, status: BookingStatus.Cancelled));

        Assert.Equal(nameof(BookingStatus.Cancelled), Found(await controller.FindBookingByReference(reference.Value)).Status);
    }

    // ---- what it refuses, and how the two refusals differ -----------------------------------

    [Fact]
    public async Task Spec_scenario_a_malformed_reference_is_distinguishable_from_a_miss()
    {
        var (controller, _) = Endpoint(sensitiveData: true);

        var malformed = Refused(await controller.FindBookingByReference("not-a-ref"));
        var missing = Refused(await controller.FindBookingByReference(new RandomBookingReferenceFactory().Next().Value));

        Assert.Equal(400, malformed.Status);
        Assert.Equal(FailureCodes.ReferenceInvalid, Assert.Single(malformed.Errors).Code);
        Assert.Equal(404, missing.Status);
        Assert.Equal(FailureCodes.BookingNotFound, Assert.Single(missing.Errors).Code);
    }

    [Fact]
    public async Task Spec_scenario_a_partial_reference_finds_nothing()
    {
        var (controller, store) = Endpoint(sensitiveData: true);
        var reference = new RandomBookingReferenceFactory().Next();
        store.Add(Summary(reference));

        var refused = Refused(await controller.FindBookingByReference(reference.Value[..5]));

        // Refused as MALFORMED — a prefix is not a reference — and no lookup was made for it.
        Assert.Equal(FailureCodes.ReferenceInvalid, Assert.Single(refused.Errors).Code);
        Assert.Empty(store.LookedUp);
    }

    [Fact]
    public void The_endpoint_offers_no_parameter_that_could_widen_the_match()
    {
        // Spec: "the endpoint offers no parameter that would make it so". One route value, no
        // query string, nothing a caller could set to ask for a prefix or a fragment.
        var action = typeof(BookingsController).GetMethod(nameof(BookingsController.FindBookingByReference))!;

        var parameters = action.GetParameters().Where(p => p.ParameterType != typeof(CancellationToken)).ToList();

        Assert.Single(parameters);
        Assert.Equal("reference", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
    }

    // ---- the claim that matters: the list's own withholding decision ------------------------

    [Fact]
    public async Task Spec_scenario_contact_details_are_withheld_by_the_lists_rule()
    {
        // The SAME path the list takes — BookingModelMapper.ToModel under ResolveBookerVisibility
        // — exercised with a real Umbraco user outside the group. A second visibility path in
        // this action would be a place to forget the decision; there is none, and this is what
        // shows it rather than the code comment saying so.
        var reference = new RandomBookingReferenceFactory().Next();

        var (outside, outsideStore) = Endpoint(sensitiveData: false);
        outsideStore.Add(Summary(reference));
        var withheld = Found(await outside.FindBookingByReference(reference.Value));

        var (inside, insideStore) = Endpoint(sensitiveData: true);
        insideStore.Add(Summary(reference));
        var shown = Found(await inside.FindBookingByReference(reference.Value));

        Assert.Equal(BookerConditions.Withheld, withheld.Booker.Condition);
        Assert.Null(withheld.Booker.Contact);
        Assert.Equal(BookerConditions.Shown, shown.Booker.Condition);
        Assert.Equal("ada@example.com", shown.Booker.Contact!.Email);
    }

    [Fact]
    public async Task Spec_scenario_an_erased_booking_is_still_found_and_shows_erased()
    {
        var (controller, store) = Endpoint(sensitiveData: true);
        var reference = new RandomBookingReferenceFactory().Next();
        store.Add(Summary(reference, SummaryBooker.Erased(TestData.Now)));

        var found = Found(await controller.FindBookingByReference(reference.Value));

        Assert.Equal(BookerConditions.Erased, found.Booker.Condition);
        Assert.Null(found.Booker.Contact);
    }

    [Fact]
    public void The_action_is_read_gated_and_not_sensitive_data_gated()
    {
        // A reference is not a contact detail. Gating this on the sensitive-data group would
        // teach that the gate is decoration — and would hide a booking's TIME from a Read-only
        // operator who could see the same row in the list.
        var action = typeof(BookingsController).GetMethod(nameof(BookingsController.FindBookingByReference))!;
        var policies = action.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).ToList();

        Assert.Contains(UBookIt.Backoffice.Constants.VerbPolicies.BookingsRead, policies);
        Assert.DoesNotContain(UBookIt.Backoffice.Constants.SensitiveDataAccessPolicy, policies);
    }

    // ---- doubles ----------------------------------------------------------------------------

    /// <summary>Holds summaries and records what was looked up; the list and search are unreachable here.</summary>
    private sealed class InMemoryManagementStore : IBookingManagementStore
    {
        private readonly List<BookingSummary> _rows = [];

        public List<BookingReference> LookedUp { get; } = [];

        public void Add(BookingSummary summary) => _rows.Add(summary);

        public Task<BookingSummary?> FindByReferenceAsync(BookingReference reference, CancellationToken cancellationToken = default)
        {
            LookedUp.Add(reference);
            return Task.FromResult(_rows.SingleOrDefault(row => row.Reference == reference));
        }

        public Task<BookingPage> ListAsync(BookingQuery query, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The by-reference endpoint does not list bookings.");

        public Task<BookingPage> FindByBookerEmailAsync(BookerEmailQuery query, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The by-reference endpoint does not search by address.");
    }

    private static IBackOfficeSecurityAccessor Security(bool sensitiveData)
    {
        var user = new User(new GlobalSettings());
        user.AddGroup(new ReadOnlyUserGroup(
            id: 1,
            key: sensitiveData ? Constants.Security.SensitiveDataGroupKey : Guid.NewGuid(),
            name: "Test group",
            description: null,
            icon: null,
            startContentId: null,
            startMediaId: null,
            alias: "testGroup",
            allowedLanguages: [],
            allowedSections: [UBookIt.Backoffice.Constants.SectionAlias],
            permissions: new HashSet<string>(),
            granularPermissions: new HashSet<IGranularPermission>(),
            hasAccessToAllLanguages: true));

        return new StubAccessor(new StubSecurity(user));
    }

    [Fact]
    public void Exactly_one_failure_of_this_endpoint_maps_to_404()
    {
        // THE PREMISE THE CLIENT'S MISS RESTS ON, made mechanical.
        //
        // The backoffice client decides "no booking has that reference" from the HTTP STATUS,
        // because it has no choice: measured against a running backoffice, Umbraco's HTTP client
        // discards the `errors` extension on a 404 (it keeps it on a 400), so the domain's
        // `booking-not-found` code never reaches the browser. `isMiss` in `find-fields.ts` says
        // so, and says the status is only unambiguous because this endpoint maps exactly one
        // failure to 404.
        //
        // That sentence was left as prose, which on this project is how an untested assertion
        // gets mistaken for a fact — so it is asserted here instead. Add a second 404-mapped
        // failure to this action and the client will report it to an operator as
        // "No booking has the reference X." — a false statement about the site's data. This test
        // fails first.
        // COMMENTS AND STRING LITERALS GO FIRST, and both removals earn their place.
        //
        // Comments: a future `// unlike FailureCodes.ServiceNotFound` in this action would fail
        // this test spuriously — a test dictating prose, which is the exact fault
        // `BookingsControllerContractTests` records as its reason for moving the old
        // status-default grep out of itself.
        //
        // String literals: the brace matcher below would otherwise count braces inside them. The
        // action already contains `$"No booking has the reference {parsed.Display}."`, whose one
        // `{` and one `}` happen to cancel — it balanced by luck, and a future `"{"` or `$"{{"`
        // would have silently truncated the extracted body and left this guard reading the wrong
        // text.
        var source = Sanitize(File.ReadAllText(
            Path.Combine(RepoFiles.Root, "src", "UBookIt.Backoffice", "Controllers", "BookingsController.cs")));

        var action = ActionBody(source, nameof(BookingsController.FindBookingByReference));

        var codes = Regex.Matches(action, @"FailureCodes\.(\w+)")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // POSITIVE CONTROL, AND IT COMES FIRST. Placed after the equality it was useless: the
        // equality already fails on an empty set, so the control could never be the assertion
        // that fired and its comment claimed a job the line above had done.
        Assert.NotEmpty(codes);

        Assert.Equal(new[] { nameof(FailureCodes.BookingNotFound), nameof(FailureCodes.ReferenceInvalid) }, codes);

        // And of the codes this action can actually produce, exactly one maps to 404.
        var mapsTo404 = codes
            .Select(code => (Code: code, Status: StatusFor(code)))
            .Where(mapped => mapped.Status == StatusCodes.Status404NotFound)
            .Select(mapped => mapped.Code)
            .ToArray();

        Assert.Equal(new[] { nameof(FailureCodes.BookingNotFound) }, mapsTo404);
    }

    /// <summary>The status <see cref="ApiResults" /> gives a single failure carrying this code.</summary>
    private static int StatusFor(string codeName)
    {
        var code = (string)typeof(FailureCodes).GetField(codeName)!.GetValue(null)!;
        var result = (ObjectResult)new List<DomainFailure> { new(code, "probe", null) }.ToProblemResult();

        return result.StatusCode!.Value;
    }

    /// <summary>
    /// Source with comments and string literals removed, so a scan reads CODE.
    /// </summary>
    private static string Sanitize(string source)
    {
        // Verbatim strings first (they may contain backslashes and doubled quotes), then
        // ordinary and interpolated ones, then block and line comments. Each is replaced by an
        // empty literal rather than deleted, so nothing either side of it runs together.
        var withoutStrings = Regex.Replace(source, @"@""(?:[^""]|"""")*""", @"""""");
        withoutStrings = Regex.Replace(withoutStrings, @"""(?:\\.|[^""\\])*""", @"""""");

        var withoutBlockComments = Regex.Replace(withoutStrings, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        return string.Join(
            '\n',
            withoutBlockComments
                .Split('\n')
                .Select(line => Regex.Replace(line, @"//.*$", string.Empty)));
    }

    /// <summary>The body of one action, brace-matched from its signature.</summary>
    private static string ActionBody(string source, string actionName)
    {
        var start = source.IndexOf($"{actionName}(", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{actionName} was not found in BookingsController.cs.");

        var open = source.IndexOf('{', start);
        var depth = 0;

        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}' && --depth == 0)
            {
                return source[open..(i + 1)];
            }
        }

        throw new InvalidOperationException($"{actionName}'s body is unbalanced.");
    }

    private sealed class StubAccessor(IBackOfficeSecurity? security) : IBackOfficeSecurityAccessor
    {
        public IBackOfficeSecurity? BackOfficeSecurity { get; } = security;
    }

    private sealed class StubSecurity(IUser? currentUser) : IBackOfficeSecurity
    {
        public IUser? CurrentUser { get; } = currentUser;

        public bool UserHasSectionAccess(string section, IUser user)
            => throw new InvalidOperationException("Visibility is decided from group membership, not section access.");

        public bool IsAuthenticated()
            => throw new InvalidOperationException("The authorization policy has already run.");
    }
}
