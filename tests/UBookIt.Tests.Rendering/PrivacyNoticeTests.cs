using UBookIt.Core;
using UBookIt.Tests.Rendering.Support;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// What the booking form tells a visitor about the personal data it collects.
/// </summary>
/// <remarks>
/// <para>
/// Rendered rather than inspected as source: the four statements are what a person reads, and a
/// test over the <c>.cshtml</c> would pass on markup that never reaches a page.
/// </para>
/// <para>
/// <b>The no-period case is the DEFAULT INSTALL</b>, so it is exercised first and everywhere.
/// Retention is off unless a site configures it, which means a suite that only ever seeded a
/// configured period would leave the branch most sites actually render untested — the fixture
/// trap that made a broken retention sweep pass its own paging test one change ago.
/// </para>
/// </remarks>
public class PrivacyNoticeTests
{
    private readonly ViewRenderer _renderer = new();

    private async Task<string> RenderAsync(
        int? retentionDays, string? policyUrl, bool sendsBookerEmail = false)
        => Flat(await _renderer.RenderAsync(
            ViewInventory.PrivacyNotice, Form(retentionDays, policyUrl, sendsBookerEmail)));

    /// <summary>
    /// Whitespace runs collapsed to single spaces, so a phrase can be matched however Razor
    /// happened to wrap it.
    /// </summary>
    /// <remarks>
    /// <b>Added because an absence guard here was defeated by exactly that, and the mutation
    /// proved it.</b> Re-adding "We will send your booking confirmation to that address" to the
    /// notice — by ADDITION, beside the correct sentence, wrapped across two source lines as the
    /// file's own 100-column style forces — left the guard green: the needle spans the break, and
    /// a raw substring check cannot see it. Every assertion in this file now goes through this,
    /// the positive halves included, so the two directions cannot use different matchers. That
    /// asymmetry — one test, two matchers — is what let an over-claim walk back into the
    /// documentation one change ago.
    /// </remarks>
    private static string Flat(string html)
        => System.Text.RegularExpressions.Regex.Replace(html, @"\s+", " ");

    private static IBookingFormView Form(
        int? retentionDays, string? policyUrl, bool sendsBookerEmail = false)
        => new BookingFormModel
        {
            PrivacyNotice = new PrivacyNoticeView(retentionDays, policyUrl, sendsBookerEmail),
            ResourceId = Guid.NewGuid(),
            ResourceName = "Meeting Room A",
            SelectedDate = new DateOnly(2026, 9, 15),
            MinDate = new DateOnly(2026, 9, 10),
            MaxDate = new DateOnly(2026, 12, 10),
            DurationMinutes = 60,
            DurationOptions = [30, 60],
            Times = [],
            Errors = [],
        };

    [Fact]
    public async Task All_four_statements_are_made()
    {
        // The requirement is that a visitor is told what, why, how long and who — so all four
        // are asserted together. Asserting them in four tests would let three pass while the
        // notice said three things, which is not what the notice is for.
        var html = await RenderAsync(90, null);

        Assert.Contains("name and email address", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("phone number", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("can contact you about it", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("90 days", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("only by staff", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(90, "/privacy")]
    [InlineData(null, null)]
    public async Task The_notice_promises_no_message(int? retentionDays, string? policyUrl)
    {
        // THE ASSERTION THAT REPLACED A TEST PINNING AN UNTRUE CLAIM. The notice used to say the
        // details were used "to confirm it with you", and this suite asserted it — while
        // docs/notifications.md says in bold that uBookIt sends nothing itself: no email, no SMS,
        // no message of any kind. So on a default install the address confirmed nothing with
        // anybody and the phone number served no stated purpose at all.
        //
        // A purpose statement — the details are held so the site is ABLE to make contact — is
        // true whether or not a site has wired up notifications. A promise that something is sent
        // is true only for the sites that have, and the package cannot know which those are.
        var html = await RenderAsync(retentionDays, policyUrl);

        Assert.DoesNotContain("we'll send", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("we will send", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confirm it with you", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confirmation email", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_form_does_not_contradict_the_notice_about_contact()
    {
        // The email field's own hint sat three lines above the notice and said "We'll send your
        // booking confirmation here." Fixing the notice while leaving the hint would have left
        // the package making two contradictory claims about the same field on the same screen —
        // and the hint was the one a visitor reads first.
        var details = await _renderer.RenderAsync(ViewInventory.YourDetails, Form(90, "/privacy"));

        Assert.DoesNotContain("we'll send", details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("we will send", details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("can contact you about your booking", details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_configured_period_is_stated()
    {
        var html = await RenderAsync(90, null);

        Assert.Contains("90 days", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("has not set a period", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_period_of_one_day_is_not_pluralised()
    {
        // Small, and it is here because the alternative reads as a defect to every visitor who
        // sees it: "We keep them for 1 days" is the kind of thing that makes a person doubt
        // everything else the page says about their data.
        var html = await RenderAsync(1, null);

        Assert.Contains("1 day", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1 days", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_configured_period_is_stated_rather_than_omitted()
    {
        // THE DEFAULT INSTALL. Omitting the sentence here would omit one of the four statements
        // on most sites, invisibly — which is why the branch exists at all.
        var html = await RenderAsync(null, null);

        Assert.Contains("until we remove them", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("has not set a period", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_configured_period_promises_no_deletion_and_states_no_number()
    {
        // The failure this branch could still have: saying something reassuring that is not
        // true. With no period configured nothing is erased automatically, so the notice must
        // not imply that anything is.
        var html = await RenderAsync(null, null);

        Assert.DoesNotContain("we remove them permanently", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" days", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_configured_policy_link_is_presented()
    {
        var html = await RenderAsync(90, "/privacy");

        Assert.Contains("href=\"/privacy\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_configured_link_there_is_no_anchor_at_all()
    {
        // Not an empty href, not a "#", not a disabled link. A link that goes nowhere on a
        // privacy notice is worse than no link, because it looks like the policy exists.
        var html = await RenderAsync(90, null);

        Assert.DoesNotContain("<a ", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(90, "/privacy")]
    [InlineData(90, null)]
    [InlineData(null, "/privacy")]
    [InlineData(null, null)]
    public async Task Every_combination_renders_the_notice(int? retentionDays, string? policyUrl)
    {
        // All four states the model can express, per default-frontend's requirement that a view
        // render every one of them. The combinations are crossed rather than sampled: the two
        // members are independent, and a suite that tested them one at a time could not see a
        // branch that only misbehaves when both are set.
        var html = await RenderAsync(retentionDays, policyUrl);

        Assert.Contains("ubookit-privacy", html, StringComparison.Ordinal);
        Assert.Contains("name and email address", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_notice_offers_no_control()
    {
        // It is a statement, not a consent mechanism. The basis for holding a booker's details
        // is performance of the booking, and an unrefusable tickbox would misrepresent that as
        // consent — which would imply a right to withdraw it and make the site's own records
        // revocable on request.
        foreach (var html in new[]
        {
            await RenderAsync(90, "/privacy"),
            await RenderAsync(null, null),
        })
        {
            Assert.DoesNotContain("<input", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<button", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<select", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("checkbox", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("required", html, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task The_notice_precedes_the_submit_control_in_the_details_view()
    {
        // ORDER, not mere presence. A person should read what happens to their details before
        // handing them over, and in a no-JS server-rendered form the DOM order IS the order a
        // screen reader follows. "Both are on the page" would pass with the notice underneath
        // the button, which is where it would be of least use.
        var details = await _renderer.RenderAsync(ViewInventory.YourDetails, Form(90, "/privacy"));

        var notice = details.IndexOf("ubookit-privacy", StringComparison.Ordinal);
        var submit = details.IndexOf("ubookit-submit", StringComparison.Ordinal);

        Assert.True(notice >= 0, "The details view does not render the privacy notice at all.");
        Assert.True(submit >= 0, "The details view does not render a submit control.");
        Assert.True(
            notice < submit,
            "The privacy notice renders after the submit control, so a visitor reaches the button "
            + "before being told what happens to the details they are about to send.");
    }

    [Fact]
    public void The_notice_and_the_retention_sweep_read_one_value()
    {
        // The guarantee is that they CANNOT disagree, which is a property of there being one
        // source — not of two call sites happening to say 90 today. So both are read from one
        // settings instance and compared, rather than each asserted against a literal.
        var settings = new SiteBookingSettings
        {
            TimeZoneId = "Europe/London",
            RetentionDays = 45,
            PrivacyPolicyUrl = "/privacy",
        };

        var notice = PrivacyNoticeView.From(settings, hostCanSendMail: false);

        Assert.Equal(settings.RetentionDays, notice.RetentionDays);
        Assert.Equal(settings.PrivacyPolicyUrl, notice.PolicyUrl);
    }

    [Fact]
    public void Turning_retention_off_turns_the_notice_off_with_it()
    {
        // The other half of the one-source guarantee, and the half a single equality check
        // would miss: null must survive the journey as null rather than becoming a zero.
        var settings = new SiteBookingSettings { TimeZoneId = "Europe/London", RetentionDays = null };

        var notice = PrivacyNoticeView.From(settings, hostCanSendMail: false);

        Assert.Null(notice.RetentionDays);
        Assert.False(notice.HasRetentionPeriod);
    }

    // ---- what the notice says about being contacted -----------------------------------------

    /// <summary>
    /// Both directions in one test, because the guarantee is that the notice describes THIS site
    /// — and a pair of one-way tests would each pass against a notice that always said its own
    /// thing. The two renders differ only in the send predicate.
    /// </summary>
    [Fact]
    public async Task What_the_notice_says_about_messages_follows_the_site()
    {
        var sends = await RenderAsync(null, null, sendsBookerEmail: true);
        var silent = await RenderAsync(null, null, sendsBookerEmail: false);

        Assert.Contains("send messages about your booking", sends, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("send messages about your booking", silent, StringComparison.OrdinalIgnoreCase);

        // MESSAGES, NEVER A CONFIRMATION — on the SENDING rendering, which is the one that can
        // over-promise. A form speaks before placement, and what arrives afterwards depends on
        // AutoConfirm and on the operator: an approval site sends "we have received your
        // booking", and a declined booking is never confirmed to anybody. The notice promised a
        // confirmation until QA caught it. This is the guard that keeps the noun honest, and it
        // is on the positive branch precisely because the negative branch already forbids every
        // promise (see A_site_that_sends_nothing_promises_nothing).
        Assert.DoesNotContain("your booking confirmation", sends, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("will be confirmed", sends, StringComparison.OrdinalIgnoreCase);

        // And the silent rendering still says why the address is wanted, rather than saying
        // nothing — the purpose statement is one of the four the notice exists to make.
        Assert.Contains("can contact you about it", silent, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A site that sends nothing must not have a notice that merely omits the promise while
    /// implying it some other way. Asserted against the words a promise would have to use.
    /// </summary>
    [Fact]
    public async Task A_site_that_sends_nothing_promises_nothing()
    {
        var html = await RenderAsync(90, "/privacy", sendsBookerEmail: false);

        Assert.DoesNotContain("we will send", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confirmation", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The two conditional sentences are independent: retention and sending are different settings
    /// and a site can be in any combination of them.
    /// </summary>
    [Theory]
    [InlineData(true, 90)]
    [InlineData(true, null)]
    [InlineData(false, 90)]
    [InlineData(false, null)]
    public async Task Retention_and_sending_do_not_interfere(bool sends, int? retentionDays)
    {
        var html = await RenderAsync(retentionDays, null, sendsBookerEmail: sends);

        Assert.Equal(
            sends,
            html.Contains("send messages about your booking", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            retentionDays is not null,
            html.Contains("90 days", StringComparison.OrdinalIgnoreCase));
    }

    // ---- the email field's hint, bound by the same requirement -------------------------------

    /// <summary>
    /// The notice's requirement governs "any surface of the same form that mentions contact", and
    /// the email field's hint is one. A site where the notice promises a confirmation and the hint
    /// does not — or the reverse — is contradicting itself about the same address, a few lines
    /// apart on the same screen.
    /// </summary>
    [Fact]
    public async Task The_email_hint_follows_the_same_predicate_as_the_notice()
    {
        var sends = Flat(await _renderer.RenderAsync(
            ViewInventory.YourDetails, Form(null, null, sendsBookerEmail: true)));
        var silent = Flat(await _renderer.RenderAsync(
            ViewInventory.YourDetails, Form(null, null, sendsBookerEmail: false)));

        Assert.Contains("email you about your booking", sends, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email you about your booking", silent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("can contact you about your booking", silent, StringComparison.OrdinalIgnoreCase);

        // The hint is bound by the notice's requirement, so the same no-confirmation rule
        // applies to it — and it is the surface that carried this exact false promise twice.
        Assert.DoesNotContain("confirmation", sends, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The hint's id is referenced by the input's `aria-describedby`, so it must be the same in
    /// both branches — which is exactly why the branch-reachability inventory lists it as unable
    /// to distinguish them, and why this test has to do that job instead.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_email_hint_keeps_its_id_whichever_it_says(bool sendsBookerEmail)
    {
        var html = await _renderer.RenderAsync(
            ViewInventory.YourDetails, Form(null, null, sendsBookerEmail));

        Assert.Contains("id=\"ubookit-email-hint\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"ubookit-email-hint\"", html, StringComparison.Ordinal);
    }
}
