using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;
using UBookIt.Web;
using UBookIt.Web.Rendering;
using Umbraco.Cms.Core.Mail;

namespace UBookIt.Tests;

/// <summary>
/// The single-choice skip, driven through the REAL <c>BookingFlow</c> component — activated by
/// MVC's own view-component activator, over the real flows and catalogue, reading a real request.
/// </summary>
/// <remarks>
/// <para>
/// <b>Activation is part of what is under test, not set-up.</b> The component gained a second
/// constructor taking the site settings, and only <c>[ActivatorUtilitiesConstructor]</c> makes MVC
/// choose it. Constructing the component by hand here would prove nothing about which constructor
/// a site gets — and the published four-argument one silently skips (it has no settings), so a
/// wrong choice is invisible with the setting ON. Every test therefore activates through
/// <see cref="IViewComponentActivator"/>, and <see cref="The_setting_off_shows_a_catalogue_of_one"/>
/// is the one that fails if the activator picks the old constructor.
/// </para>
/// <para>
/// <b>What is asserted is the component's decision: which view, with which model.</b> That those
/// views render a heading naming the thing booked, and the flow token as the hidden <c>Subject</c>
/// field, is the rendering suite's to prove of the shipped views, and it does; the PRG redirect's
/// use of that field is <see cref="BookingFlowLink.AfterSubmission"/>'s, tested where it lives.
/// The one seam between them this change creates — the token on the model — is asserted here.
/// </para>
/// </remarks>
public class SingleChoiceDispatchTests
{
    private static WeeklyOpenHours EveryDay()
        => TestData.Weekly(
            "08:00", "18:00",
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday);

    private static Resource TreatmentRoom(bool directlyBookable, AvailabilityConfiguration? availability = null)
        => Resource.Create(
            "room",
            "Treatment Room",
            directlyBookable: directlyBookable,
            availability: availability ?? TestData.Config(EveryDay()),
            id: Guid.NewGuid()).Value;

    private static Resource MeetingRoom(AvailabilityConfiguration? availability = null)
        => Resource.Create(
            "room",
            "Meeting Room A",
            directlyBookable: true,
            availability: availability ?? TestData.Config(EveryDay()),
            id: Guid.NewGuid()).Value;

    private static Service Massage()
        => Service.Create(
            "Massage",
            duration: null,
            roles: [new ServiceRole("room", 1)],
            id: Guid.NewGuid()).Value;

    /// <summary>A site: what it offers, its skip setting, and a request to the booking page.</summary>
    private sealed class Site
    {
        private readonly InMemoryServiceStore _services = new();
        private readonly InMemoryResourceStore _resources = new();

        public Site With(Service service)
        {
            _services.Add(service);
            return this;
        }

        public Site With(Resource resource)
        {
            _resources.Add(resource);
            return this;
        }

        public async Task<IViewComponentResult> InvokeAsync(
            bool skip,
            string query = "",
            Guid? serviceId = null,
            Guid? resourceId = null,
            Action<ITempDataDictionary>? tempData = null)
        {
            var settings = TestData.Settings with { SkipSingleChoiceCatalogue = skip };
            var time = new FixedTimeProvider(TestData.Now);
            var (serviceBooking, _, _) = TestData.ServiceBookingWith(_services, _resources);

            var services = new ServiceCollection();
            services.AddMvcCore().AddViews();
            services.AddSingleton<IServiceStore>(_services);
            services.AddSingleton<IResourceStore>(_resources);
            services.AddSingleton<IServiceBookingService>(serviceBooking);
            services.AddSingleton<IAvailabilityQueryService>(
                new AvailabilityService(_resources, new InMemoryBookingStore(), time, settings));
            services.AddSingleton(settings);
            services.AddSingleton<TimeProvider>(time);
            services.AddSingleton<IEmailSender>(new TestEmailSender());
            services.AddSingleton(new FrontendSettings());
            services.AddScoped<ResourceBookingFlow>();
            services.AddScoped<ServiceBookingFlow>();
            services.AddScoped<BookingCatalogue>();

            var provider = services.BuildServiceProvider();
            var httpContext = new DefaultHttpContext { RequestServices = provider };
            httpContext.Request.QueryString = new QueryString(query);

            var temp = new TempDataDictionary(httpContext, new NoTempDataProvider());
            tempData?.Invoke(temp);

            var context = new ViewComponentContext
            {
                ViewComponentDescriptor = new ViewComponentDescriptor
                {
                    TypeInfo = typeof(BookingFlowViewComponent).GetTypeInfo(),
                },
                ViewContext = new ViewContext
                {
                    HttpContext = httpContext,
                    TempData = temp,
                    ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
                },
            };

            // MVC's own activator, in an AddMvcCore container rather than Umbraco's host: the same
            // component-activation code a site runs, not the site's whole container. That the
            // site's container reaches the settings constructor was seen live (task 3.5: stored
            // off rendered a catalogue of one).
            var component = (BookingFlowViewComponent)provider
                .GetRequiredService<IViewComponentActivator>()
                .Create(context);
            component.ViewComponentContext = context;

            return await component.InvokeAsync(serviceId, resourceId);
        }
    }

    private sealed class NoTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private static (string ViewName, object? Model) Rendered(IViewComponentResult result)
    {
        var view = Assert.IsType<ViewViewComponentResult>(result);
        return (view.ViewName!, view.ViewData?.Model);
    }

    // ---- skipped -----------------------------------------------------------------------------

    [Fact]
    public async Task A_catalogue_of_one_service_is_skipped_to_its_date_step()
    {
        var massage = Massage();
        var site = new Site().With(massage).With(TreatmentRoom(directlyBookable: false));

        var (view, model) = Rendered(await site.InvokeAsync(skip: true));

        Assert.Equal("Service", view);
        var form = Assert.IsType<ServiceFormModel>(model);
        Assert.Equal(massage.Id, form.ServiceId);
        Assert.Equal("Massage", form.ServiceName);
    }

    [Fact]
    public async Task A_catalogue_of_one_resource_is_skipped_to_its_booking_step()
    {
        var room = MeetingRoom();
        var site = new Site().With(room);

        var (view, model) = Rendered(await site.InvokeAsync(skip: true));

        Assert.Equal("Default", view);
        var form = Assert.IsType<BookingFormModel>(model);
        Assert.Equal(room.Id, form.ResourceId);
        Assert.Equal("Meeting Room A", form.ResourceName);
    }

    [Fact]
    public async Task A_skipped_service_carries_its_subject_into_the_next_step()
    {
        // As if CHOSEN, not as if author-named: the token is what the date step's form and the
        // POST's Subject field carry, so every later URL names what is being booked.
        var massage = Massage();
        var site = new Site().With(massage).With(TreatmentRoom(directlyBookable: false));

        var (_, model) = Rendered(await site.InvokeAsync(skip: true));

        Assert.Equal(BookingSubject.Service(massage.Id).Token, Assert.IsType<ServiceFormModel>(model).FlowToken);
    }

    [Fact]
    public async Task A_skipped_resource_carries_its_subject_into_the_next_step()
    {
        var room = MeetingRoom();
        var site = new Site().With(room);

        var (_, model) = Rendered(await site.InvokeAsync(skip: true));

        Assert.Equal(BookingSubject.Resource(room.Id).Token, Assert.IsType<BookingFormModel>(model).FlowToken);
    }

    [Fact]
    public async Task The_token_a_skipped_flow_carries_is_one_the_redirect_after_submission_keeps()
    {
        // The seam this change creates, followed one step: the token on the model is what the
        // view posts back as Subject, and AfterSubmission is the PRG target built from it. A token
        // the redirect discarded would land the confirmation back on a bare page.
        var massage = Massage();
        var site = new Site().With(massage).With(TreatmentRoom(directlyBookable: false));
        var form = Assert.IsType<ServiceFormModel>(Rendered(await site.InvokeAsync(skip: true)).Model);

        var query = BookingFlowLink.AfterSubmission(
            BookingSubject.Agreeing(form.FlowToken, BookingSubject.Service(form.ServiceId)),
            form.SelectedDate,
            form.DurationMinutes,
            chosenResourceId: null,
            preserved: []);

        Assert.NotNull(query);
        Assert.Contains($"{BookingKeys.SubjectQuery}={Uri.EscapeDataString(form.FlowToken!)}", query.Value.Value);
    }

    [Fact]
    public async Task A_skipped_flow_is_the_same_flow_as_a_chosen_one()
    {
        // Same view, same model, field for field: reaching the flow by the skip and reaching it by
        // the catalogue's own link (?ubBook=…) must not diverge.
        var massage = Massage();
        var site = new Site().With(massage).With(TreatmentRoom(directlyBookable: false));
        var token = BookingSubject.Service(massage.Id).Token;

        var skipped = Rendered(await site.InvokeAsync(skip: true));
        var chosen = Rendered(await site.InvokeAsync(skip: true, query: $"?{BookingKeys.SubjectQuery}={token}"));

        Assert.Equal(chosen.ViewName, skipped.ViewName);
        Assert.Equivalent(chosen.Model, skipped.Model, strict: true);
    }

    [Fact]
    public async Task A_skipped_flow_shows_its_confirmation()
    {
        // After the PRG redirect the confirmation is read in the flow's own branch. Reached with
        // no subject at all — a redirect that had lost the token — the skip still lands there.
        var massage = Massage();
        var site = new Site().With(massage).With(TreatmentRoom(directlyBookable: false));

        var (view, _) = Rendered(await site.InvokeAsync(
            skip: true,
            tempData: temp => temp.Stash(BookingKeys.ServiceConfirmation, new ServiceConfirmationModel
            {
                BookingId = Guid.NewGuid(),
                Reference = "ABCD-EFGH",
                ServiceName = "Massage",
                ResourceNames = ["Treatment Room"],
                LocalStart = "10:00",
                LocalEnd = "11:00",
                BookerName = "Test Person",
                BookerEmail = "test@example.com",
            })));

        Assert.Equal("ServiceConfirmation", view);
    }

    [Fact]
    public async Task A_resource_not_offered_on_its_own_does_not_count()
    {
        var massage = Massage();
        var site = new Site().With(massage).With(TreatmentRoom(directlyBookable: false));

        var (view, model) = Rendered(await site.InvokeAsync(skip: true));

        Assert.Equal("Service", view);
        Assert.Equal(massage.Id, Assert.IsType<ServiceFormModel>(model).ServiceId);
    }

    [Fact]
    public async Task A_sole_choice_without_availability_is_still_skipped_to()
    {
        // Counted over the catalogue, never over the calendar: a room with no opening hours at all
        // leads to its own flow, which explains the absence itself.
        var closed = MeetingRoom(TestData.Config(WeeklyOpenHours.Empty));
        var site = new Site().With(closed);

        var (view, model) = Rendered(await site.InvokeAsync(skip: true));

        Assert.Equal("Default", view);
        Assert.Equal(closed.Id, Assert.IsType<BookingFormModel>(model).ResourceId);
    }

    // ---- not skipped -------------------------------------------------------------------------

    [Fact]
    public async Task The_setting_off_shows_a_catalogue_of_one()
    {
        // THE ACTIVATION GUARD (design D4). The published four-argument constructor has no
        // settings and skips; only the settings constructor can honour "off". If MVC activated the
        // old one, this renders the service flow and fails.
        var site = new Site().With(Massage()).With(TreatmentRoom(directlyBookable: false));

        var (view, model) = Rendered(await site.InvokeAsync(skip: false));

        Assert.Equal("Catalogue", view);
        Assert.Single(Assert.IsType<CatalogueModel>(model).Entries);
    }

    [Fact]
    public async Task Two_choices_show_the_catalogue()
    {
        var site = new Site().With(Massage()).With(TreatmentRoom(directlyBookable: false)).With(MeetingRoom());

        var (view, model) = Rendered(await site.InvokeAsync(skip: true));

        Assert.Equal("Catalogue", view);
        Assert.Equal(2, Assert.IsType<CatalogueModel>(model).Entries.Count);
    }

    [Fact]
    public async Task An_empty_catalogue_is_shown_as_it_is_today()
    {
        var (view, model) = Rendered(await new Site().InvokeAsync(skip: true));

        Assert.Equal("Catalogue", view);
        Assert.False(Assert.IsType<CatalogueModel>(model).HasEntries);
    }

    // ---- a named subject wins ----------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_author_named_subject_wins_whatever_the_setting_says(bool skip)
    {
        // Two things on offer, the author names the second: neither the catalogue nor the skip
        // gets a say, and an author-named subject still carries no token, exactly as before.
        var room = MeetingRoom();
        var site = new Site().With(Massage()).With(TreatmentRoom(directlyBookable: false)).With(room);

        var (view, model) = Rendered(await site.InvokeAsync(skip, resourceId: room.Id));

        Assert.Equal("Default", view);
        var form = Assert.IsType<BookingFormModel>(model);
        Assert.Equal(room.Id, form.ResourceId);
        Assert.Null(form.FlowToken);
    }

    // ---- a named subject wins ON A CATALOGUE OF ONE -------------------------------------------
    //
    // FOUND BY QA (round 1): the two tests above use a site with TWO entries, where the skip can
    // never fire, so neither could tell "the named subject wins" from "the skip overrides it". A
    // dispatcher that computed the sole entry BEFORE looking at the subject passed every suite.
    // These name a DIFFERENT thing from the sole entry, on a site where the skip WOULD fire.
    // The thing named is the service-only room: the only other bookable thing a one-entry site
    // can have. Its flow reports it as not offered on its own — a resource-branch outcome, which
    // is precisely what the skip, choosing the service, could never produce.

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_author_named_subject_beats_the_skip_on_a_catalogue_of_one(bool skip)
    {
        var room = TreatmentRoom(directlyBookable: false);
        var site = new Site().With(Massage()).With(room);

        var (view, model) = Rendered(await site.InvokeAsync(skip, resourceId: room.Id));

        Assert.Equal("Unavailable", view);
        Assert.IsType<BookingUnavailableModel>(model);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_subject_in_the_url_beats_the_skip_on_a_catalogue_of_one(bool skip)
    {
        var room = TreatmentRoom(directlyBookable: false);
        var site = new Site().With(Massage()).With(room);
        var token = BookingSubject.Resource(room.Id).Token;

        var (view, model) = Rendered(await site.InvokeAsync(skip, query: $"?{BookingKeys.SubjectQuery}={token}"));

        Assert.Equal("Unavailable", view);
        Assert.IsType<BookingUnavailableModel>(model);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_subject_in_the_url_wins_whatever_the_setting_says(bool skip)
    {
        var room = MeetingRoom();
        var site = new Site().With(Massage()).With(TreatmentRoom(directlyBookable: false)).With(room);
        var token = BookingSubject.Resource(room.Id).Token;

        var (view, model) = Rendered(await site.InvokeAsync(skip, query: $"?{BookingKeys.SubjectQuery}={token}"));

        Assert.Equal("Default", view);
        Assert.Equal(token, Assert.IsType<BookingFormModel>(model).FlowToken);
    }
}
