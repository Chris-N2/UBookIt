using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
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
            SummaryBooker.Of(new SummaryContact("Ada Lovelace", "ada@example.com")),
            [new BookedResource(RoomId, "Meeting Room A"), new BookedResource(TherapistId, "MRC Therapist")],
            new ServiceAttribution(ServiceId, "Initial Consultation"));

    [Fact]
    public void The_mapper_carries_the_booker_when_it_is_shown()
    {
        var model = BookingModelMapper.ToModel(Summary(), BookerVisibility.Shown);

        Assert.Equal(BookerConditions.Shown, model.Booker.Condition);
        Assert.NotNull(model.Booker.Contact);
        Assert.Equal("Ada Lovelace", model.Booker.Contact.Name);
        Assert.Equal("ada@example.com", model.Booker.Contact.Email);
        Assert.Null(model.Booker.ErasedUtc);
    }

    [Fact]
    public void The_mapper_withholds_the_booker_when_it_is_not()
    {
        var model = BookingModelMapper.ToModel(Summary(), BookerVisibility.Withheld);

        Assert.Equal(BookerConditions.Withheld, model.Booker.Condition);
        Assert.Null(model.Booker.Contact);

        // Withheld, not erased. The two absences send an operator to different places, so
        // the wrong one is asserted against as well as the right one.
        Assert.Null(model.Booker.ErasedUtc);
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
    /// <remarks>
    /// <b><c>BookerContactModel</c> is listed as well as <c>BookerModel</c>, and that is not
    /// tidiness.</b> Splitting the booker into a condition plus a nested contact object moved
    /// the personal data down a level: with only the outer model recorded, a phone number added
    /// to the inner one would reach every permitted caller without this guard noticing — the
    /// exact case it exists for. A model that carries personal data is guarded wherever it ends
    /// up, not wherever it used to be.
    /// </remarks>
    [Theory]
    [InlineData(typeof(BookingModel),
        "BookingId,Reference,StartUtc,EndUtc,TimeZoneId,Status,CreatedUtc,Booker,Resources,Service")]
    // Condition and ErasedUtc were weighed against the question this guard's failure asks.
    // Neither is personal data: Condition says which of shown/withheld/erased applies, and
    // ErasedUtc says when a record stopped holding a person — facts about the ROW, disclosing
    // nothing about who the booker was. They sit outside the contact object deliberately, since
    // both must reach a caller who may not see contact details. The name and email stay inside
    // it, where the mapper decides them.
    [InlineData(typeof(BookerModel), "Condition,Contact,ErasedUtc")]
    [InlineData(typeof(BookerContactModel), "Name,Email")]
    // The erase endpoint's response. It carries no personal data today, and it is the most
    // tempting place in the package to add some — "here is what we removed for you" — over a
    // requirement that forbids exactly that. Guarded so the temptation costs a failing test.
    [InlineData(typeof(ErasedBookerModel), "BookingId,ErasedUtc")]
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
            // BY CONSTRUCTION, not by enumeration — and this is the fifth version of this
            // guard, so the distinction is the whole point. An exact `== typeof(BookingModel)`
            // is blind to every shape a row can arrive in: `IReadOnlyList<BookingModel>`,
            // `BookingModel[]`, `Task<BookingModel>`, and `PagedBookingsModel`, which is the
            // actual HTTP response type. All four were measured composing rows hard-coded to
            // Shown while 1821 tests passed, and `ToModels` is not contrived: the controller
            // already does that `Select` inline, so lifting it out is the ordinary next edit.
            //
            // Each previous repair corrected the EXTENSION the last one got wrong — the
            // literal, the idiom, comments, the file — while the INTENSION, "no route composes
            // a row without the decision", stayed a list. `CarriesARow` recurses, so a shape
            // nobody has thought of yet is covered rather than added later.
            .Where(method => CarriesARow(method.ReturnType))
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

        // And it must still see the one route that legitimately exists. `NotEmpty` alone would
        // pass if the filter stopped seeing `ToModel` while matching something else — the exact
        // weakness that was fixed in the sibling guard in this same file and not carried across.
        Assert.Contains(routes, route => route.Name == nameof(BookingModelMapper.ToModel));

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

        // Any shape that carries a booking row to a caller: the row itself, the page that
        // wraps it, and any array, collection or Task of either — recursively, so
        // `Task<IReadOnlyList<BookingModel>>` is covered without naming it.
        static bool CarriesARow(Type type)
            => type == typeof(BookingModel)
                || type == typeof(PagedBookingsModel)
                || (type.IsArray && CarriesARow(type.GetElementType()!))
                || (type.IsGenericType && type.GetGenericArguments().Any(CarriesARow));
    }

    /// <summary>
    /// The package decides sensitive-data access one way: by asking Umbraco.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The requirement has two halves — that the package uses Umbraco's built-in group, and that
    /// it <b>defines no group, flag or configuration setting of its own for the purpose</b>. Only
    /// the first was observed; a fixture using the real group key says nothing about whether a
    /// second mechanism exists beside it.
    /// </para>
    /// <para>
    /// The second half is the one worth guarding, because a parallel mechanism is a second answer
    /// to the same question, free to disagree with the first — and the roadmap's permissions work
    /// is exactly when somebody would reach for a <c>UBookIt:SensitiveDataGroupKey</c> setting.
    /// </para>
    /// <para>
    /// Comment-stripped, so the prose explaining the mechanism does not read as a second one.
    /// The identifier set is compared whole rather than searched, on the same reasoning as the
    /// membership snapshot: a check that something is absent passes when the scan breaks.
    /// </para>
    /// <para>
    /// <b>It once recorded exactly one identifier, and that was too narrow a rule for the
    /// guarantee it stands for.</b> The requirement forbids a second <i>source of truth</i> — a
    /// group, a flag, a setting of our own. It does not forbid naming the concept. Adding an
    /// authorization policy for the erase endpoint introduced a requirement, a handler and a
    /// policy constant, none of which decides anything: the handler asks Umbraco and returns
    /// the answer. Failing that was the guard checking its mechanism rather than its guarantee.
    /// </para>
    /// <para>
    /// So it now asserts both halves separately. The set still has to be stated whole, so a new
    /// name is still a deliberate decision rather than a silent addition — and the thing the
    /// requirement actually forbids is asserted directly, by name, instead of being implied by
    /// the set having one element.
    /// </para>
    /// <para>
    /// Compared as a <b>sorted distinct set</b>. The previous ordered-list form also depended on
    /// the order <c>RepoFiles.Paths</c> happens to enumerate in, which is a property of the file
    /// system rather than of the package.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_package_defines_no_sensitive_data_mechanism_of_its_own()
    {
        var occurrences = new List<string>();

        foreach (var path in RepoFiles.Paths("src", "*.cs"))
        {
            var code = string.Join(
                '\n',
                File.ReadAllLines(path)
                    .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

            occurrences.AddRange(
                Regex.Matches(code, @"\w*SensitiveData\w*").Select(match => match.Value));
        }

        // Every identifier in the package that names the concept. Each is either Umbraco's own
        // membership test or plumbing that routes to it — none of them holds a group key, reads
        // a setting, or decides membership itself:
        //
        //   HasAccessToSensitiveData      Umbraco's extension method. THE decision, and the
        //                                 only thing here that makes one.
        //   UBookItSensitiveDataHandler   Calls it and succeeds or fails the requirement.
        //   UBookItSensitiveDataRequirement / SensitiveDataAccessPolicy
        //                                 The policy the erase endpoint carries, so its gate is
        //                                 a property of the endpoint rather than a line inside
        //                                 it. Neither carries a value.
        //   UBookItSensitiveDataAccess    The file's own type-name prefix.
        Assert.Equal(
            [
                "HasAccessToSensitiveData",
                "SensitiveDataAccessPolicy",
                "UBookItSensitiveDataAccess",
                "UBookItSensitiveDataHandler",
                "UBookItSensitiveDataRequirement",
            ],
            occurrences.Distinct().Order(StringComparer.Ordinal).ToList());

        // The half the requirement is actually about, asserted directly rather than inferred
        // from the size of the set above. Reimplementing the membership test against Umbraco's
        // group key, or introducing a setting of our own, would add a SECOND answer to a
        // question Umbraco already answers — and two answers are free to disagree.
        Assert.DoesNotContain("SensitiveDataGroupKey", occurrences);
        Assert.DoesNotContain(
            occurrences,
            occurrence => occurrence.Contains("Setting", StringComparison.Ordinal)
                || occurrence.Contains("GroupKey", StringComparison.Ordinal)
                || occurrence.Contains("Flag", StringComparison.Ordinal));

        // And the decision itself is still made by asking Umbraco, so the set above cannot be
        // satisfied by plumbing that routes to nothing.
        Assert.Contains("HasAccessToSensitiveData", occurrences);
    }

    /// <summary>
    /// Every durable row type and what it carries, so a second home for personal data cannot
    /// arrive unnoticed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>booker-erasure</c> capability requires that booker contact details have exactly
    /// one durable home, and <c>design.md</c> names that as the mitigation for the change's
    /// headline risk — that a later feature re-homes the data and reduces erasure to a gesture.
    /// A mitigation that exists only as a sentence is not one: the features most likely to
    /// break it (0.5.0's confirmation emails, an erasure audit table, a retention report) will
    /// be written by somebody who has not read the spec.
    /// </para>
    /// <para>
    /// <b>A membership snapshot, not a search for three column names.</b> An earlier form
    /// filtered on properties literally called <c>BookerName</c>/<c>BookerEmail</c>/
    /// <c>BookerPhone</c> — which is the mechanism, not the guarantee, and every plausible way
    /// of breaking the requirement evades it: <c>ErasureAuditRow.SubjectEmail</c>,
    /// <c>OutboxMessageRow.RecipientAddress</c>, <c>ReportRow.CustomerName</c>. None of those
    /// spellings is on the list, all of them are a second durable home.
    /// </para>
    /// <para>
    /// So this records the whole persistence surface — every row type and every property —
    /// and fails on <b>any</b> difference. What it detects is CHANGE, which it can do
    /// honestly; it cannot recognise personal data, and a guard claiming to would be checking
    /// a mechanism while appearing to promise a guarantee. What it buys is that a column
    /// cannot join a stored row without somebody answering the question in the failure
    /// message.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_durable_storage_surface_has_not_changed_without_a_decision()
    {
        var actual = typeof(UBookIt.Persistence.UBookItDbContext).Assembly
            .GetTypes()
            .Where(type => type.Name.EndsWith("Row", StringComparison.Ordinal))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .Select(type => type.Name + ": " + string.Join(
                ",",
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(property => property.Name)
                    .OrderBy(name => name, StringComparer.Ordinal)))
            .ToList();

        // Measured from the assembly, not written from memory. A recorded set somebody typed
        // out by hand is a second description of the schema, and the first thing it does is
        // disagree with the real one.
        string[] recorded =
        [
            "BookingRow: BookerEmail,BookerErasedUtc,BookerName,BookerPhone,Claims,CreatedUtc,EndUtc,Id,MemberKey,Reference,ServiceId,ServiceName,StartUtc,Status,TimeZoneId",
            "ClaimRow: BookingId,Id,ResourceId",
            "ExceptionRow: Date,EndTime,Id,ResourceId,StartTime",
            "OpenHoursRow: DayOfWeek,EndTime,Id,ResourceId,StartTime",
            "ResourceCapabilityRow: Key,ResourceId",
            "ResourceRow: Capabilities,Description,DirectlyBookable,DisplayName,Exceptions,GranularityMinutes,HorizonDays,Id,LeadTimeMinutes,MaxDurationMinutes,MinDurationMinutes,OpenHours,Type",

            // Decision, resource-responsibility (roadmap 0.8.0): four keys and nothing
            // else. The party columns reference an Umbraco user or group BY KEY — no
            // name, no email address, nothing personal — so no booker or staff detail
            // gains a second durable home here; the people behind the keys are resolved
            // from Umbraco's own store at the moment they are needed.
            "ResponsibilityRow: PartyKey,PartyType,SubjectId,SubjectType",
            "ServiceRoleCapabilityRow: Key,ServiceRoleId",
            "ServiceRoleRow: Capabilities,Count,Id,ResourceType,ServiceId,VisitorSelectable",
            "ServiceRow: DurationKind,Id,MaxDurationMinutes,MinDurationMinutes,Name,Roles",
        ];

        Assert.True(
            actual.SequenceEqual(recorded),
            "The persistence layer's stored shape has changed."
            + Environment.NewLine + "  recorded:" + Environment.NewLine + "    "
            + string.Join(Environment.NewLine + "    ", recorded)
            + Environment.NewLine + "  actual:" + Environment.NewLine + "    "
            + string.Join(Environment.NewLine + "    ", actual)
            + Environment.NewLine
            + "If the difference stores a person's name, email address, phone number or member "
            + "key, it is a SECOND durable home for personal data — and the booker-erasure "
            + "capability requires exactly one, because erasing one home while another keeps a "
            + "copy is not erasure. Either erase it with the booking, or do not store it.");

        // Still exactly one row carrying the booker's contact columns. Asserted separately so
        // the specific guarantee is stated, not merely implied by the snapshot above.
        var carriers = actual
            .Where(entry => entry.Contains("BookerName", StringComparison.Ordinal))
            .ToList();

        Assert.Single(carriers);
        Assert.StartsWith("BookingRow:", carriers[0], StringComparison.Ordinal);
    }

    [Fact]
    public void The_contract_states_why_contact_details_are_absent()
    {
        // Was `The_contract_states_what_a_null_booker_means`, and the null it pinned no longer
        // exists: absence acquired a second cause — erasure — so the member stopped being
        // nullable and states its condition instead. The GUARANTEE is unchanged and is what is
        // pinned here: a caller must be told WHY there are no contact details, rather than left
        // to infer it. What changed is that the shape now carries the answer, so the sentence
        // to pin is the one saying the condition is stated rather than inferred.
        //
        // Normalised first: an XML doc sentence wraps across `///` lines, so a raw substring
        // match asserts the line breaks rather than the sentence, and fails the moment somebody
        // reflows the comment. Strip the markers and collapse whitespace, then match the words.
        var source = RepoFiles.Read("src/UBookIt.Backoffice/Models/BookingModels.cs");

        var prose = Regex.Replace(
            Regex.Replace(source, @"^\s*///", " ", RegexOptions.Multiline),
            @"\s+",
            " ");

        // The three conditions are named, so a reader of the contract alone knows the full set.
        Assert.Contains("shown, withheld, or erased", prose, StringComparison.Ordinal);

        // Read, never deduced — the property that makes the third state worth having.
        Assert.Contains(
            "it never infers one from the absence of a field",
            prose,
            StringComparison.Ordinal);

        // Still true, and still the reason absence at this level can carry meaning at all.
        Assert.Contains(
            "Every booking has a booker",
            prose,
            StringComparison.Ordinal);

        // Erasure beats withholding, stated where a client integrator reads rather than only in
        // the spec — this is the rule that keeps "ask a colleague" from being said about a
        // booking nobody can recover.
        Assert.Contains("Erasure outranks withholding", prose, StringComparison.Ordinal);
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
        // **WHAT CHANGED, AND WHAT DID NOT.** This used to assert that NO endpoint anywhere
        // named a booker or an email. The requirement it stands for was reopened deliberately
        // (find-by-booker, signed off 2026-09-05) once a data subject's erasure request — which
        // arrives as an email address and nothing else — could not otherwise be honoured.
        //
        // The guarantee is unchanged and is what is asserted now: an endpoint may accept a
        // contact detail ONLY if it requires sensitive-data access as its OWN authorization. A
        // caller who could already read every address on the page learns nothing from asking
        // about one; a caller who could not must not be able to ask at all. So the tripwire
        // still fires on exactly the thing it was built to catch — a filter added to an
        // endpoint gated on section access alone — and no longer fires on the gated lookup.
        //
        // It is a POLICY that satisfies this, never a check inside a handler: a condition
        // somebody must remember to write leaves a route that reaches the query having
        // established nothing, and reflection cannot see it at all.
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
        // The management actions that CHANGE something. Everything else is a read, and a read
        // may not expose a free-text search surface over bookings without being named here and
        // justified. Recorded rather than inferred from the HTTP verb, because this package
        // deliberately uses POST for a read that takes a sensitive value.
        string[] KnownWrites =
        [
            "BookingsController.CancelBooking",
            // Confirm and decline are genuine writes — each drives a status transition
            // through the domain and persists it (approval-decline change). Neither takes a
            // free-text or contact-detail parameter; each takes a booking id alone.
            "BookingsController.ConfirmBooking",
            "BookingsController.DeclineBooking",
            "BookingsController.EraseBooker",
            "ResourcesController.CreateResource",
            "ResourcesController.UpdateResource",
            "ResourcesController.DeleteResource",
            "ServicesController.CreateService",
            "ServicesController.UpdateService",
            "ServicesController.DeleteService",
        ];

        var classifiedActions = new List<string>();
        var readIdentifiers = new List<(string Identifier, string Where)>();
        var allIdentifiers = new List<(string Identifier, string Where, bool Gated)>();
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

                // A READ, which is no longer the same set as "a GET".
                //
                // This asked `HttpGetAttribute` alone, and that was harmless for exactly as
                // long as every management read was a GET. `find-by-booker` ended that: it is
                // a POST BECAUSE it takes a contact detail, since an address in a query string
                // is written to the web server's log, every proxy's log and the browser's
                // history. So the package's own precedent for "a read that takes a sensitive
                // value" is a POST — and the free-text rule below, which exists to force any
                // search surface over bookings to be named and justified, could not see one.
                // An ungated `[HttpPost("bookings/search")] Search([FromQuery] string term)`
                // passed this guard.
                //
                // Widening to "GET or POST" outright would fire on every legitimate write body
                // carrying a `name`, which is the false positive the rule below is scoped to
                // avoid — and a guard that cries wolf gets relaxed rather than fixed. So the
                // WRITES are enumerated instead: anything not recorded as one is treated as a
                // read. A new POST therefore has to be classified by whoever adds it, which is
                // the decision this guard exists to force.
                var isWrite = KnownWrites.Contains(
                    $"{controller.Name}.{method.Name}", StringComparer.Ordinal);

                var isRead = !isWrite;

                classifiedActions.Add(
                    $"{controller.Name}.{method.Name} = {(isWrite ? "write" : "read")}");

                // The action's OWN authorization, not the controller's. The base controller's
                // section policy applies to everything and would make every endpoint look
                // gated; what this requirement is about is the second, narrower gate.
                var gated = method.GetCustomAttributes<AuthorizeAttribute>()
                    .Any(attribute => attribute.Policy == UBookIt.Backoffice.Constants.SensitiveDataAccessPolicy);

                foreach (var parameter in method.GetParameters())
                {
                    foreach (var identifier in Identifiers(parameter.Name, parameter.ParameterType, depth: 0))
                    {
                        allIdentifiers.Add((identifier, $"{controller.Name}.{method.Name}", gated));

                        if (isRead)
                        {
                            readIdentifiers.Add((identifier, $"{controller.Name}.{method.Name}"));
                        }
                    }
                }
            }
        }

        // ANTI-VACUITY, and it names the controller this guard exists for. `NotEmpty` plus a
        // count was not enough: blinding the scan to BookingsContoller specifically still left
        // plenty of identifiers from Resources and Services, and both checks passed.
        Assert.Contains(nameof(BookingsController), scannedControllers);
        Assert.Contains(readIdentifiers, entry => entry.Identifier == "resourceIds");

        // ANTI-VACUITY for the rule below: at least one endpoint really does take a contact
        // detail now, so a scan that stopped recognising them would fail here rather than
        // reporting that nothing needs gating.
        Assert.Contains(
            allIdentifiers,
            entry => entry.Identifier.Contains("email", StringComparison.OrdinalIgnoreCase));

        // THE SECOND OBLIGATION, and the one the first version of this guard lost.
        //
        // The reopened requirement forbids two things, not one: an UNGATED endpoint accepting a
        // contact detail (below), and ANY such endpoint offering a partial, prefix, substring,
        // fuzzy or wildcard form, an ordering by a contact detail, or a count-only response.
        // The narrowed guard enforced only the first, so a GATED
        //
        //     [HttpGet("bookings/enumerate-by-domain")]
        //     [Authorize(Policy = Constants.SensitiveDataAccessPolicy)]
        //     public IActionResult Enumerate([FromQuery] string bookerEmailContains)
        //
        // passed it — an enumeration facility over exactly the values this capability exists to
        // protect, wearing the right policy. `design.md` D3 promised "the restatement must keep
        // a tripwire"; it kept one of the two.
        //
        // Recorded as a SET rather than as a search for suspicious operation words: a list of
        // forbidden spellings passes on the next one somebody invents, and the parameter that
        // widens this will be called `match` or `mode` or `q`, not `contains`. Every parameter
        // this scan RECOGNISES as a contact detail is named here, and a new one fails until an
        // author states what it does.
        //
        // **The limit of that claim, stated because the first version of this comment
        // overreached it:** the set is populated by a name filter — `booker` or `email` — so a
        // parameter called `subject`, `address` or `who` carrying an address enters neither
        // this set nor the gating check below. That is the same class of gap as the free-text
        // list further down, and it is why the free-text list exists alongside this: between
        // them they cover the words somebody actually reaches for. Neither is a proof, and a
        // reviewer should read new parameters rather than trusting either.
        string[] recordedContactParameters =
        [
            "BookingsController.FindBookingsByBooker: Email",
        ];

        var contactParameters = allIdentifiers
            .Where(entry =>
                entry.Identifier.Contains("booker", StringComparison.OrdinalIgnoreCase)
                || entry.Identifier.Contains("email", StringComparison.OrdinalIgnoreCase))
            .Select(entry => $"{entry.Where}: {entry.Identifier}")
            .Distinct()
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            contactParameters.SequenceEqual(recordedContactParameters.Order(StringComparer.Ordinal)),
            "The set of endpoint parameters naming a booker contact detail has changed."
            + Environment.NewLine
            + $"  recorded: {string.Join(", ", recordedContactParameters.Order(StringComparer.Ordinal))}"
            + Environment.NewLine
            + $"  actual:   {string.Join(", ", contactParameters)}"
            + Environment.NewLine
            + "A parameter accepting a contact detail may exist ONLY if its endpoint requires "
            + "sensitive-data access as its own authorization AND it matches the whole value "
            + "exactly. A partial match answers WHICH PEOPLE match a fragment, which is an "
            + "enumeration facility rather than a lookup."
            + Environment.NewLine
            + Environment.NewLine
            + "THIS guard sees only the first of those two. It compares parameter NAMES, so it "
            + "cannot tell equality from a substring match — keeping a parameter called 'email' "
            + "while matching with Contains passes here and passes the whole unit suite. "
            + "Exactness is observed in the INTEGRATION suite, by "
            + "FindByBookerStoreTests.The_search_emits_an_equality_comparison_and_never_a_LIKE "
            + "and .Matching_is_exact_and_a_fragment_finds_nothing. If you record a new "
            + "parameter here, give it an equivalent there — this message is not evidence that "
            + "its matching was checked.");

        foreach (var (identifier, where, gated) in allIdentifiers)
        {
            var isContactDetail =
                identifier.Contains("booker", StringComparison.OrdinalIgnoreCase)
                || identifier.Contains("email", StringComparison.OrdinalIgnoreCase);

            Assert.False(
                isContactDetail && !gated,
                $"{where} accepts '{identifier}' without requiring sensitive-data access as its "
                + "own authorization. Answering questions about a booker's contact details is "
                + "not withholding them: a caller who may not read an email but may filter by "
                + "one can confirm it by watching whether a row comes back. Carry "
                + "[Authorize(Policy = Constants.SensitiveDataAccessPolicy)] on the action - a "
                + "check inside the handler does not satisfy this and cannot be seen from here.");
        }

        // THE CLASSIFICATION ITSELF IS RECORDED, not merely the names of the writes.
        //
        // `KnownWrites` EXEMPTS whatever is on it: an action listed there is treated as a write
        // and the free-text rule below stops applying to it. Asserting only that its entries
        // still exist left the obvious bypass open — add a read to the list and a failing guard
        // goes green, which is exactly what the failure message invites a hurried reader to do.
        // A guard whose escape hatch nobody guards is the fault this rule was built to catch,
        // one level up.
        //
        // So the whole action set is recorded with its classification. Adding an action fails
        // this; RECLASSIFYING one fails it too, and the failure names both sides. The list can
        // still be edited — it must be, when a genuine write arrives — but not silently, and
        // not as a way of quieting the rule underneath.
        string[] recordedActions =
        [
            "BookingsController.CancelBooking = write",
            "BookingsController.ConfirmBooking = write",
            "BookingsController.DeclineBooking = write",
            "BookingsController.EraseBooker = write",
            "BookingsController.FindBookingsByBooker = read",
            "BookingsController.ListBookings = read",
            "ResourcesController.CreateResource = write",
            "ResourcesController.DeleteResource = write",
            "ResourcesController.GetResource = read",
            "ResourcesController.ListCapabilities = read",
            "ResourcesController.ListResourceTypes = read",
            "ResourcesController.ListResources = read",
            "ResourcesController.UpdateResource = write",
            "ServicesController.CreateService = write",
            "ServicesController.DeleteService = write",
            "ServicesController.GetService = read",
            "ServicesController.ListServices = read",
            "ServicesController.PreviewServiceConfiguration = read",
            "ServicesController.UpdateService = write",
        ];

        Assert.True(
            classifiedActions.Order(StringComparer.Ordinal).SequenceEqual(
                recordedActions.Order(StringComparer.Ordinal)),
            "The management actions, or how they are classified, have changed."
            + Environment.NewLine
            + $"  recorded: {string.Join(", ", recordedActions.Order(StringComparer.Ordinal))}"
            + Environment.NewLine
            + $"  actual:   {string.Join(", ", classifiedActions.Order(StringComparer.Ordinal))}"
            + Environment.NewLine
            + "A READ may not expose a free-text search surface over bookings. Marking one as a "
            + "write in KnownWrites exempts it from that rule — which is a decision, not a "
            + "formality, so it has to be made here as well. If an action genuinely changes "
            + "something, record it as a write in BOTH places and say so in the change. Note "
            + "that a POST is not evidence either way: this package uses POST for a read that "
            + "carries a sensitive value.");

        foreach (var (identifier, where) in readIdentifiers)
        {
            Assert.False(
                new[] { "name", "search", "searchterm", "term", "query", "q", "keyword" }
                    .Contains(identifier, StringComparer.OrdinalIgnoreCase),
                $"{where} exposes '{identifier}', which reads as a free-text search surface "
                + "over bookings, and this guard treats it as a READ."
                + Environment.NewLine
                + Environment.NewLine
                + "If it is actually a WRITE — a create or an update whose body legitimately "
                + "carries a name — record it in KnownWrites above and this stops applying. "
                + "Reads are the default deliberately: the package uses POST for a read that "
                + "takes a sensitive value, so the HTTP verb cannot classify these and "
                + "somebody has to."
                + Environment.NewLine
                + Environment.NewLine
                + "If it IS a read, it must not offer free text over bookings: a caller who "
                + "may not see a booker's details could confirm one by watching whether a row "
                + "comes back. Name it for what it searches and record why here.");
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
