using AngleSharp.Html.Parser;
using UBookIt.Tests.Rendering.Support;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// What the date step says when its window starts after today because a closure covers today
/// (closure-date-ranges: the <c>default-frontend</c> delta, and the narrowing in
/// <c>site-closures</c>' <i>Closures are not disclosed publicly</i>).
/// </summary>
/// <remarks>
/// Every statement the step makes about the window is rendered in BOTH states — shifted and not —
/// because the two wordings sit behind one branch, and a branch tested on one side only is the
/// place a fix passes while guarding nothing.
/// </remarks>
public class AvailableDatesShiftedWindowRenderingTests
{
    private readonly ViewRenderer _renderer = new();
    private static readonly HtmlParser Parser = new();

    private static readonly DateOnly Today = new(2026, 10, 11);

    private static readonly DateOnly WindowFrom = new(2026, 12, 21);

    private static IBookingFormView Form(DateOnly? shiftedFrom, IReadOnlyList<AvailableDate> dates, int? longestInWindow)
        => new BookingFormModel
        {
            PrivacyNotice = new PrivacyNoticeView(null, null, false),
            AvailableDates = dates,
            SelectedDateIsListed = dates.Any(date => date.IsSelected),
            WindowDays = 30,
            ShiftedWindowFrom = shiftedFrom,
            LongestAvailableInWindowMinutes = longestInWindow,
            ResourceId = Guid.NewGuid(),
            ResourceName = "Meeting Room A",
            SelectedDate = shiftedFrom ?? Today,
            MinDate = Today,
            MaxDate = Today.AddDays(120),
            DurationMinutes = 60,
            DurationOptions = [30, 60],
            Times = [],
            Errors = [],
        };

    private async Task<string> TextAsync(DateOnly? shiftedFrom, IReadOnlyList<AvailableDate> dates, int? longestInWindow = null)
    {
        var html = await _renderer.RenderAsync(ViewInventory.AvailableDates, Form(shiftedFrom, dates, longestInWindow));
        var document = await Parser.ParseDocumentAsync(html);
        return string.Join(' ', document.Body!.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static readonly IReadOnlyList<AvailableDate> Listed = [new AvailableDate(WindowFrom, true)];

    // Every window state the step can render, so each statement is checked in each.
    public static TheoryData<string> States() => new() { "listed", "empty at this length", "empty at every length" };

    private Task<string> TextAsync(string state, DateOnly? shiftedFrom) => state switch
    {
        "listed" => TextAsync(shiftedFrom, Listed),
        "empty at this length" => TextAsync(shiftedFrom, [], longestInWindow: 30),
        _ => TextAsync(shiftedFrom, [], longestInWindow: null),
    };

    [Theory]
    [MemberData(nameof(States))]
    public async Task A_shifted_window_names_its_dates_instead_of_the_next_n_days(string state)
    {
        var text = await TextAsync(state, WindowFrom);

        Assert.Contains("from Monday 21 December 2026 to Tuesday 19 January 2027", text);
        Assert.DoesNotContain("in the next 30 days", text);
        Assert.DoesNotContain("beyond the next 30 days", text);
    }

    [Theory]
    [MemberData(nameof(States))]
    public async Task A_window_starting_today_keeps_its_wording(string state)
    {
        var text = await TextAsync(state, shiftedFrom: null);

        Assert.Contains("in the next 30 days", text);
        Assert.DoesNotContain("from Monday", text);
    }

    [Fact]
    public async Task The_other_date_hint_names_the_last_listed_date_when_shifted()
    {
        Assert.Contains("For a date after Tuesday 19 January 2027.", await TextAsync(WindowFrom, Listed));
        Assert.Contains("For a date beyond the next 30 days.", await TextAsync(null, Listed));
    }

    [Theory]
    [MemberData(nameof(States))]
    public async Task The_shifted_window_discloses_no_closure(string state)
    {
        // The view model carries no closure, label or note at all, so this asserts the WORDING the
        // view adds of its own accord: nothing that says the dates before were closed, or why.
        var text = await TextAsync(state, WindowFrom);

        Assert.DoesNotContain("clos", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("holiday", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reopen", text, StringComparison.OrdinalIgnoreCase);
    }
}
