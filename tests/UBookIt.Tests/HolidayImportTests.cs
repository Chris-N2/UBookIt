using System.Reflection;
using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The import's decision rule, and the shape of the port it decides over.
/// </summary>
/// <remarks>
/// Every case here is data: what a source returned, and which dates are already closed. No
/// database, no network, no HTTP — which is the point of keeping the rule a pure function.
/// </remarks>
public class HolidayImportTests
{
    private static readonly DateOnly From = new(2027, 1, 1);
    private static readonly DateOnly To = new(2027, 12, 31);

    private static PublicHoliday Holiday(string date, string name) => new(DateOnly.Parse(date), name);

    private static IReadOnlyList<HolidayRow> Classify(
        IEnumerable<PublicHoliday> holidays, params string[] closed)
        => HolidayImport.Classify(holidays, closed.Select(DateOnly.Parse), From, To);

    // ---- the port's shape ----

    /// <summary>
    /// <b>The absence of a region parameter is the design decision, not an oversight.</b> Which
    /// jurisdiction's holidays a site wants is a property of the implementation it registered;
    /// a parameter here would be one the package could neither validate nor default, and would
    /// invite the assumption that uBookIt knows something about jurisdictions. Asserted over the
    /// whole signature rather than by naming a forbidden parameter, because the one somebody adds
    /// will be called `country` or `locale` or `division`, not whatever a list here guessed.
    /// </summary>
    [Fact]
    public void The_source_port_takes_a_window_and_a_token_and_nothing_else()
    {
        var method = typeof(IPublicHolidaySource).GetMethod(nameof(IPublicHolidaySource.GetAsync));

        Assert.NotNull(method);
        Assert.Equal(
            new[] { typeof(DateOnly), typeof(DateOnly), typeof(CancellationToken) },
            method!.GetParameters().Select(p => p.ParameterType));
    }

    [Fact]
    public void The_source_port_has_exactly_one_member()
    {
        // A port a host implements is a promise about how much work implementing it is.
        Assert.Single(typeof(IPublicHolidaySource).GetMethods(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void A_holiday_carries_a_date_and_a_name()
    {
        var names = typeof(PublicHoliday)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(new[] { "Date", "Name" }, names);
    }

    [Fact]
    public void The_holiday_failure_codes_have_their_contract_values()
    {
        Assert.Equal("holiday-source-failed", FailureCodes.HolidaySourceFailed);
        Assert.Equal("holiday-not-importable", FailureCodes.HolidayNotImportable);
        Assert.Equal("holiday-source-absent", FailureCodes.HolidaySourceAbsent);
    }

    // ---- classification ----

    [Fact]
    public void A_date_with_no_closure_is_offered_as_new()
    {
        var row = Assert.Single(Classify([Holiday("2027-12-27", "Christmas Day (substitute day)")]));

        Assert.Equal(HolidayRowState.New, row.State);
        Assert.Equal("Christmas Day (substitute day)", row.Name);
        Assert.Null(row.Reason);
        Assert.False(row.CollapsedDuplicate);
    }

    [Fact]
    public void A_date_that_already_has_a_closure_is_not_offered()
    {
        var row = Assert.Single(Classify([Holiday("2027-12-25", "Christmas Day")], "2027-12-25"));

        Assert.Equal(HolidayRowState.AlreadyClosed, row.State);
    }

    /// <summary>
    /// <b>Already closed cannot depend on who closed it, because nothing records who did.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// This was originally written as a comparison — classify a "typed" closure, classify an
    /// "imported" one, assert the same answer. The two expressions were character-for-character
    /// identical, because there is no way to build a closure that remembers where it came from,
    /// which is precisely the property being claimed. A test that stages a comparison between a
    /// value and itself passes whatever the rule does.
    /// </para>
    /// <para>
    /// So it is asserted STRUCTURALLY instead. The guarantee — "the package SHALL NOT record that
    /// a closure came from an import" — is a fact about the types, and the reason no behavioural
    /// test can express it is the same reason it holds: the domain has nowhere to put an origin,
    /// and the rule is handed dates alone. This fails the day somebody adds the field.
    /// </para>
    /// </remarks>
    [Fact]
    public void Nothing_records_where_a_closure_came_from()
    {
        var closureMembers = typeof(SiteClosure)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToList();

        // The whole of it: an id, a date, a label. No origin, no source, no "imported" flag.
        Assert.Equal(
            new[] { nameof(SiteClosure.Date), nameof(SiteClosure.Id), nameof(SiteClosure.Label) },
            closureMembers.OrderBy(name => name, StringComparer.Ordinal));

        // And the classification is handed dates, not closures, so it could not read an origin
        // even if one existed.
        var closedDates = typeof(HolidayImport)
            .GetMethod(nameof(HolidayImport.Classify))!
            .GetParameters()
            .Single(parameter => parameter.Name == "closedDates");

        Assert.Equal(typeof(IEnumerable<DateOnly>), closedDates.ParameterType);
    }

    [Fact]
    public void An_over_long_name_cannot_be_imported_and_says_why()
    {
        var row = Assert.Single(Classify([Holiday("2027-03-01", new string('x', SiteClosure.MaxLabelLength + 1))]));

        Assert.Equal(HolidayRowState.CannotImport, row.State);
        Assert.Equal(FailureCodes.HolidayNotImportable, row.Reason);
    }

    [Fact]
    public void A_blank_name_cannot_be_imported()
    {
        var row = Assert.Single(Classify([Holiday("2027-03-01", "   ")]));

        Assert.Equal(HolidayRowState.CannotImport, row.State);
        Assert.Equal(FailureCodes.HolidayNotImportable, row.Reason);
    }

    [Fact]
    public void An_unimportable_name_is_not_truncated_to_fit()
    {
        // Truncating would create a closure labelled with something no source said, and the
        // operator would never learn the name was altered.
        var name = new string('x', SiteClosure.MaxLabelLength + 10);
        var row = Assert.Single(Classify([Holiday("2027-03-01", name)]));

        Assert.Equal(name.Length, row.Name.Length);
    }

    [Fact]
    public void A_name_is_trimmed_the_way_a_label_would_be()
    {
        var row = Assert.Single(Classify([Holiday("2027-01-01", "  New Year's Day  ")]));

        Assert.Equal("New Year's Day", row.Name);
    }

    // ---- duplicates ----

    [Fact]
    public void Two_holidays_on_one_date_collapse_to_the_first_and_report_it()
    {
        // A host merging England and Scotland can legitimately produce this, and only one
        // closure may exist per date. Asserting BOTH halves deliberately: a test that only
        // counted rows would pass while the operator was told nothing about the name that
        // vanished.
        var rows = Classify(
        [
            Holiday("2027-01-04", "New Year's Day (substitute day)"),
            Holiday("2027-01-04", "2nd January (substitute day)"),
        ]);

        var row = Assert.Single(rows);
        Assert.Equal("New Year's Day (substitute day)", row.Name);
        Assert.True(row.CollapsedDuplicate);
    }

    [Fact]
    public void A_date_with_no_duplicate_does_not_report_one()
    {
        Assert.False(Assert.Single(Classify([Holiday("2027-01-04", "Only one")])).CollapsedDuplicate);
    }

    [Fact]
    public void Three_on_one_date_still_leave_one_row()
    {
        var rows = Classify(
        [
            Holiday("2027-01-04", "First"),
            Holiday("2027-01-04", "Second"),
            Holiday("2027-01-04", "Third"),
        ]);

        Assert.Equal("First", Assert.Single(rows).Name);
    }

    // ---- the window ----

    [Fact]
    public void A_holiday_before_the_window_is_ignored()
    {
        Assert.Empty(Classify([Holiday("2026-12-25", "Christmas Day")]));
    }

    [Fact]
    public void A_holiday_after_the_window_is_ignored()
    {
        Assert.Empty(Classify([Holiday("2028-01-01", "New Year's Day")]));
    }

    [Fact]
    public void The_window_is_inclusive_at_both_ends()
    {
        var rows = Classify([Holiday("2027-01-01", "First day"), Holiday("2027-12-31", "Last day")]);

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void A_source_returning_nothing_yields_no_rows()
    {
        // A real and correct answer — a window with no holidays in it — and distinct from a
        // source that failed, which never reaches this rule at all.
        Assert.Empty(Classify([]));
    }

    // ---- ordering ----

    [Fact]
    public void Rows_are_ordered_by_date_whatever_order_the_source_used()
    {
        var rows = Classify(
        [
            Holiday("2027-12-25", "Christmas Day"),
            Holiday("2027-01-01", "New Year's Day"),
            Holiday("2027-04-02", "Good Friday"),
        ]);

        Assert.Equal(
            new[] { "2027-01-01", "2027-04-02", "2027-12-25" },
            rows.Select(r => r.Date.ToString("yyyy-MM-dd")));
    }

    [Fact]
    public void The_three_states_can_appear_together()
    {
        var rows = Classify(
        [
            Holiday("2027-01-01", "New Year's Day"),
            Holiday("2027-12-25", "Christmas Day"),
            Holiday("2027-06-01", new string('x', SiteClosure.MaxLabelLength + 1)),
        ], "2027-12-25");

        Assert.Equal(
            new[] { HolidayRowState.New, HolidayRowState.CannotImport, HolidayRowState.AlreadyClosed },
            rows.Select(r => r.State));
    }
}

/// <summary>
/// Reading a source: what the package does with what a site's own code returns, throws, or is
/// cancelled out of.
/// </summary>
public class HolidayPreviewServiceTests
{
    private static readonly DateOnly From = new(2027, 1, 1);
    private static readonly DateOnly To = new(2027, 12, 31);

    private static CancellationToken Ct => CancellationToken.None;

    /// <summary>A source that answers, and records the token it was handed.</summary>
    private sealed class FakeSource(params PublicHoliday[] holidays) : IPublicHolidaySource
    {
        public CancellationToken TokenReceived { get; private set; }

        public (DateOnly From, DateOnly To)? WindowReceived { get; private set; }

        public Task<IReadOnlyList<PublicHoliday>> GetAsync(
            DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        {
            TokenReceived = cancellationToken;
            WindowReceived = (from, to);
            return Task.FromResult<IReadOnlyList<PublicHoliday>>(holidays);
        }
    }

    private sealed class ThrowingSource(Exception? thrown = null) : IPublicHolidaySource
    {
        public Task<IReadOnlyList<PublicHoliday>> GetAsync(
            DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
            => throw (thrown ?? new HttpRequestException("the feed is down"));
    }

    private static HolidayPreviewService Service(IPublicHolidaySource? source, params SiteClosure[] closures)
    {
        var store = new InMemorySiteClosureStore();
        foreach (var closure in closures)
        {
            store.Add(closure);
        }

        return new HolidayPreviewService(store, source);
    }

    [Fact]
    public async Task A_registered_source_is_asked_for_the_window_it_was_given()
    {
        var source = new FakeSource(new PublicHoliday(new DateOnly(2027, 1, 1), "New Year's Day"));

        var result = await Service(source).PreviewAsync(From, To, Ct);

        Assert.True(result.Succeeded);
        Assert.Equal((From, To), source.WindowReceived);
        Assert.Equal("New Year's Day", Assert.Single(result.Value).Name);
    }

    [Fact]
    public async Task Cancellation_reaches_the_source()
    {
        // A source that cannot be abandoned holds an operator's screen, and the package sets no
        // timeout of its own — so the token being passed through IS the whole contract.
        using var cts = new CancellationTokenSource();
        var source = new FakeSource();

        await Service(source).PreviewAsync(From, To, cts.Token);

        Assert.Equal(cts.Token, source.TokenReceived);
    }

    [Fact]
    public async Task A_cancelled_preview_does_not_become_a_source_failure()
    {
        // The operator's own doing. Reporting it as "your source is broken" would send somebody
        // to debug a feed that is fine.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Service(new ThrowingSource(new OperationCanceledException(cts.Token)))
                .PreviewAsync(From, To, cts.Token));
    }

    /// <summary>
    /// <b>The distinction this service exists to keep.</b> A window with no holidays in it is a
    /// real, correct answer; a broken source is not an answer at all. Conflating them tells an
    /// operator their calendar is clear when it is unknown.
    /// </summary>
    [Fact]
    public async Task A_broken_source_and_an_empty_one_produce_different_answers()
    {
        var empty = await Service(new FakeSource()).PreviewAsync(From, To, Ct);
        var broken = await Service(new ThrowingSource()).PreviewAsync(From, To, Ct);

        Assert.True(empty.Succeeded);
        Assert.Empty(empty.Value);

        Assert.False(broken.Succeeded);
        Assert.Equal(FailureCodes.HolidaySourceFailed, Assert.Single(broken.Failures).Code);
    }

    /// <summary>
    /// <b>A host's exception text never reaches the reported failure.</b>
    /// </summary>
    /// <remarks>
    /// The source is arbitrary site code reaching an arbitrary place, and the message it throws
    /// with is the host's, not ours. Such a message routinely carries the URI it was calling —
    /// query string and all — and can carry a connection string or a credential. The failure
    /// travels into an HTTP response body, so quoting the message would publish whatever the host
    /// happened to put in it, to nobody's benefit: the client renders a fixed sentence from the
    /// stable code and never displays the detail.
    /// <para>
    /// The secret here is a fake, and the assertion is that it does not appear. The type name is
    /// asserted present so this is not satisfied by reporting nothing at all.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_sources_own_exception_text_is_not_carried_into_the_failure()
    {
        var leaky = new ThrowingSource(new HttpRequestException(
            "GET https://holidays.example/api?apiKey=not-a-real-secret-8sf7 failed"));

        var result = await Service(leaky).PreviewAsync(From, To, Ct);

        var message = Assert.Single(result.Failures).Message;

        Assert.DoesNotContain("apiKey", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not-a-real-secret-8sf7", message, StringComparison.Ordinal);
        Assert.DoesNotContain("holidays.example", message, StringComparison.OrdinalIgnoreCase);

        // Not vacuous: something IS reported, and it names the shape of the failure.
        Assert.Contains(nameof(HttpRequestException), message, StringComparison.Ordinal);
    }

    /// <summary>
    /// An inverted window is a malformed request, not an empty answer.
    /// </summary>
    /// <remarks>
    /// Without this it read back as "the source returned no holidays for those dates" — the same
    /// conflation of "no answer" with "an empty answer" that the empty/failed distinction exists
    /// to prevent, arriving by a different door.
    /// </remarks>
    [Fact]
    public async Task An_inverted_window_is_refused_rather_than_answered_emptily()
    {
        var source = new FakeSource(new PublicHoliday(new DateOnly(2027, 1, 1), "New Year's Day"));

        var result = await Service(source).PreviewAsync(To, From, Ct);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.DateRangeInvalid, Assert.Single(result.Failures).Code);

        // And the source was never troubled with a window that could not be answered.
        Assert.Null(source.WindowReceived);
    }

    /// <summary>
    /// <b>Absence outranks validation.</b> A site with no source answers "no such feature" to a
    /// malformed request too, rather than validating it and thereby confirming the endpoint is
    /// there. Absence is total, including in what it declines to tell anyone.
    /// </summary>
    [Fact]
    public async Task With_no_source_even_a_malformed_window_reports_absence()
    {
        var result = await Service(source: null).PreviewAsync(To, From, Ct);

        Assert.Equal(FailureCodes.HolidaySourceAbsent, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public async Task No_source_is_absence_not_failure_of_the_source()
    {
        var result = await Service(source: null).PreviewAsync(From, To, Ct);

        Assert.False(result.Succeeded);

        // A DIFFERENT code from a source that failed: there is nothing to fail, and an operator
        // told "your source is broken" would go looking for one that was never registered.
        Assert.Equal(FailureCodes.HolidaySourceAbsent, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void Whether_a_source_is_registered_is_answerable_without_calling_it()
    {
        // The client needs this to decide whether to render the control at all, and asking must
        // not reach out to somebody's API to find out.
        Assert.True(Service(new FakeSource()).SourceRegistered);
        Assert.False(Service(source: null).SourceRegistered);
    }

    [Fact]
    public async Task Existing_closures_classify_the_rows()
    {
        var service = Service(
            new FakeSource(
                new PublicHoliday(new DateOnly(2027, 1, 1), "New Year's Day"),
                new PublicHoliday(new DateOnly(2027, 12, 25), "Christmas Day")),
            TestData.Closure(new DateOnly(2027, 12, 25), "Christmas Day"));

        var rows = (await service.PreviewAsync(From, To, Ct)).Value;

        Assert.Equal(HolidayRowState.New, rows[0].State);
        Assert.Equal(HolidayRowState.AlreadyClosed, rows[1].State);
    }

    [Fact]
    public async Task Previewing_twice_creates_nothing()
    {
        var store = new InMemorySiteClosureStore();
        var service = new HolidayPreviewService(
            store, new FakeSource(new PublicHoliday(new DateOnly(2027, 1, 1), "New Year's Day")));

        await service.PreviewAsync(From, To, Ct);
        await service.PreviewAsync(From, To, Ct);

        Assert.Empty(await ((ISiteClosureStore)store).ListAsync(Ct));
    }
}
