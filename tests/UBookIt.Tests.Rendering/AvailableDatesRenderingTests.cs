using AngleSharp.Html.Parser;
using UBookIt.Tests.Rendering.Support;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// What the date step actually renders: the grouped list, the field for a date beyond it, and
/// the one attribute whose presence would quietly freeze the whole control.
/// </summary>
public class AvailableDatesRenderingTests
{
    private readonly ViewRenderer _renderer = new();
    private static readonly HtmlParser Parser = new();

    private static readonly DateOnly Day = new(2026, 9, 15);

    private static IBookingFormView Form(
        IReadOnlyList<AvailableDate>? dates = null,
        int? longestInWindow = null,
        DateOnly? selected = null)
    {
        var list = dates ?? [new AvailableDate(Day, true), new AvailableDate(Day.AddDays(2), false)];

        return new BookingFormModel
        {
            PrivacyNotice = new PrivacyNoticeView(null, null),
            AvailableDates = list,
            SelectedDateIsListed = list.Any(date => date.IsSelected),
            WindowDays = 30,
            LongestAvailableInWindowMinutes = longestInWindow,
            ResourceId = Guid.NewGuid(),
            ResourceName = "Meeting Room A",
            SelectedDate = selected ?? Day,
            MinDate = Day.AddDays(-5),
            MaxDate = Day.AddDays(85),
            DurationMinutes = 60,
            DurationOptions = [30, 60],
            Times = [],
            Errors = [],
        };
    }

    private async Task<AngleSharp.Dom.IDocument> RenderAsync(
        IReadOnlyList<AvailableDate>? dates = null,
        int? longestInWindow = null,
        DateOnly? selected = null)
        => await Parser.ParseDocumentAsync(
            await _renderer.RenderAsync(
                ViewInventory.AvailableDates, Form(dates, longestInWindow, selected)));

    [Fact]
    public async Task The_field_for_another_date_carries_no_value_and_that_is_load_bearing()
    {
        // FOUND BY MUTATION, and it is the trap this design most depends on nobody springing.
        //
        // Adding `value="@Model.SelectedDate…"` to this field looks like an obvious improvement
        // — the control it replaced did exactly that, and an empty field after choosing a date
        // reads like a bug. It would break the list completely: the typed date WINS over the
        // list by design, so a field that resubmitted a date chosen three renders ago would make
        // every subsequent click on the list do nothing at all, for ever.
        //
        // Left empty it submits an empty value, which falls through to the list. Nothing else in
        // the suite could see this: the freeze spans two requests, and every single-request
        // assertion passes either way.
        var document = await RenderAsync();

        var field = document.QuerySelector($"input[name='{BookingKeys.OtherDateQuery}']");

        Assert.NotNull(field);
        Assert.False(
            field!.HasAttribute("value"),
            "The 'another date' field carries a value attribute. It must not: the typed date "
            + "beats the list, so a repopulated field resubmits a stale date on every request "
            + "and the list of available dates stops working entirely.");
    }

    [Fact]
    public async Task The_list_and_the_field_submit_different_parameters()
    {
        // The collision in its rendered form. Two controls sharing a name means the browser
        // sends both values and model binding picks one — asserted over the markup rather than
        // over the constants, because it is the markup a browser reads.
        var document = await RenderAsync();

        var radios = document.QuerySelectorAll($"input[type=radio][name='{BookingKeys.DateQuery}']");
        var field = document.QuerySelector($"input[name='{BookingKeys.OtherDateQuery}']");

        Assert.NotEmpty(radios);
        Assert.NotNull(field);
        Assert.NotEqual(radios[0].GetAttribute("name"), field!.GetAttribute("name"));
    }

    [Fact]
    public async Task The_list_is_a_grouped_choice_with_a_label_per_date()
    {
        var document = await RenderAsync();

        var group = document.QuerySelector("fieldset.ubookit-dates");
        Assert.NotNull(group);
        Assert.NotNull(group!.QuerySelector("legend"));

        foreach (var radio in group.QuerySelectorAll("input[type=radio]"))
        {
            Assert.NotNull(document.QuerySelector($"label[for='{radio.Id}']"));
        }
    }

    [Fact]
    public async Task Exactly_the_selected_date_is_checked()
    {
        var document = await RenderAsync();

        var checkedRadios = document
            .QuerySelectorAll($"input[type=radio][name='{BookingKeys.DateQuery}']")
            .Where(radio => radio.HasAttribute("checked"))
            .ToList();

        var only = Assert.Single(checkedRadios);
        Assert.Equal(Day.ToString("yyyy-MM-dd"), only.GetAttribute("value"));
    }

    [Fact]
    public async Task A_selected_date_outside_the_list_is_named()
    {
        // Otherwise the page shows a list with nothing selected beside times for a date the list
        // does not contain, and the visitor has to reconcile the two.
        var document = await RenderAsync(
            dates: [new AvailableDate(Day.AddDays(1), false)],
            selected: Day.AddDays(40));

        var showing = document.QuerySelector(".ubookit-dates-showing");

        Assert.NotNull(showing);
        Assert.Contains(
            Day.AddDays(40).ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture),
            showing!.TextContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_selected_date_inside_the_list_is_not_named_again()
    {
        // The other direction: the statement appears only when it resolves a contradiction.
        // Rendering it always would put a redundant sentence under every ordinary page.
        var document = await RenderAsync();

        Assert.Null(document.QuerySelector(".ubookit-dates-showing"));
    }

    [Fact]
    public async Task An_empty_window_does_not_refer_to_a_list_that_is_not_there()
    {
        // FOUND BY LOOKING AT IT, and by nothing else — every assertion passed while the page
        // said "…which is not in the list above" directly beneath a paragraph explaining that
        // there is no list. The statement exists to resolve a contradiction between a list and
        // the times below it; with no list there is nothing to contradict, and the sentence
        // refers to something the page has just denied.
        var document = await RenderAsync(dates: [], longestInWindow: 30, selected: Day.AddDays(40));

        Assert.NotNull(document.QuerySelector(".ubookit-no-dates"));
        Assert.Null(document.QuerySelector(".ubookit-dates-showing"));
    }

    [Fact]
    public async Task An_empty_window_at_this_length_names_the_length_that_would_work()
    {
        var document = await RenderAsync(dates: [], longestInWindow: 30);

        var message = document.QuerySelector(".ubookit-no-dates");

        Assert.NotNull(message);
        Assert.Contains("30 minutes", message!.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("shorter length", message.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_empty_window_at_every_length_offers_no_shorter_length()
    {
        // The two empty states share a hook and differ only in wording, so the wording is what
        // is pinned. Suggesting a shorter length here would send the visitor to try something
        // that cannot work — a next move that leads nowhere is worse than naming the one that
        // remains.
        var document = await RenderAsync(dates: [], longestInWindow: null);

        var message = document.QuerySelector(".ubookit-no-dates");

        Assert.NotNull(message);
        Assert.Contains("any availability", message!.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("shorter length", message.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_two_empty_states_do_not_say_the_same_thing()
    {
        // They are different facts. A reader must be able to tell which they are being told,
        // and the class alone cannot say — it is deliberately the same on both.
        var lengthLimited = await RenderAsync(dates: [], longestInWindow: 30);
        var nothingAtAll = await RenderAsync(dates: [], longestInWindow: null);

        Assert.NotEqual(
            lengthLimited.QuerySelector(".ubookit-no-dates")!.TextContent.Trim(),
            nothingAtAll.QuerySelector(".ubookit-no-dates")!.TextContent.Trim());
    }

    [Fact]
    public async Task The_step_offers_no_control_that_needs_JavaScript()
    {
        // The standing invariant, checked where a new control was added rather than assumed to
        // have survived it.
        var html = await _renderer.RenderAsync(ViewInventory.AvailableDates, Form());

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onchange", html, StringComparison.OrdinalIgnoreCase);
    }
}
