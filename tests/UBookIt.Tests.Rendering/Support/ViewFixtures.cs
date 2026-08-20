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
/// One rendered <b>document</b>: the parts that make up a page, in the order a
/// flow renders them.
/// <para>
/// Rule 1 is a document-level rule — ids are unique within a document, and an
/// aria reference resolves against a document — so it must be asked of a
/// document. A shared partial is not one. The four form a page together, and
/// `_DateAndLength` deliberately describes its length control with an id that
/// `_Times` owns; rendered apart that reference dangles, rendered together it
/// resolves, and the second is what the site serves (design D7).
/// </para>
/// </summary>
public sealed record DocumentCase(string Name, string State, IReadOnlyList<ViewCase> Parts)
{
    /// <summary>
    /// Names the views the document was built from, not just the document, so a
    /// failure says which file to open. The spec's scenarios all say the check
    /// "fails, naming the view"; for a composed document that means naming its
    /// parts.
    /// </summary>
    public override string ToString()
        => Parts.Count == 1
            ? $"{Name} [{State}]"
            : $"{Name} ({string.Join(" + ", Parts.Select(p => p.ViewPath[(p.ViewPath.LastIndexOf('/') + 1)..]))}) [{State}]";
}

/// <summary>
/// The model states each in-scope view is rendered across.
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
    /// The views that are fragments rather than pages. They are included only by the
    /// three deferred flow views, so nothing in this suite renders them as a page —
    /// and rule 1 asks a question only a page can answer.
    /// </summary>
    public static IReadOnlyList<string> Partials { get; } =
    [
        ViewInventory.ErrorSummary,
        ViewInventory.DateAndLength,
        ViewInventory.Times,
        ViewInventory.YourDetails,
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

    private static IEnumerable<DocumentCase> BuildDocuments()
    {
        // The shared partials, composed as `Service.cshtml` composes them:
        // the error summary, then the date-and-length GET form, then the times and
        // the booker fields inside the POST form. Concatenation preserves exactly
        // what this rule asks about — id uniqueness and reference resolution across
        // the whole document.
        foreach (var (state, model) in FormStates())
        {
            var parts = new List<ViewCase>
            {
                new(ViewInventory.ErrorSummary, state, model),
                new(ViewInventory.DateAndLength, state, model),
                new(ViewInventory.Times, state, model),
            };

            // The booker fields only where the flow renders them. BOTH flow views
            // guard `_YourDetails` with `@if (Model.HasTimes)` — `Service.cshtml`
            // and `Booking/Default.cshtml` alike — and composing it unconditionally
            // made this document something neither flow produces.
            //
            // It masked rather than over-constrained, which is the worse direction:
            // an error against a booker field, on a page with no times, would link
            // into a control the real page never rendered, and a document that
            // always includes those fields always resolves it. The "errors and no
            // times" state above is that case, and it is honest only with this
            // guard in place.
            if (model.HasTimes)
            {
                parts.Add(new ViewCase(ViewInventory.YourDetails, state, model));
            }

            yield return new DocumentCase("the shared partials", state, parts);
        }

        // Every other in-scope view is a page in its own right.
        foreach (var rendered in All.Where(c => !Partials.Contains(c.ViewPath)))
        {
            yield return new DocumentCase(rendered.ViewPath, rendered.State, [rendered]);
        }
    }

    private static IEnumerable<ViewCase> Build()
    {
        foreach (var partial in new[]
        {
            ViewInventory.DateAndLength,
            ViewInventory.Times,
            ViewInventory.ErrorSummary,
            ViewInventory.YourDetails,
        })
        {
            foreach (var (state, model) in FormStates())
            {
                yield return new ViewCase(partial, state, model);
            }
        }

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

        foreach (var view in new[]
        {
            "~/Views/Shared/Components/Booking/Confirmation.cshtml",
            "~/Views/Shared/Components/BookingFlow/Confirmation.cshtml",
        })
        {
            yield return new ViewCase(view, "with phone", Confirmation("07700 900123"));
            yield return new ViewCase(view, "no phone", Confirmation(null));
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

        yield return ("service: choice reset", Service(choices: Choices, wasReset: true));
        yield return ("service: choice reset with no control", Service(wasReset: true));
        yield return ("service: entered details", Service(
            name: "Ada Lovelace", email: "ada@example.com", phone: "07700 900123",
            selectedTimeIso: Times[0].InstantIso));
        yield return ("service: via the catalogue", Service(flowToken: "s:00000000-0000-0000-0000-000000000900"));

        yield return ("resource: times, no errors", Resource());
        yield return ("resource: no times", Resource(times: []));
        yield return ("resource: length is the problem", Resource(times: [], longest: 60));
        yield return ("resource: with errors", Resource(errors:
        [
            new BookingError("Please enter a valid email address.", BookingFieldIds.Email),
        ]));
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
        string? flowToken = null)
        => new()
        {
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
        string? flowToken = null)
        => new()
        {
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
            Errors = errors ?? [],
        };

    private static BookingConfirmationModel Confirmation(string? phone)
        => new()
        {
            BookingId = new Guid("00000000-0000-0000-0000-0000000000b1"),
            ResourceName = "Meeting Room A",
            LocalStart = "Thursday 20 August 2026, 09:00",
            LocalEnd = "10:00",
            BookerName = "Ada Lovelace",
            BookerEmail = "ada@example.com",
            BookerPhone = phone,
        };

    private static ServiceConfirmationModel ServiceConfirmation(string[] resources, string? phone)
        => new()
        {
            BookingId = new Guid("00000000-0000-0000-0000-0000000000b2"),
            ServiceName = "Massage",
            ResourceNames = resources,
            LocalStart = "Thursday 20 August 2026, 09:00",
            LocalEnd = "10:00",
            BookerName = "Ada Lovelace",
            BookerEmail = "ada@example.com",
            BookerPhone = phone,
        };
}
