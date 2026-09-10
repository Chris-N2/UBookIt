using System.Text.RegularExpressions;
using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Tests.Support;
using UBookIt.Web.Rendering;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;

namespace UBookIt.Tests;

/// <summary>
/// That the notice and the retention sweep read one value — asserted over the paths that build
/// a form, not over the factory they are supposed to call.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written because the first attempt was unfalsifiable, and QA proved it.</b> The only test
/// for this guarantee constructed a <see cref="SiteBookingSettings"/>, called
/// <c>PrivacyNoticeView.From</c>, and asserted the two properties matched. Replacing
/// <c>ServiceBookingFlow</c>'s call with <c>new PrivacyNoticeView(30, …)</c> — a notice telling
/// every visitor "we keep them for 30 days" while the sweep acted on the configured period —
/// left all 2091 tests green.
/// </para>
/// <para>
/// That test proved <c>From</c> copies a field. The guarantee is about the path, and a guard
/// that watches a reconstruction of the thing it guards watches nothing — the same defect this
/// project found in a SQL-shape assertion one change earlier, and cited in the comment above the
/// test that then repeated it.
/// </para>
/// <para>
/// Two guards, because they fail for different reasons: the behavioural one below catches a flow
/// that builds the notice from the wrong value, and the source one catches a flow that builds it
/// from the right value by a second route — which would be correct today and is exactly how the
/// single source stops being single.
/// </para>
/// </remarks>
public class PrivacyNoticeSourceTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    /// <summary>A period no other fixture uses, so a hard-coded value cannot pass by coincidence.</summary>
    private const int DistinctivePeriod = 137;

    private static SiteBookingSettings Settings => TestData.Settings with
    {
        RetentionDays = DistinctivePeriod,
        PrivacyPolicyUrl = "/distinctive-policy",
    };

    [Fact]
    public async Task The_resource_flow_states_the_period_the_sweep_would_act_on()
    {
        var room = TestData.Room();
        var resources = new InMemoryResourceStore().Add(room);
        var bookings = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);
        var settings = Settings;

        var flow = new ResourceBookingFlow(
            resources,
            new AvailabilityService(resources, bookings, time, settings),
            settings,
            time, new TestEmailSender());

        var outcome = await flow.BuildAsync(
            room.Id,
            new BookingFlowInput { Date = Date, DurationMinutes = 60 });

        Assert.NotNull(outcome.Form);

        // Against the SETTINGS, not against the literal. Comparing to 137 would pass for a flow
        // that hard-coded 137; comparing to the settings instance is the guarantee.
        Assert.Equal(settings.RetentionDays, outcome.Form!.PrivacyNotice.RetentionDays);
        Assert.Equal(settings.PrivacyPolicyUrl, outcome.Form.PrivacyNotice.PolicyUrl);
    }

    /// <summary>
    /// THE SEAM, over BOTH flows. What the notice says about being contacted is decided by asking
    /// the host, live, from the flow — and until QA mutated it, nothing drove that path: replacing
    /// <c>HostMailAvailability.CanSend(emailSender)</c> with the literal <c>false</c> in both flows
    /// left all 2343 tests green.
    /// </summary>
    /// <remarks>
    /// <para>
    /// That mutant ships a site with sending configured and working mail whose form says only "so
    /// this site can contact you" — understating the processing on the very page collecting the
    /// address, which is the failure the privacy-notice requirement forbids in its other direction.
    /// </para>
    /// <para>
    /// <b>The predicate was tested pure and the views were tested from fixtures; the three lines
    /// joining them lived in neither.</b> Which is this project's recurring shape, and the reason
    /// this is a theory over both flows rather than a test of one: the previous change fixed both
    /// flows, tested one, and claimed both.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task What_the_flow_tells_the_visitor_comes_from_asking_the_host(bool hostCanSendMail)
    {
        var settings = TestData.Settings with
        {
            Notifications = new BookingNotificationSettings { SendBookerEmails = true },
        };

        var resourceNotice = await ResourceNoticeAsync(settings, new TestEmailSender(hostCanSendMail));
        var serviceNotice = await ServiceNoticeAsync(settings, new TestEmailSender(hostCanSendMail));

        // The SETTING is on in both cases, so anything that reads the setting alone passes both
        // rows. Only a flow that actually asks the host distinguishes them.
        Assert.Equal(hostCanSendMail, resourceNotice.SendsBookerEmail);
        Assert.Equal(hostCanSendMail, serviceNotice.SendsBookerEmail);
    }

    /// <summary>
    /// And the other conjunct, from the flow: a host that can send does not make a notice promise
    /// anything on a site that never asked for it.
    /// </summary>
    [Fact]
    public async Task A_host_that_can_send_does_not_promise_for_a_site_that_did_not_ask()
    {
        var settings = TestData.Settings;  // no notification settings at all

        Assert.False((await ResourceNoticeAsync(settings, new TestEmailSender(true))).SendsBookerEmail);
        Assert.False((await ServiceNoticeAsync(settings, new TestEmailSender(true))).SendsBookerEmail);
    }

    /// <summary>
    /// A host that throws when asked must not take the booking page down with it — the opposite
    /// choice from the sending path, and made deliberately because here the answer decides a
    /// sentence rather than a message. Until now the <c>catch</c> was executed by no test at all,
    /// so the claim was unproven in both halves: that the page survives, and that the sentence
    /// errs towards promising nothing rather than towards promising wrongly.
    /// </summary>
    [Fact]
    public async Task A_host_that_cannot_be_asked_does_not_break_the_form_and_promises_nothing()
    {
        var settings = TestData.Settings with
        {
            Notifications = new BookingNotificationSettings { SendBookerEmails = true },
        };

        var resourceNotice = await ResourceNoticeAsync(settings, new ThrowingEmailSender());
        var serviceNotice = await ServiceNoticeAsync(settings, new ThrowingEmailSender());

        Assert.False(resourceNotice.SendsBookerEmail);
        Assert.False(serviceNotice.SendsBookerEmail);
    }

    private static async Task<PrivacyNoticeView> ResourceNoticeAsync(
        SiteBookingSettings settings, IEmailSender emailSender)
    {
        var room = TestData.Room();
        var resources = new InMemoryResourceStore().Add(room);
        var time = new FixedTimeProvider(TestData.Now);

        var flow = new ResourceBookingFlow(
            resources,
            new AvailabilityService(resources, new InMemoryBookingStore(), time, settings),
            settings,
            time,
            emailSender);

        var outcome = await flow.BuildAsync(
            room.Id, new BookingFlowInput { Date = Date, DurationMinutes = 60 });

        Assert.NotNull(outcome.Form);

        return outcome.Form!.PrivacyNotice;
    }

    private static async Task<PrivacyNoticeView> ServiceNoticeAsync(
        SiteBookingSettings settings, IEmailSender emailSender)
    {
        var service = UBookIt.Core.Services.Service.Create(
            "Massage",
            duration: null,
            roles: [new UBookIt.Core.Services.ServiceRole("room", 1)]).Value;
        var serviceStore = new InMemoryServiceStore().Add(service);
        var resourceStore = new InMemoryResourceStore().Add(TestData.Room());
        var (core, _, _) = TestData.ServiceBookingWith(serviceStore, resourceStore);

        var flow = new ServiceBookingFlow(
            serviceStore, core, settings, new FixedTimeProvider(TestData.Now), emailSender);

        var outcome = await flow.BuildAsync(
            service.Id, new BookingFlowInput { Date = Date, DurationMinutes = 60 });

        Assert.NotNull(outcome.Form);

        return outcome.Form!.PrivacyNotice;
    }

    /// <summary>Stands in for Umbraco's default sender, which throws from every member.</summary>
    private sealed class ThrowingEmailSender : IEmailSender
    {
        public bool CanSendRequiredEmail()
            => throw new NotImplementedException("To send an Email ensure IEmailSender is implemented");

        public Task SendAsync(EmailMessage message, string emailType) => Task.CompletedTask;

        public Task SendAsync(EmailMessage message, string emailType, bool enableNotification)
            => Task.CompletedTask;

        public Task SendAsync(
            EmailMessage message, string emailType, bool enableNotification = false, TimeSpan? expires = null)
            => Task.CompletedTask;
    }

    [Fact]
    public void Retention_off_reaches_the_form_as_off_rather_than_as_a_number()
    {
        // The other direction, and the one a single equality check would let through: null must
        // survive as null. A flow that turned it into 0 would render "we keep them for 0 days",
        // which is a promise no site has made.
        var settings = TestData.Settings with { RetentionDays = null };

        var notice = PrivacyNoticeView.From(settings, hostCanSendMail: false);

        Assert.Null(notice.RetentionDays);
        Assert.False(notice.HasRetentionPeriod);
    }

    [Fact]
    public void The_notice_is_constructed_in_exactly_one_place()
    {
        // THE TRIPWIRE. The behavioural tests above catch a flow that states the WRONG value;
        // this catches a flow that states the right value by a SECOND ROUTE — which would pass
        // every assertion today and is precisely how "there is exactly one source" stops being
        // true without anything looking broken.
        //
        // Source-level because that is where the property lives: "one construction site" is not
        // observable from behaviour while every site happens to agree.
        //
        // Counted per file rather than classified as inside-or-outside the factory. Counting is
        // strictly stronger and far clearer: a second construction added to the factory's OWN
        // file would satisfy any "is it in the right file?" test, and it is just as much a second
        // source as one added to a flow.
        //
        // `.cshtml` IS SCANNED TOO. A view can reach settings through `@inject`, so "the scan
        // only looked at C#" would be a hole with a real shape rather than a theoretical one.
        var scanned = RepoFiles
            .Paths("src", "*.cs")
            .Concat(RepoFiles.Paths("src", "*.cshtml"))
            .Select(path => (Path: path, File: Relative(path)))
            .ToList();

        var constructions = scanned
            .Select(x => (x.File, Count: ConstructionsIn(File.ReadAllText(x.Path))))
            .Where(x => x.Count > 0)
            .OrderBy(x => x.File, StringComparer.Ordinal)
            .ToList();

        // Exactly one file, constructing exactly once, and it is the factory's.
        var only = Assert.Single(constructions);

        Assert.EndsWith("src/UBookIt.Web/Rendering/BookingFormView.cs", only.File, StringComparison.Ordinal);
        Assert.Equal(1, only.Count);

        // THE SCAN'S OWN ESCAPE HATCH, CLOSED — and the first attempt at closing it did not.
        //
        // Narrowing the root from "src" to "src/UBookIt.Web/Rendering" left this test green while
        // hiding every construction outside one folder. An emptied scan fails on Assert.Single,
        // but a NARROWED one did not. Named files rather than a count floor, because a floor is a
        // number somebody lowers.
        //
        // The first list named three files that ALL LIVE IN `Rendering` — so the narrowing kept
        // every one of them and the guard stayed green against the very mutation it was written
        // for. A required-file list only closes narrowing if its members are spread across the
        // roots somebody might narrow to, which is why Core and Persistence are named here even
        // though neither constructs the type: what they are doing in this list is being
        // ELSEWHERE.
        foreach (var required in new[]
        {
            "src/UBookIt.Web/Rendering/BookingFormView.cs",
            "src/UBookIt.Web/Rendering/ResourceBookingFlow.cs",
            "src/UBookIt.Web/Rendering/ServiceBookingFlow.cs",
            "src/UBookIt.Web/Views/Shared/UBookIt/_PrivacyNotice.cshtml",
            "src/UBookIt.Core/SiteBookingSettings.cs",
            "src/UBookIt.Persistence/Composing/UBookItPersistenceComposer.cs",
        })
        {
            Assert.Contains(
                scanned,
                x => x.File.EndsWith(required, StringComparison.Ordinal));
        }
    }

    private static string Relative(string path)
    {
        var normalised = path.Replace('\\', '/');

        return normalised[(normalised.IndexOf("/src/", StringComparison.Ordinal) + 1)..];
    }

    /// <summary>
    /// How many times a source file produces a <see cref="PrivacyNoticeView"/> — by construction
    /// or by <c>with</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>with</c> is a second construction and carries no <c>new</c>.</b> QA proved the first
    /// version blind to it: replacing a flow's <c>PrivacyNoticeView.From(settings)</c> with
    /// <c>PrivacyNoticeView.From(settings) with { RetentionDays = … }</c> left all 2104 tests
    /// green. The type is a record, so <c>with</c> is available to every caller and is the
    /// idiomatic way to clone one — which put the most natural form of the exact case this guard
    /// exists to catch outside its reach.
    /// </para>
    /// <para>
    /// <b>What this still cannot see, stated rather than left to be discovered.</b> A <c>with</c>
    /// applied to a local — <c>var n = form.PrivacyNotice; var m = n with { … };</c> — names the
    /// type nowhere and no regex over source will find it. That is why this guard is
    /// defence-in-depth over the two behavioural tests above rather than a proof: those catch a
    /// flow stating the wrong value however it was built, and this catches a second construction
    /// route that would be correct today. Neither subsumes the other, and neither is complete.
    /// </para>
    /// </remarks>
    private static int ConstructionsIn(string source)
    {
        var news = Regex.Matches(source, @"new\s+PrivacyNoticeView\s*\(").Count;

        // A `with` in any statement that names the type. Split on `;` so a `with` elsewhere in
        // the file cannot be attributed to a mention of the type several statements away.
        var withs = source
            .Split(';')
            .Count(statement =>
                statement.Contains("PrivacyNoticeView", StringComparison.Ordinal)
                && Regex.IsMatch(statement, @"\bwith\b\s*\{"));

        return news + withs;
    }
}
