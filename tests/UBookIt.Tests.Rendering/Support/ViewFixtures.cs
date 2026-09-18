using UBookIt.Web.Rendering;

namespace UBookIt.Tests.Rendering.Support;

/// <summary>
/// One rendered case: a view, a model, and a name for the state so a failure says
/// which one it was.
/// </summary>
/// <param name="ViewPath">Absolute view path.</param>
/// <param name="State">
/// What is peculiar about this model, for the failure message. "no times", "with
/// errors", "choice reset" — enough that a reader knows what was being rendered
/// without opening the fixture.
/// </param>
/// <param name="Model">The model to render with.</param>
public sealed record ViewCase(string ViewPath, string State, object Model)
{
    public override string ToString() => $"{ViewPath} [{State}]";
}

/// <summary>
/// One rendered <b>document</b>: a whole page, as one shipped view renders it.
/// <para>
/// Rule 1 is a document-level rule — ids are unique within a document, and an aria
/// reference resolves against a document — so it must be asked of a document, and a
/// shared partial is not one. The partials become a document by being rendered by
/// the flow view that includes them.
/// </para>
/// <para>
/// <b>This type holds exactly one view, and that is the guarantee, not a
/// simplification.</b> It previously held a list, and the suite assembled the shared
/// partials into what it believed was flow order. Nothing checked the belief and it
/// was wrong: the assembly included the booker fields unconditionally, where both
/// flows guard them, so an error summary linking to a booker field always resolved —
/// concealing exactly the defect rule 1 exists to catch. A type that cannot express
/// an assembled document cannot drift from the page it is standing in for.
/// </para>
/// </summary>
public sealed record DocumentCase(ViewCase Page)
{
    public string ViewPath => Page.ViewPath;

    public string State => Page.State;

    public object Model => Page.Model;

    /// <summary>
    /// The form model this page renders, as none-or-one. Most document rules ask
    /// about the model's errors; pages that carry no form model (the catalogue, the
    /// confirmations) yield nothing and those rules pass over them.
    /// </summary>
    public IEnumerable<IBookingFormView> Forms
        => Model is IBookingFormView form ? [form] : [];

    /// <summary>Names the view and the state, so a failure says which file to open.</summary>
    public override string ToString() => $"{ViewPath} [{State}]";
}

/// <summary>
/// The model states each shipped view is rendered across.
/// <para>
/// Several per view rather than one, because a property is often only live in a
/// particular state — <c>LongestAvailableMinutes</c> renders only when the length
/// is the problem — and judging "does varying this change the output" from a
/// single base model would report those as dead (design D4).
/// </para>
/// <para>
/// Built directly rather than resolved through Core. These are view models; going
/// through the booking graph would drag stores, a clock and a service into a
/// rendering suite, and would make a fixture change a domain question.
/// </para>
/// <para>
/// The shared partials are rendered against <b>both</b> form models — the resource
/// flow's and the service flow's — because they are shared, and a partial that
/// worked for one and not the other is precisely the fault "one bar, stated once"
/// is meant to prevent.
/// </para>
/// </summary>
public static class ViewFixtures
{
    private static readonly DateOnly Today = new(2026, 8, 20);

    /// <summary>
    /// The default list: three dates, the first of them the selected one.
    /// </summary>
    /// <remarks>
    /// A LISTED selected date is the ordinary case, so it is the default — the states above
    /// cover the ones that are not, which is the way round that leaves the common rendering
    /// exercised by everything rather than by one test.
    /// </remarks>
    private static readonly IReadOnlyList<AvailableDate> Dates =
    [
        new(Today, true),
        new(Today.AddDays(1), false),
        new(Today.AddDays(3), false),
    ];

    // DECLARED HERE, ABOVE `All`, AND NOT BESIDE THE STATES THAT USE IT.
    //
    // Static initialisers run in TEXTUAL order, and `All` is `[.. Build()]` — so a `Dates`
    // declared further down the file is still null when Build() enumerates FormStates(), and
    // every fixture in the suite fails with a TypeInitializationException naming none of this.
    // The same trap `ViewInventory` records for its own exclusion list, met again.


    private static readonly IReadOnlyList<BookingTimeOption> Times =
    [
        new("2026-08-20T08:00:00.0000000+00:00", "09:00"),
        new("2026-08-20T08:30:00.0000000+00:00", "09:30"),
    ];

    private static readonly IReadOnlyList<BookingResourceChoice> Choices =
    [
        new(new Guid("00000000-0000-0000-0000-000000000002"), "Ada"),
        new(new Guid("00000000-0000-0000-0000-000000000003"), "Grace"),
    ];

    /// <summary>
    /// Host-page parameters for the preservation states: a plain pair, a repeated name
    /// (a multi-valued parameter arrives as one pair per value, order kept), and a
    /// markup-hostile value — the one the encoding scenario watches.
    /// </summary>
    private static readonly IReadOnlyList<PreservedQueryPair> PreservedPairs =
    [
        new("utm_source", "newsletter"),
        new("tag", "a"),
        new("tag", "b"),
        new("note", "\"><script>alert(1)</script>"),
    ];

    /// <summary>
    /// The views that are fragments rather than pages. They are included only by the
    /// flow views, so nothing in this suite renders them as a page — and rule 1 asks
    /// a question only a page can answer. They reach rule 1 inside the flow pages
    /// that render them, which is the composition the site serves.
    /// </summary>
    public static IReadOnlyList<string> Partials { get; } =
    [
        ViewInventory.ErrorSummary,
        ViewInventory.DateAndLength,
        ViewInventory.Times,
        ViewInventory.YourDetails,
        ViewInventory.PrivacyNotice,
        ViewInventory.AvailableDates,
    ];

    /// <summary>Every single-view case, used by the per-view property rule.</summary>
    public static IReadOnlyList<ViewCase> All { get; } = [.. Build()];

    /// <summary>The cases for one view.</summary>
    public static IReadOnlyList<ViewCase> For(string viewPath)
        => [.. All.Where(c => c.ViewPath == viewPath)];

    /// <summary>
    /// Every document, for the markup invariants. A standalone view is a document
    /// of one part; the four shared partials compose into one document in the order
    /// the flow views render them.
    /// </summary>
    public static IReadOnlyList<DocumentCase> Documents { get; } = [.. BuildDocuments()];

    /// <summary>
    /// Every document the rules are asked of — each one the rendered output of a
    /// shipped view.
    /// <para>
    /// There is deliberately <b>no hand-composed document</b> here. The shared
    /// partials were once concatenated in what someone believed was flow order and
    /// the result called a page; nothing checked the belief, and it was wrong —
    /// composing <c>_YourDetails</c> unconditionally produced a document neither
    /// flow renders, in which an error summary linking to booker fields always
    /// resolved. That is the very defect class the document-level rule exists to
    /// catch, concealed by the fixture the rule was reading.
    /// </para>
    /// <para>
    /// Now the flow views render, so the composition is theirs. Change what
    /// <c>Service.cshtml</c> includes, or the condition it includes it under, and
    /// the documents these rules see change with it — with nothing here to edit,
    /// and so nothing here to forget to edit.
    /// </para>
    /// </summary>
    private static IEnumerable<DocumentCase> BuildDocuments()
    {
        // Every shipped view that is a page in its own right — which now includes
        // the three flow views, and so includes the real composition of the shared
        // partials. The partials themselves are fragments and are excluded: rule 1
        // asks a question only a page can answer.
        foreach (var rendered in All.Where(c => !Partials.Contains(c.ViewPath)))
        {
            yield return new DocumentCase(rendered);
        }
    }

    /// <summary>A view that declares no model still needs something to render against.</summary>
    private static readonly object NoModel = new();

    /// <summary>
    /// The states the cancellation page can be in — which are states of the BOOKING, since the
    /// page has no other input.
    /// </summary>
    private static IEnumerable<(string State, CancellationPageModel Model)> CancellationStates()
    {
        var start = new DateTimeOffset(2026, 10, 15, 14, 0, 0, TimeSpan.Zero);

        yield return ("service booking", new CancellationPageModel
        {
            Reference = "BJQ4-ZP5C",
            LocalStart = start,
            LocalEnd = start.AddHours(1),
            TimeZoneId = "Europe/London",
            ServiceName = "Massage",
            ResourceNames = ["Meeting Room A"],
        });

        yield return ("booked directly, one resource", new CancellationPageModel
        {
            Reference = "DVNF-ZTHK",
            LocalStart = start.AddDays(3).AddHours(2),
            LocalEnd = start.AddDays(3).AddHours(3),
            TimeZoneId = "Europe/London",
            ServiceName = null,
            ResourceNames = ["Meeting Room A"],
        });

        yield return ("several resources", new CancellationPageModel
        {
            Reference = "DXNG-Z7S4",
            LocalStart = start.AddDays(9).AddHours(-5),
            LocalEnd = start.AddDays(9).AddHours(-3),
            TimeZoneId = "Europe/London",
            ServiceName = "Team day",
            ResourceNames = ["Meeting Room A", "Meeting Room B"],
        });

        // What was booked could not be established — the same trade the booker's message makes.
        yield return ("nothing nameable", new CancellationPageModel
        {
            Reference = "FMZH-36GB",
            LocalStart = start.AddDays(21).AddMinutes(45),
            LocalEnd = start.AddDays(21).AddMinutes(105),
            TimeZoneId = "Europe/London",
            ServiceName = null,
            ResourceNames = [],
        });
    }

    private static IEnumerable<ViewCase> Build()
    {
        foreach (var partial in new[]
        {
            ViewInventory.DateAndLength,
            ViewInventory.Times,
            ViewInventory.ErrorSummary,
            ViewInventory.YourDetails,
            ViewInventory.PrivacyNotice,
            ViewInventory.AvailableDates,
        })
        {
            foreach (var (state, model) in FormStates())
            {
                yield return new ViewCase(partial, state, model);
            }
        }

        // The flow views: whole pages, each rendering the shared partials in the
        // order and under the conditions it actually uses.
        //
        // Each takes only the form model it declares — `Service.cshtml` is
        // `@model ServiceFormModel`, the two resource pages `BookingFormModel` — so
        // the states are partitioned by model type rather than crossed. A state a
        // model cannot express is not evidence about the view that renders it.
        foreach (var (state, model) in FormStates())
        {
            switch (model)
            {
                case ServiceFormModel:
                    yield return new ViewCase(ViewInventory.ServiceFlow, state, model);
                    break;

                case BookingFormModel:
                    yield return new ViewCase(ViewInventory.ResourceFlow, state, model);

                    // The dispatcher's page delegates to the one above with a single
                    // `PartialAsync`. Rendered separately rather than assumed
                    // equivalent: "it only delegates" is a claim about source, and
                    // the wrapper it adds is part of the document the rules judge.
                    yield return new ViewCase(
                        ViewInventory.ResourceFlowViaDispatcher, state, model);
                    break;
            }
        }

        // THE CANCELLATION PAGES. Standalone documents, so they reach rule 1 directly rather than
        // through a flow view that includes them.
        //
        // The states are the ones that change what Index renders: a service booking names a
        // service, a direct one does not, and a multi-resource booking lists more than one name.
        // The other two pages take no model at all — see ModelReferences.StaticViews for why
        // that is the guarantee rather than an omission.
        foreach (var (state, model) in CancellationStates())
        {
            yield return new ViewCase(ViewInventory.CancellationIndex, state, model);
        }

        yield return new ViewCase(ViewInventory.CancellationCancelled, "cancelled", NoModel);
        yield return new ViewCase(ViewInventory.CancellationUnusable, "unusable", NoModel);

        yield return new ViewCase(ViewInventory.Catalogue, "two entries", new CatalogueModel
        {
            Entries =
            [
                new(BookingSubject.Service(new Guid("00000000-0000-0000-0000-000000000900")), "Massage"),
                new(BookingSubject.Resource(new Guid("00000000-0000-0000-0000-000000000001")), "Meeting Room A"),
            ],
        });

        yield return new ViewCase(
            ViewInventory.Catalogue, "nothing to book", new CatalogueModel { Entries = [] });

        // The catalogue's own preservation state — its loop is a separate copy of the
        // date form's (design D3), so the flow states above are no evidence about it.
        yield return new ViewCase(ViewInventory.Catalogue, "preserved query carried", new CatalogueModel
        {
            Entries =
            [
                new(BookingSubject.Service(new Guid("00000000-0000-0000-0000-000000000900")), "Massage"),
            ],
            PreservedQuery = PreservedPairs,
        });

        foreach (var view in new[]
        {
            "~/Views/Shared/Components/Booking/Confirmation.cshtml",
            "~/Views/Shared/Components/BookingFlow/Confirmation.cshtml",
        })
        {
            yield return new ViewCase(view, "with phone", Confirmation("07700 900123"));
            yield return new ViewCase(view, "no phone", Confirmation(null));

            // Placement under AutoConfirm off: the booking is stored as Requested and the
            // page must say received-not-confirmed rather than confirmed.
            yield return new ViewCase(view, "pending", Confirmation("07700 900123", isPending: true));
        }

        foreach (var view in new[]
        {
            "~/Views/Shared/Components/Booking/Unavailable.cshtml",
            "~/Views/Shared/Components/BookingFlow/Unavailable.cshtml",
        })
        {
            yield return new ViewCase(view, "unknown", BookingUnavailableModel.Unknown);
            yield return new ViewCase(
                view, "not offered on its own", BookingUnavailableModel.NotOfferedIndividually);
        }

        yield return new ViewCase(
            ViewInventory.ServiceConfirmation, "one resource", ServiceConfirmation(["Ada"], "07700 900123"));
        yield return new ViewCase(
            ViewInventory.ServiceConfirmation, "several resources, no phone",
            ServiceConfirmation(["Treatment Room", "Ada"], null));
        yield return new ViewCase(
            ViewInventory.ServiceConfirmation, "pending",
            ServiceConfirmation(["Treatment Room", "Ada"], "07700 900123", isPending: true));

        yield return new ViewCase(
            ViewInventory.ServiceUnavailable, "not fulfillable",
            ServiceUnavailableModel.NotFulfillable("Massage"));
        yield return new ViewCase(
            ViewInventory.ServiceUnavailable, "unknown", ServiceUnavailableModel.Unknown);
    }

    /// <summary>
    /// The states the shared partials are rendered across, for each of the two form
    /// models that implement their contract.
    /// </summary>
    private static IEnumerable<(string State, IBookingFormView Model)> FormStates()
    {
        // THE AVAILABLE-DATES BRANCHES. Every other state below carries the default list, in
        // which the selected date IS listed — so without these the empty-window wordings and
        // the outside-the-window statement would never render, and rule 3 would report the
        // view fully covered while three of its four branches had never been taken.
        yield return ("service: no dates at this length", Service(
            availableDates: [], longestInWindow: 30));
        yield return ("service: no dates at any length", Service(
            availableDates: [], longestInWindow: null));
        yield return ("service: selected date outside the window", Service(
            availableDates: [new AvailableDate(Today.AddDays(1), false)]));
        yield return ("resource: no dates at this length", Resource(
            availableDates: [], longestInWindow: 60));
        yield return ("resource: selected date outside the window", Resource(
            availableDates: [new AvailableDate(Today.AddDays(2), false)]));

        // THE PRIVACY NOTICE'S FOUR COMBINATIONS, first because they are the ones most
        // easily left unexercised: every other state below leaves the notice in its default
        // shape, so without these the configured-period and policy-link branches would never
        // render and rule 3 would report the view fully covered.
        //
        // The default install — no period, no link — is deliberately NOT among these four as a
        // special case: it is what every other state in this method already renders, which is
        // correct, because it is what every site renders until somebody configures something.
        yield return ("service: retention period stated", Service(
            privacyNotice: new PrivacyNoticeView(90, null, false)));
        yield return ("service: retention of one day", Service(
            privacyNotice: new PrivacyNoticeView(1, null, false)));
        yield return ("service: policy link, no period", Service(
            privacyNotice: new PrivacyNoticeView(null, "/privacy", false)));
        yield return ("service: period and policy link", Service(
            privacyNotice: new PrivacyNoticeView(90, "https://example.com/privacy", false)));
        yield return ("resource: period and policy link", Resource(
            privacyNotice: new PrivacyNoticeView(30, "/privacy", false)));

        // THE SENDING STATE, in both flows. The purpose sentence and the email field's hint both
        // change with it, and they are separate views — so a fixture for one would leave the other
        // rendering only its silent branch while rule 3 reported the view fully covered.
        yield return ("service: a confirmation will be sent", Service(
            privacyNotice: new PrivacyNoticeView(null, null, true)));
        yield return ("resource: a confirmation will be sent", Resource(
            privacyNotice: new PrivacyNoticeView(null, null, true)));
        yield return ("service: sends, with a period and a link", Service(
            privacyNotice: new PrivacyNoticeView(90, "/privacy", true)));

        yield return ("service: times, no errors", Service());
        yield return ("service: no times", Service(times: []));
        yield return ("service: length is the problem", Service(times: [], longest: 60));
        yield return ("service: with errors", Service(errors:
        [
            new BookingError("Please enter your name.", BookingFieldIds.Name),
            new BookingError("That booking length is too long.", BookingFieldIds.Duration),
            new BookingError("Ada is not available at the time you chose.", BookingFieldIds.Resource),
        ]));
        yield return ("service: fixed length", Service(lengthIsFixed: true));

        // A settled length AND a rejection of it. Reachable through a hand-made
        // POST, which is the whole reason the settled-length branch carries the
        // duration control's id at all. QA found that no state reached that
        // branch's error span — rule 3 could not see it, because the sibling
        // branch emits the identical markup.
        yield return ("service: fixed length rejected", Service(
            lengthIsFixed: true,
            errors: [new BookingError("That booking length is not offered.", BookingFieldIds.Duration)]));

        // Errors with no times at all. The composed document renders the booker
        // fields only when there are times, exactly as both flow views do, so this
        // is the state in which an error could link into a control that is not on
        // the page — the masking QA identified in the composition.
        yield return ("service: errors and no times", Service(
            times: [],
            errors: [new BookingError("Please enter your name.", BookingFieldIds.Name)]));
        yield return ("service: offers a choice", Service(choices: Choices));
        yield return ("service: choice made", Service(choices: Choices, chosen: Choices[0].Id));
        yield return ("service: choice of several", Service(choices: Choices, chosen: Choices[0].Id, count: 2));
        // The refused-pin redraw: a choice control AND an error against it. Added
        // because rule 3 found that no state reached the who-control's error branch
        // — a fixture gap rather than a dead branch, and this is the page a visitor
        // sees when the person they chose was taken between load and submit.
        yield return ("service: refused choice", Service(
            choices: Choices,
            chosen: Choices[0].Id,
            errors: [new BookingError("Ada is not available at the time you chose.", BookingFieldIds.Resource)]));

        // The conflict redraw: times AND an error against the time list. Found the
        // same way, and the page a visitor sees when their slot went while they were
        // filling the form in.
        yield return ("service: time no longer available", Service(
            errors: [new BookingError("That time is no longer available.", BookingFieldIds.Times)]));

        // The same conflict with NOTHING left that day — the most likely real
        // instance of the fault the summary-link guard was written for, and the one
        // `default-frontend`'s own scenario names: "a time that became unavailable
        // between rendering and submission produces a clear message with refreshed
        // availability". Refreshed availability can be empty.
        //
        // QA found this path untested: adding `|| fieldId == BookingFieldIds.Times`
        // to the guard passed all 260 tests, because every Times error in the
        // fixtures came with times still present.
        yield return ("service: time taken and none left", Service(
            times: [],
            errors: [new BookingError("That time is no longer available.", BookingFieldIds.Times)]));

        // The three states that make every arm of the summary's "is this control on
        // the page" rule mutation-detectable. QA found three of five arms silently
        // mutable — two of them in the OPPOSITE direction from the fault that was
        // fixed: a working association quietly dropped rather than a dangling one
        // emitted.
        //
        // Both are reachable. A hand-made POST with a disallowed length, or a
        // refused pin, on a date whose availability has since emptied.
        yield return ("service: length rejected and none left", Service(
            times: [],
            errors: [new BookingError("That booking length is not offered.", BookingFieldIds.Duration)]));

        yield return ("service: refused choice and none left", Service(
            times: [],
            errors: [new BookingError(
                "Your choice is no longer offered for this service.", BookingFieldIds.Resource)]));

        // The default arm, added last round precisely so a sixth field id could not
        // inherit `HasTimes` semantics by silence — and then exercised by nothing.
        // A failure may carry any field id, so an unrecognised one is a real shape.
        yield return ("service: error against an unknown control", Service(
            errors: [new BookingError("Something else went wrong.", "ubookit-not-a-control")]));

        yield return ("service: choice reset", Service(choices: Choices, wasReset: true));
        yield return ("service: choice reset with no control", Service(wasReset: true));
        yield return ("service: entered details", Service(
            name: "Ada Lovelace", email: "ada@example.com", phone: "07700 900123",
            selectedTimeIso: Times[0].InstantIso));
        yield return ("service: via the catalogue", Service(flowToken: "s:00000000-0000-0000-0000-000000000900"));

        // THE PRESERVATION STATES. Every other state leaves PreservedQuery empty —
        // which is the default install and stays the common rendering — so without
        // these the hidden-input loop would never run and the member would read as
        // dead. One state per flow, because the forms render the loop independently
        // (design D3: inlined, not shared) and a defect in one is invisible to the
        // other's state.
        yield return ("service: preserved query carried", Service(preservedQuery: PreservedPairs));
        yield return ("resource: preserved query carried", Resource(preservedQuery: PreservedPairs));

        // The resource flow's states mirror the service flow's wherever
        // `BookingFormModel` can express them. It cannot express a choice control
        // (`ResourceChoices` is structurally empty — a directly booked resource IS
        // the resource chosen) nor a fixed length (`LengthIsFixed` is always false),
        // so the choice and settled-length states have no resource counterpart. That
        // is the model being narrower, not the coverage being thinner.
        //
        // Every other state does have one, and until this change none of them
        // existed: the resource flow reached the document rules through five states
        // where the service flow reached them through nineteen, on partials the two
        // flows SHARE. A defect live only in a resource-flow state would have been
        // invisible.
        yield return ("resource: times, no errors", Resource());
        yield return ("resource: no times", Resource(times: []));
        yield return ("resource: length is the problem", Resource(times: [], longest: 60));
        yield return ("resource: with errors", Resource(errors:
        [
            new BookingError("Please enter a valid email address.", BookingFieldIds.Email),
        ]));

        // Errors with no times: the state in which an error can link into a booker
        // field the page never rendered. This is the exact fault the hand-built
        // composition concealed, and the resource flow had no state for it.
        yield return ("resource: errors and no times", Resource(
            times: [],
            errors: [new BookingError("Please enter your name.", BookingFieldIds.Name)]));

        yield return ("resource: time no longer available", Resource(
            errors: [new BookingError("That time is no longer available.", BookingFieldIds.Times)]));

        yield return ("resource: time taken and none left", Resource(
            times: [],
            errors: [new BookingError("That time is no longer available.", BookingFieldIds.Times)]));

        yield return ("resource: length rejected and none left", Resource(
            times: [],
            errors: [new BookingError("That booking length is not offered.", BookingFieldIds.Duration)]));

        yield return ("resource: error against an unknown control", Resource(
            errors: [new BookingError("Something else went wrong.", "ubookit-not-a-control")]));

        yield return ("resource: entered details", Resource(
            name: "Ada Lovelace", email: "ada@example.com", phone: "07700 900123",
            selectedTimeIso: Times[0].InstantIso));

        yield return ("resource: via the catalogue", Resource(flowToken: "r:00000000-0000-0000-0000-000000000001"));
    }

    private static ServiceFormModel Service(
        IReadOnlyList<BookingTimeOption>? times = null,
        IReadOnlyList<BookingError>? errors = null,
        IReadOnlyList<BookingResourceChoice>? choices = null,
        Guid? chosen = null,
        int count = 1,
        bool wasReset = false,
        bool lengthIsFixed = false,
        int? longest = null,
        string? name = null,
        string? email = null,
        string? phone = null,
        string? selectedTimeIso = null,
        string? flowToken = null,
        PrivacyNoticeView? privacyNotice = null,
        IReadOnlyList<AvailableDate>? availableDates = null,
        int? longestInWindow = null,
        IReadOnlyList<PreservedQueryPair>? preservedQuery = null)
        => new()
        {
            // Defaults to the DEFAULT INSTALL: no retention period and no policy link. A
            // fixture that defaulted to a configured period would exercise the branch most
            // sites never see and leave the common one untested.
            PrivacyNotice = privacyNotice ?? new PrivacyNoticeView(null, null, false),
            PreservedQuery = preservedQuery ?? [],
            AvailableDates = availableDates ?? Dates,
            SelectedDateIsListed = (availableDates ?? Dates).Any(date => date.IsSelected),
            WindowDays = 30,
            LongestAvailableInWindowMinutes = longestInWindow,
            ServiceId = new Guid("00000000-0000-0000-0000-000000000900"),
            ServiceName = "Massage",
            FlowToken = flowToken,
            SelectedDate = Today,
            MinDate = Today,
            MaxDate = Today.AddDays(90),
            DurationMinutes = 60,
            DurationOptions = [30, 60, 90],
            LengthIsFixed = lengthIsFixed,
            LongestAvailableMinutes = longest,
            Times = times ?? Times,
            ResourceChoices = choices ?? [],
            ChosenResourceId = chosen,
            ResourceChoiceCount = count,
            ResourceChoiceWasReset = wasReset,
            SelectedTimeIso = selectedTimeIso,
            Name = name,
            Email = email,
            Phone = phone,
            Errors = errors ?? [],
        };

    private static BookingFormModel Resource(
        IReadOnlyList<BookingTimeOption>? times = null,
        IReadOnlyList<BookingError>? errors = null,
        int? longest = null,
        string? name = null,
        string? email = null,
        string? phone = null,
        string? selectedTimeIso = null,
        string? flowToken = null,
        PrivacyNoticeView? privacyNotice = null,
        IReadOnlyList<AvailableDate>? availableDates = null,
        int? longestInWindow = null,
        IReadOnlyList<PreservedQueryPair>? preservedQuery = null)
        => new()
        {
            /// <inheritdoc cref="Service" />
            PrivacyNotice = privacyNotice ?? new PrivacyNoticeView(null, null, false),
            PreservedQuery = preservedQuery ?? [],
            AvailableDates = availableDates ?? Dates,
            SelectedDateIsListed = (availableDates ?? Dates).Any(date => date.IsSelected),
            WindowDays = 30,
            LongestAvailableInWindowMinutes = longestInWindow,
            ResourceId = new Guid("00000000-0000-0000-0000-000000000001"),
            ResourceName = "Meeting Room A",
            FlowToken = flowToken,
            SelectedDate = Today,
            MinDate = Today,
            MaxDate = Today.AddDays(90),
            DurationMinutes = 60,
            DurationOptions = [30, 60, 90],
            LongestAvailableMinutes = longest,
            Times = times ?? Times,
            SelectedTimeIso = selectedTimeIso,
            Name = name,
            Email = email,
            Phone = phone,
            Errors = errors ?? [],
        };

    /// <summary>The confirmation fixture, exposed so a rule suite can render it directly.</summary>
    public static BookingConfirmationModel ConfirmationModel() => Confirmation("07700 900123");

    /// <summary>The service confirmation fixture, likewise.</summary>
    public static ServiceConfirmationModel ServiceConfirmationModel()
        => ServiceConfirmation(["Treatment Room", "Ada"], "07700 900123");

    private static BookingConfirmationModel Confirmation(string? phone, bool isPending = false)
        => new()
        {
            BookingId = new Guid("00000000-0000-0000-0000-0000000000b1"),
            Reference = "7QX4-M2NP",
            IsPending = isPending,
            ResourceName = "Meeting Room A",
            LocalStart = "Thursday 20 August 2026, 09:00",
            LocalEnd = "10:00",
            BookerName = "Ada Lovelace",
            BookerEmail = "ada@example.com",
            BookerPhone = phone,
        };

    private static ServiceConfirmationModel ServiceConfirmation(
        string[] resources, string? phone, bool isPending = false)
        => new()
        {
            BookingId = new Guid("00000000-0000-0000-0000-0000000000b2"),
            Reference = "5KGT-BW9D",
            ServiceName = "Massage",
            IsPending = isPending,
            ResourceNames = resources,
            LocalStart = "Thursday 20 August 2026, 09:00",
            LocalEnd = "10:00",
            BookerName = "Ada Lovelace",
            BookerEmail = "ada@example.com",
            BookerPhone = phone,
        };
}
