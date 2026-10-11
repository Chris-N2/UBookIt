using UBookIt.Core.Availability;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The holiday preview's "already closed" against closure RANGES (closure-date-ranges: the
/// <c>public-holidays</c> delta, scenario <i>A date inside a range is already closed</i>), through
/// <see cref="HolidayPreviewService"/> — where the range is clipped to the window and expanded.
/// </summary>
public class HolidayPreviewRangeTests
{
    private static CancellationToken Ct => CancellationToken.None;

    private sealed class Source(params PublicHoliday[] holidays) : IPublicHolidaySource
    {
        public Task<IReadOnlyList<PublicHoliday>> GetAsync(
            DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PublicHoliday>>(holidays);
    }

    private static async Task<IReadOnlyList<HolidayRow>> PreviewAsync(
        DateOnly from, DateOnly to, SiteClosure[] closures, params PublicHoliday[] holidays)
    {
        var store = new InMemorySiteClosureStore();
        foreach (var closure in closures)
        {
            store.Add(closure);
        }

        var result = await new HolidayPreviewService(store, new Source(holidays)).PreviewAsync(from, to, Ct);
        Assert.True(result.Succeeded);
        return result.Value;
    }

    [Fact]
    public async Task A_holiday_inside_a_closure_range_is_already_closed()
    {
        var rows = await PreviewAsync(
            new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31),
            [SiteClosure.CreateRange(new DateOnly(2027, 3, 1), new DateOnly(2027, 4, 30), "Refurbishment").Value],
            new PublicHoliday(new DateOnly(2027, 3, 26), "Good Friday"),
            new PublicHoliday(new DateOnly(2027, 5, 3), "Early May bank holiday"));

        Assert.Equal(HolidayRowState.AlreadyClosed, rows.Single(r => r.Name == "Good Friday").State);
        Assert.Equal(HolidayRowState.New, rows.Single(r => r.Name == "Early May bank holiday").State);
    }

    [Fact]
    public async Task Both_ends_of_a_range_are_already_closed()
    {
        var rows = await PreviewAsync(
            new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31),
            [SiteClosure.CreateRange(new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 31), "March").Value],
            new PublicHoliday(new DateOnly(2027, 3, 1), "First"),
            new PublicHoliday(new DateOnly(2027, 3, 31), "Last"),
            new PublicHoliday(new DateOnly(2027, 4, 1), "After"));

        Assert.Equal(HolidayRowState.AlreadyClosed, rows.Single(r => r.Name == "First").State);
        Assert.Equal(HolidayRowState.AlreadyClosed, rows.Single(r => r.Name == "Last").State);
        Assert.Equal(HolidayRowState.New, rows.Single(r => r.Name == "After").State);
    }

    [Fact]
    public async Task A_range_reaching_outside_the_window_is_clipped_not_ignored()
    {
        // The range starts before the window and ends inside it: the dates it covers inside the
        // window are closed, and the clipping must not drop the range for starting outside.
        var rows = await PreviewAsync(
            new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31),
            [SiteClosure.CreateRange(new DateOnly(2026, 12, 20), new DateOnly(2027, 1, 3), "Christmas shutdown").Value],
            new PublicHoliday(new DateOnly(2027, 1, 1), "New Year's Day"));

        Assert.Equal(HolidayRowState.AlreadyClosed, Assert.Single(rows).State);
    }

    [Fact]
    public async Task A_ten_year_closure_previews_without_expanding_beyond_the_window()
    {
        // Clipped to the window, so this cannot cost ten years of dates. Asserted by outcome: the
        // preview answers, and answers correctly at both ends of the window.
        var rows = await PreviewAsync(
            new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31),
            [SiteClosure.CreateRange(new DateOnly(2020, 1, 1), new DateOnly(2035, 12, 31), "Long").Value],
            new PublicHoliday(new DateOnly(2027, 1, 1), "Start"),
            new PublicHoliday(new DateOnly(2027, 1, 31), "End"));

        Assert.All(rows, row => Assert.Equal(HolidayRowState.AlreadyClosed, row.State));
    }
}
