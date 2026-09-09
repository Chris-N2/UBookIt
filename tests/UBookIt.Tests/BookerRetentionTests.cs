using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;
using UBookIt.Persistence.Composing;
using UBookIt.Persistence.Jobs;
using UBookIt.Tests.Support;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace UBookIt.Tests;

/// <summary>
/// The retention sweep: what it erases, what it leaves, and the three ways it could silently
/// do less than it claims.
/// </summary>
/// <remarks>
/// <para>
/// The storage half is not here and cannot be — the double keeps the aggregate the caller
/// mutated, so it can show that the sweep <i>asked</i> for the right erasures but not that any
/// row was written. That belongs to the integration suite against a real database, as it does
/// for erasure itself.
/// </para>
/// <para>
/// <b>What IS here that could not be tested anywhere else</b> is the loop: that it reaches past
/// the first batch, that it stops rather than spinning, and that it issues no query at all when
/// retention is off.
/// </para>
/// </remarks>
public class BookerRetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private const int RetentionDays = 90;

    /// <summary>A booking whose slot ended <paramref name="days"/> days before <see cref="Now"/>.</summary>
    /// <remarks>
    /// Negative values put the end in the future, which is how the "cancelled long ago, happens
    /// next March" case is staged — the one Chris signed off as staying on the same clock as
    /// everything else.
    /// </remarks>
    private static Booking EndedDaysAgo(
        double days,
        BookingStatus status = BookingStatus.Confirmed,
        Guid? resourceId = null,
        Booker? booker = null)
    {
        var endUtc = Now.AddDays(-days);

        return Booking.Rehydrate(
            Guid.NewGuid(),
            References.Any(),
            BookingInterval.Create(endUtc.AddHours(-1), endUtc, TestData.LondonZoneId).Value,
            booker ?? TestData.Booker(),
            [new ResourceClaim(resourceId ?? Guid.NewGuid())],
            status,
            Now.AddDays(-365),
            new ServiceAttribution(Guid.NewGuid(), "Initial Consultation")).Value;
    }

    private sealed record Harness(
        BookerRetentionJob Job, InMemoryBookingStore Store, CapturingLogger Logger);

    private static Harness Build(
        int? retentionDays = RetentionDays,
        IEnumerable<Booking>? seed = null,
        IBookingService? bookingService = null,
        DateTimeOffset? now = null)
    {
        var store = new InMemoryBookingStore();

        foreach (var booking in seed ?? [])
        {
            // Straight to the store, not through BookingService.PlaceAsync — the fixtures need
            // slots in the distant past and in the future, which placement's lead-time and
            // horizon rules would refuse. What is under test is the sweep, not placement.
            store.PlaceAsync(booking).GetAwaiter().GetResult();
        }

        var clock = new FixedTimeProvider(now ?? Now);
        var settings = TestData.Settings with { RetentionDays = retentionDays };

        var services = new ServiceCollection();
        services.AddSingleton<IBookingStore>(store);
        services.AddSingleton<IResourceStore>(new InMemoryResourceStore());
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton(settings);

        if (bookingService is null)
        {
            // The real service over the real verb. A double here would let the sweep pass while
            // erasing through something that is not the package's erasure operation.
            services.AddScoped<IBookingService, BookingService>();
        }
        else
        {
            services.AddSingleton(bookingService);
        }

        var provider = services.BuildServiceProvider();
        var logger = new CapturingLogger();

        return new Harness(
            new BookerRetentionJob(
                provider.GetRequiredService<IServiceScopeFactory>(), settings, clock, logger),
            store,
            logger);
    }

    private static async Task<Booking> Reread(InMemoryBookingStore store, Booking booking)
        => (await store.GetBookingAsync(booking.Id))!;

    [Fact]
    public async Task A_booking_past_the_period_is_erased()
    {
        var due = EndedDaysAgo(RetentionDays + 10);
        var harness = Build(seed: [due]);

        await harness.Job.ExecuteAsync(CancellationToken.None);

        var stored = await Reread(harness.Store, due);
        Assert.True(stored.Booker.IsErased);
        Assert.Equal(Now, stored.Booker.ErasedUtc);
    }

    [Fact]
    public async Task A_booking_inside_the_period_is_untouched()
    {
        var recent = EndedDaysAgo(RetentionDays - 10);
        var harness = Build(seed: [recent]);

        await harness.Job.ExecuteAsync(CancellationToken.None);

        Assert.False((await Reread(harness.Store, recent)).Booker.IsErased);
    }

    [Fact]
    public async Task The_boundary_falls_where_the_period_says()
    {
        // A day either side of the cutoff, so that an off-by-one in the direction of erasing too
        // much is visible. Erasing a day early is not a rounding error here; it is destroying
        // data the site's own published policy said it would still hold.
        var justOver = EndedDaysAgo(RetentionDays + 0.5);
        var justUnder = EndedDaysAgo(RetentionDays - 0.5);
        var harness = Build(seed: [justOver, justUnder]);

        await harness.Job.ExecuteAsync(CancellationToken.None);

        Assert.True((await Reread(harness.Store, justOver)).Booker.IsErased);
        Assert.False((await Reread(harness.Store, justUnder)).Booker.IsErased);
    }

    [Theory]
    [InlineData(BookingStatus.Requested)]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Declined)]
    public async Task Retention_is_blind_to_status(BookingStatus status)
    {
        // Every status, one per case, because a fixture set of one status cannot see a status
        // filter that excludes the others — and a status filter is the most natural-looking way
        // for this sweep to quietly under-erase.
        var due = EndedDaysAgo(RetentionDays + 10, status);
        var harness = Build(seed: [due]);

        await harness.Job.ExecuteAsync(CancellationToken.None);

        Assert.True((await Reread(harness.Store, due)).Booker.IsErased);
    }

    [Fact]
    public async Task A_booking_whose_slot_has_not_ended_is_never_erased()
    {
        // Cancelled a year ago, happening next March. Its details stay until its own end plus the
        // period — the decision taken deliberately over adding a cancellation timestamp, and
        // therefore something that needs a test rather than a paragraph.
        var futureCancelled = EndedDaysAgo(-180, BookingStatus.Cancelled);
        var harness = Build(seed: [futureCancelled]);

        await harness.Job.ExecuteAsync(CancellationToken.None);

        Assert.False((await Reread(harness.Store, futureCancelled)).Booker.IsErased);
    }

    [Fact]
    public async Task The_booking_survives_the_erasure()
    {
        var due = EndedDaysAgo(RetentionDays + 10, BookingStatus.Confirmed);
        var harness = Build(seed: [due]);

        await harness.Job.ExecuteAsync(CancellationToken.None);

        var stored = await Reread(harness.Store, due);
        Assert.Equal(due.Reference, stored.Reference);
        Assert.Equal(due.Interval, stored.Interval);
        Assert.Equal(BookingStatus.Confirmed, stored.Status);
        Assert.Equal(due.Claims.Single().ResourceId, stored.Claims.Single().ResourceId);
    }

    [Fact]
    public async Task Every_due_booking_is_erased_even_when_they_do_not_fit_in_one_batch()
    {
        // THE MOST IMPORTANT ASSERTION IN THIS FILE, and the reason for the fixture count.
        //
        // The due set is defined by "not yet erased", so erasing a booking REMOVES IT from that
        // set. A sweep that paged by offset — skip 0, skip 100, skip 200 — would therefore step
        // over exactly as many un-erased bookings as it had just erased, erase a fraction of the
        // data, and report success. Nothing about the run would look wrong.
        //
        // With BatchSize or fewer fixtures, taking-the-head and skipping-by-offset behave
        // IDENTICALLY and this test cannot fail however it is written. Hence 250: two full
        // batches and a partial one, so a broken sweep leaves the second and third behind.
        const int Count = (BookerRetentionJob.BatchSize * 2) + 50;

        var due = Enumerable.Range(0, Count)
            .Select(i => EndedDaysAgo(RetentionDays + 1 + i))
            .ToList();

        var harness = Build(seed: due);

        await harness.Job.ExecuteAsync(CancellationToken.None);

        var unerased = new List<Guid>();
        foreach (var booking in due)
        {
            if (!(await Reread(harness.Store, booking)).Booker.IsErased)
            {
                unerased.Add(booking.Id);
            }
        }

        Assert.Empty(unerased);
    }

    [Fact]
    public async Task Retention_that_is_off_issues_no_query_at_all()
    {
        // "Nothing was erased" is satisfied by a sweep that queried and discarded the answer, and
        // by one that never ran. Only the second is the requirement, so the read is counted
        // rather than the outcome inspected.
        var due = EndedDaysAgo(RetentionDays + 10);
        var harness = Build(retentionDays: null, seed: [due]);

        await harness.Job.ExecuteAsync(CancellationToken.None);

        Assert.Equal(0, harness.Store.DueForErasureReads);
        Assert.Equal(0, harness.Store.EraseCount);
        Assert.False((await Reread(harness.Store, due)).Booker.IsErased);
        Assert.Empty(harness.Logger.Entries);
    }

    [Fact]
    public async Task Running_twice_changes_nothing_the_second_time()
    {
        var due = EndedDaysAgo(RetentionDays + 10);
        var harness = Build(seed: [due]);

        await harness.Job.ExecuteAsync(CancellationToken.None);
        var firstInstant = (await Reread(harness.Store, due)).Booker.ErasedUtc;

        // A LATER CLOCK for the second run. Sharing one would make this pass against an
        // implementation that re-erases everything on every sweep, because the instant written
        // would be the same either way — and the difference between absorbing and re-erasing is
        // the difference between a safe retry and moving the date a data subject was told.
        var later = Build(retentionDays: RetentionDays, now: Now.AddDays(30));
        foreach (var booking in new[] { await Reread(harness.Store, due) })
        {
            await later.Store.PlaceAsync(booking);
        }

        await later.Job.ExecuteAsync(CancellationToken.None);

        Assert.Equal(firstInstant, (await Reread(later.Store, due)).Booker.ErasedUtc);
        Assert.Equal(0, later.Store.EraseCount);
    }

    [Fact]
    public async Task A_batch_in_which_nothing_could_be_erased_ends_the_run()
    {
        // ANTI-SPIN. A booking that fails to erase still matches the due predicate, so it comes
        // back in the next batch — for ever. Without this guard the sweep is an infinite loop
        // holding a database connection, and a suite that only ever stages SUCCESSFUL erasures
        // cannot see the guard at all.
        //
        // The failing double is what makes the test possible; the assertion is that the run
        // RETURNS, which a spinning implementation would never do.
        var due = EndedDaysAgo(RetentionDays + 10);
        var harness = Build(seed: [due], bookingService: new AlwaysFailsToErase());

        var run = harness.Job.ExecuteAsync(CancellationToken.None);
        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.Same(run, finished);
        await run;

        Assert.False((await Reread(harness.Store, due)).Booker.IsErased);
        Assert.Contains(harness.Logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task One_unerasable_booking_does_not_stop_the_others()
    {
        var stubborn = EndedDaysAgo(RetentionDays + 100);
        var others = Enumerable.Range(0, 5).Select(i => EndedDaysAgo(RetentionDays + 10 + i)).ToList();

        var harness = Build(
            seed: [stubborn, .. others],
            bookingService: null);

        // Rebuilt with a service that throws for one id only, over the same store the fixtures
        // were seeded into — so the sweep meets a genuine per-booking failure mid-batch.
        var store = harness.Store;
        var services = new ServiceCollection();
        services.AddSingleton<IBookingStore>(store);
        services.AddSingleton<IResourceStore>(new InMemoryResourceStore());
        var clock = new FixedTimeProvider(Now);
        services.AddSingleton<TimeProvider>(clock);
        var settings = TestData.Settings with { RetentionDays = RetentionDays };
        services.AddSingleton(settings);
        services.AddScoped<IBookingService>(sp => new ThrowsForOne(
            new BookingService(
                sp.GetRequiredService<IResourceStore>(), store, clock, settings),
            stubborn.Id));

        var provider = services.BuildServiceProvider();
        var logger = new CapturingLogger();
        var job = new BookerRetentionJob(
            provider.GetRequiredService<IServiceScopeFactory>(), settings, clock, logger);

        await job.ExecuteAsync(CancellationToken.None);

        Assert.False((await Reread(store, stubborn)).Booker.IsErased);
        foreach (var booking in others)
        {
            Assert.True((await Reread(store, booking)).Booker.IsErased);
        }

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task A_sweep_cancelled_PART_WAY_stops_and_leaves_the_rest_due()
    {
        // THE SCENARIO THE TEST BELOW CANNOT REACH.
        //
        // "Interrupted AFTER erasing some of the due bookings" needs cancellation to arrive
        // mid-batch. Cancelling before the run makes `while (!token.IsCancellationRequested)`
        // false on entry, so the loop body never executes and the guard inside it — the one that
        // stops the sweep between units of work — is never reached. QA proved that by deleting
        // that guard and watching all 1095 unit tests stay green.
        //
        // So the token is cancelled BY the erasure of the third booking, from inside the loop.
        var due = Enumerable.Range(0, 10).Select(i => EndedDaysAgo(RetentionDays + 10 + i)).ToList();

        using var cts = new CancellationTokenSource();
        var harness = Build(seed: due, bookingService: null);

        var store = harness.Store;
        var services = new ServiceCollection();
        services.AddSingleton<IBookingStore>(store);
        services.AddSingleton<IResourceStore>(new InMemoryResourceStore());
        var clock = new FixedTimeProvider(Now);
        services.AddSingleton<TimeProvider>(clock);
        var settings = TestData.Settings with { RetentionDays = RetentionDays };
        services.AddSingleton(settings);
        services.AddScoped<IBookingService>(sp => new CancelsAfter(
            new BookingService(sp.GetRequiredService<IResourceStore>(), store, clock, settings),
            cts,
            after: 3));

        var provider = services.BuildServiceProvider();
        var job = new BookerRetentionJob(
            provider.GetRequiredService<IServiceScopeFactory>(), settings, clock, new CapturingLogger());

        await job.ExecuteAsync(cts.Token);

        // Stopped where it was told to. Erasure is absorbing, so this is only safe if the sweep
        // stops BETWEEN bookings rather than part-way through one — and if it stopped at all,
        // which is what the count shows.
        Assert.Equal(3, store.EraseCount);

        var erased = new List<Booking>();
        foreach (var booking in due)
        {
            if ((await Reread(store, booking)).Booker.IsErased)
            {
                erased.Add(booking);
            }
        }

        Assert.Equal(3, erased.Count);

        // And the rest are still due — the half that makes the next run able to finish the work.
        var stillDue = await store.GetBookingIdsDueForErasureAsync(Now.AddDays(-RetentionDays), 100);
        Assert.Equal(7, stillDue.Count);

        // Resumption, with a fresh token, completes it without re-erasing the first three.
        var finishing = new BookerRetentionJob(
            provider.GetRequiredService<IServiceScopeFactory>(), settings, clock, new CapturingLogger());

        await finishing.ExecuteAsync(CancellationToken.None);

        foreach (var booking in due)
        {
            Assert.True((await Reread(store, booking)).Booker.IsErased);
        }
    }

    [Fact]
    public async Task An_interrupted_sweep_leaves_what_it_reached_erased_and_the_rest_due()
    {
        // Cancelled before it starts: nothing is erased, and — the half that matters — nothing is
        // left in a state the next run cannot finish. The PART-WAY case is the test above; this
        // one only covers entry, and is kept because "an already-cancelled token does no work"
        // is worth pinning separately from "a sweep stops when cancelled mid-flight".
        var due = Enumerable.Range(0, 5).Select(i => EndedDaysAgo(RetentionDays + 10 + i)).ToList();
        var harness = Build(seed: due);

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await harness.Job.ExecuteAsync(cancelled.Token);

        Assert.Equal(0, harness.Store.EraseCount);

        // The resumption half. Without this the test would prove only that cancelling stops work,
        // not that the work is still there to be done.
        await harness.Job.ExecuteAsync(CancellationToken.None);

        foreach (var booking in due)
        {
            Assert.True((await Reread(harness.Store, booking)).Booker.IsErased);
        }
    }

    [Fact]
    public void The_lease_name_is_a_fixed_constant_and_not_derived()
    {
        // Umbraco stores the job against this name. Deriving it from a type name, an assembly
        // name or a package version would orphan the existing row and silently restart the
        // schedule the first time any of those changed — a rename nobody would connect to
        // retention having stopped.
        var job = Build(retentionDays: null).Job;

        Assert.Equal("UBookItBookerRetention", job.Name);
        Assert.DoesNotContain(nameof(BookerRetentionJob), job.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nothing_the_sweep_logs_carries_a_person()
    {
        // `booker-erasure` requires contact details to have exactly one durable home, and names
        // retention reporting as the feature that would casually create a second. A log is
        // durable. This asserts over what the sweep actually emitted, with a booker whose values
        // are distinctive enough to find.
        // ALL FOUR members the requirement names, over ALL THREE paths that log. The first
        // version checked name and email on the success path only — a finding enumerates a
        // sample, not the population, and the warning and error paths are the ones where a
        // failure message could most plausibly carry a person.
        //
        // THE FIXTURE CARRIES A PHONE AND A MEMBER KEY, and that is the whole reason this reads
        // as it does. The first attempt at this widening guarded those two assertions with
        // `if (phone is not empty)` and `if (memberKey is Guid)` over the standard fixture, which
        // has neither — so both branches were dead and the suite could not tell a job that logged
        // a member key from one that did not. An assertion behind a condition the fixture never
        // satisfies is not a weak assertion, it is no assertion. The conditionals are gone and
        // the values are seeded instead, so every one of the four is exercised.
        //
        // The member key matters most of the four: it is the identifier that survives when a name
        // does not, which is why booker-erasure singles it out.
        static Booker Identifiable(string tag) => Booker.Create(
            Guid.NewGuid(), $"Distinctive {tag} Person", $"{tag}@example.com", $"0700 000 {tag}").Value;

        var succeeds = EndedDaysAgo(RetentionDays + 10, booker: Identifiable("111"));
        var fails = EndedDaysAgo(RetentionDays + 20, booker: Identifiable("222"));
        var throws = EndedDaysAgo(RetentionDays + 30, booker: Identifiable("333"));

        var harness = Build(seed: [succeeds, fails, throws]);

        // READ BEFORE THE SWEEP. The in-memory store keeps the caller's live instance and erases
        // it in place, so `Booker.Contact` is null by the time the sweep returns — reading it
        // afterwards throws, which is how this test first failed.
        var people = new[] { succeeds, fails, throws }
            .Select(b => (b.Booker.Contact!.Name, b.Booker.Contact!.Email, b.Booker.Contact!.Phone, b.Booker.MemberKey))
            .ToList();

        var store = harness.Store;
        var clock = new FixedTimeProvider(Now);
        var settings = TestData.Settings with { RetentionDays = RetentionDays };
        var services = new ServiceCollection();
        services.AddSingleton<IBookingStore>(store);
        services.AddSingleton<IResourceStore>(new InMemoryResourceStore());
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton(settings);
        services.AddScoped<IBookingService>(sp => new FailsAndThrows(
            new BookingService(sp.GetRequiredService<IResourceStore>(), store, clock, settings),
            returnsFailureFor: fails.Id,
            throwsFor: throws.Id));

        var provider = services.BuildServiceProvider();
        var logger = new CapturingLogger();
        var job = new BookerRetentionJob(
            provider.GetRequiredService<IServiceScopeFactory>(), settings, clock, logger);

        await job.ExecuteAsync(CancellationToken.None);

        var emitted = string.Join("\n", logger.Entries.Select(e => e.Message));

        // All three paths ran: one success line, one warning, one error.
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information);

        foreach (var (name, email, phone, memberKey) in people)
        {
            // Unconditional, all four. The fixture guarantees a phone and a member key are
            // present, so these are asserted rather than skipped — and the two guards below stop
            // that guarantee from quietly lapsing if the fixture is ever changed back.
            Assert.False(string.IsNullOrWhiteSpace(phone), "The fixture must carry a phone number, or the phone assertion below tests nothing.");
            Assert.NotNull(memberKey);

            Assert.DoesNotContain(name, emitted, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(email, emitted, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(phone!, emitted, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(memberKey!.Value.ToString(), emitted, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void The_job_is_registered_as_scheduled_work_whether_or_not_retention_is_configured()
    {
        // Registered UNCONDITIONALLY. A conditional registration would make the setting's effect
        // depend on the state of configuration at startup in a second, invisible way — a site
        // that corrected a mistyped value would still have no job to run.
        //
        // Asserted against the composer's own registration list rather than by resolving, which
        // would need a container Umbraco has already configured.
        var registrations = new ServiceCollection();
        new UBookItPersistenceComposer().Compose(new ServicesOnlyUmbracoBuilder(registrations));

        var job = Assert.Single(
            registrations, d => d.ServiceType == typeof(IDistributedBackgroundJob));

        Assert.Equal(typeof(BookerRetentionJob), job.ImplementationType);

        // A SINGLETON is what Umbraco's scheduler resolves, and it is also why the job may not
        // hold a scoped dependency — see the constructor guard below.
        Assert.Equal(ServiceLifetime.Singleton, job.Lifetime);
    }

    [Fact]
    public void The_job_captures_no_scoped_dependency()
    {
        // The job is a singleton resolved from the ROOT container while the store, the booking
        // service and the DbContext behind them are scoped. A singleton that took IBookingService
        // in its constructor would hold one DbContext for the lifetime of the application — and
        // would do so silently, since it would work perfectly on the first run.
        //
        // The scoped services this package registers, by name, so that adding one to the job's
        // constructor fails here rather than in production six weeks later.
        Type[] scoped =
        [
            typeof(IBookingStore),
            typeof(IBookingManagementStore),
            typeof(IBookingService),
            typeof(IResourceStore),
            typeof(IServiceStore),
            typeof(IAvailabilityQueryService),
        ];

        var taken = typeof(BookerRetentionJob)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        var offenders = taken.Where(scoped.Contains).ToList();

        Assert.True(
            offenders.Count == 0,
            "BookerRetentionJob is a singleton and must not hold a scoped service. It takes: "
            + string.Join(", ", offenders.Select(t => t.Name))
            + ". Resolve these from a scope created per unit of work instead.");

        Assert.Contains(typeof(IServiceScopeFactory), taken);
    }

    [Fact]
    public async Task A_scope_is_created_PER_BATCH_and_not_once_for_the_run()
    {
        // The other half of the persistence scenario — "within a scope it creates per unit of
        // work". The constructor assertion above covers only that no scoped service is captured;
        // hoisting CreateScope() out of the loop satisfies it while holding one DbContext for the
        // whole sweep, which on a first run over a large table is the whole sweep.
        //
        // Counted through a factory wrapping the real one, so this observes scopes actually
        // created rather than the shape of the code.
        var due = Enumerable.Range(0, (BookerRetentionJob.BatchSize * 2) + 10)
            .Select(i => EndedDaysAgo(RetentionDays + 1 + i))
            .ToList();

        var harness = Build(seed: due);
        var store = harness.Store;
        var clock = new FixedTimeProvider(Now);
        var settings = TestData.Settings with { RetentionDays = RetentionDays };

        var services = new ServiceCollection();
        services.AddSingleton<IBookingStore>(store);
        services.AddSingleton<IResourceStore>(new InMemoryResourceStore());
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton(settings);
        services.AddScoped<IBookingService, BookingService>();

        var provider = services.BuildServiceProvider();
        var counting = new CountingScopeFactory(provider.GetRequiredService<IServiceScopeFactory>());

        var job = new BookerRetentionJob(counting, settings, clock, new CapturingLogger());
        await job.ExecuteAsync(CancellationToken.None);

        // Three full batches plus the empty one that ends the run. More than one is the
        // guarantee; the exact count is asserted so hoisting the scope fails loudly rather than
        // merely differently.
        Assert.Equal(4, counting.ScopesCreated);
    }

    [Fact]
    public void Every_erasure_path_is_classified_under_one_of_the_two_requirements()
    {
        // THE TRIPWIRE, which was prose only until QA said so. `booker-erasure` requires that
        // every erasure path either carries sensitive-data access as its own authorization or
        // satisfies every obligation on an unattended path, and that there is no third option.
        // Without an enumeration a future third path trips nothing and the rule becomes advisory,
        // which is the failure this project keeps rediscovering.
        //
        // Recorded WITH the classification, so adding a path fails here AND reclassifying an
        // existing one fails — the escape-hatch lesson from find-by-booker R3.
        var classified = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Carries [Authorize(SensitiveDataAccessPolicy)] as its own authorization.
            ["BookingsController.EraseBooker"] = "caller-gated",

            // Unattended. Its obligations are asserted by the other tests in this class:
            // identifiers only, no contact detail handled, unreachable from a request.
            ["BookerRetentionJob.ExecuteAsync"] = "unattended",

            // The verb both paths go through — the single implementation, not a third path.
            ["BookingService.EraseBookerAsync"] = "the verb both paths use",

            // The storage half of that same verb: the one statement that actually clears the
            // columns. NOT a third path — nothing reaches it except the verb above — but it is
            // reached BY an erasure, so it is classified rather than excluded.
            //
            // It was previously hidden by a `!t.Name.StartsWith("Sql")` filter with no comment,
            // which is the escape-hatch defect this test was written to close, relocated from the
            // dictionary into the filter above it. An exemption keyed on a type NAME is worse
            // than the one it replaced: the `bookings` delta this change adds explicitly invites
            // hosts to write substitute store implementations, and any of them called Sql-anything
            // would have been silently unseen.
            ["SqlBookingStore.EraseBookerAsync"] = "the storage half of the verb",

            // The in-memory aggregate transition the verb performs. Also not a third path —
            // it writes nothing and is reachable only through the verb above, and `bookings`
            // separately requires that the erased state be reachable only by erasing a booking
            // or rehydrating one already erased.
            //
            // Recorded rather than excluded, because it was hidden by a
            // `t != typeof(Booking) && t != typeof(Booker)` clause carrying no comment — the
            // THIRD instance of the same escape-hatch shape in this one test, after the
            // dictionary comparison and the `Sql` name filter. Excluding the TYPE meant any
            // erase-shaped member added to `Booking` later would be invisible to the tripwire,
            // which is precisely what a tripwire must not permit.
            ["Booking.EraseBooker"] = "the domain transition the verb performs",
        };

        var assemblies = new[]
        {
            typeof(BookerRetentionJob).Assembly,
            typeof(BookingService).Assembly,
            typeof(UBookIt.Backoffice.Constants).Assembly,
        };

        var found = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.Namespace?.StartsWith("UBookIt", StringComparison.Ordinal) == true)
            .SelectMany(t => t
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                // Property accessors, not operations. `BookerModel.get_ErasedUtc` and
                // `BookingRow.set_BookerErasedUtc` match "Erase" as a substring and are places
                // erasure is REPORTED or STORED, never performed. Excluded by the compiler's own
                // marker rather than by a `get_`/`set_` name test, so an accessor cannot slip
                // through by being named unusually.
                .Where(m => !m.IsSpecialName)
                .Select(m => (Type: t, Method: m, Name: $"{t.Name}.{m.Name}")))
            .Where(x => x.Name.Contains("Erase", StringComparison.Ordinal)
                || x.Name == "BookerRetentionJob.ExecuteAsync")
            .DistinctBy(x => x.Name, StringComparer.Ordinal)
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();

        // THE CLASSIFICATION IS DERIVED FROM THE CODE, not read back from the record.
        //
        // This first compared only the KEYS, which made the classification decorative: relabelling
        // the retention job "caller-gated" passed. That is the escape-hatch defect from
        // find-by-booker R3 — a guard whose exemption list is itself unguarded — committed in the
        // very test whose comment cites it. Now each path's classification is worked out from what
        // the code actually is, so a wrong label fails and names both sides.
        static string Classify(Type type, MethodInfo method)
        {
            if (method.GetCustomAttributes<AuthorizeAttribute>()
                .Any(a => a.Policy == UBookIt.Backoffice.Constants.SensitiveDataAccessPolicy))
            {
                return "caller-gated";
            }

            if (typeof(IDistributedBackgroundJob).IsAssignableFrom(type))
            {
                return "unattended";
            }

            if (typeof(IBookingService).IsAssignableFrom(type))
            {
                return "the verb both paths use";
            }

            if (typeof(IBookingStore).IsAssignableFrom(type))
            {
                return "the storage half of the verb";
            }

            return type == typeof(Booking)
                ? "the domain transition the verb performs"
                : "UNCLASSIFIED — not gated, not unattended, and neither the verb, its storage, nor its domain transition";
        }

        var derived = found.ToDictionary(
            x => x.Name,
            x => Classify(x.Type, x.Method),
            StringComparer.Ordinal);

        Assert.Equal(
            classified.OrderBy(kv => kv.Key, StringComparer.Ordinal),
            derived.OrderBy(kv => kv.Key, StringComparer.Ordinal));

        // Anti-vacuity: an over-eager filter would turn this into a test that passes by looking
        // at nothing, which is exactly how a tripwire stops being one.
        Assert.Equal(5, found.Count);
    }

    [Fact]
    public void No_endpoint_can_reach_the_sweep()
    {
        // "No endpoint SHALL exist that erases many bookings at once." The sweep is deliberately
        // unreachable from request handling, and the mechanism is that the job is INTERNAL to
        // UBookIt.Persistence — the Backoffice and Web assemblies cannot name the type, so no
        // controller can call it however carelessly one is written.
        //
        // Asserted rather than assumed, because making the job public is a one-word edit that
        // produces no warning and breaks no behavioural test, and the requirement would be gone
        // with nothing in the diff that looks like its removal.
        Assert.False(
            typeof(BookerRetentionJob).IsPublic,
            "BookerRetentionJob is public. It must stay internal to UBookIt.Persistence so that "
            + "no controller in any other assembly can reach the sweep.");

        // And the sweep's own verb takes ONE booking, so there is no many-booking erase for an
        // endpoint to expose even if it could reach it.
        var eraseMany = typeof(IBookingService)
            .GetMethods()
            .Where(m => m.Name.Contains("Erase", StringComparison.Ordinal))
            .Where(m => m.GetParameters().Any(p =>
                p.ParameterType != typeof(Guid)
                && typeof(System.Collections.IEnumerable).IsAssignableFrom(p.ParameterType)
                && p.ParameterType != typeof(string)))
            .ToList();

        Assert.Empty(eraseMany);
    }

    [Fact]
    public void The_due_read_carries_no_contact_detail_in_or_out()
    {
        // The requirement that lets an erasure with no caller exist at all: the unattended path
        // handles nothing it would need permission to read. That is a property of this SIGNATURE,
        // so the signature is what is asserted — a guard on the method's NAME would not notice it
        // being "improved" into returning a row.
        var read = typeof(IBookingStore).GetMethod(nameof(IBookingStore.GetBookingIdsDueForErasureAsync))!;

        Assert.Equal(
            [typeof(DateTimeOffset), typeof(int), typeof(CancellationToken)],
            read.GetParameters().Select(p => p.ParameterType).ToArray());

        Assert.Equal(typeof(Task<IReadOnlyList<Guid>>), read.ReturnType);
    }

    private sealed class AlwaysFailsToErase : IBookingService
    {
        public Task<DomainResult<Booking>> PlaceAsync(BookingRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DomainResult<Booking>> PlaceAsync(MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DomainResult<Booking>> PlaceForServiceAsync(ServiceAttribution service, MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DomainResult<Booking>> CancelAsync(Guid bookingId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DomainResult<Booking>> EraseBookerAsync(Guid bookingId, CancellationToken cancellationToken = default)
            => Task.FromResult(DomainResult<Booking>.Failure(FailureCodes.BookingNotFound, "no"));

        public DomainResult CheckPlacementRules(Resource resource, DateTimeOffset start, TimeSpan duration)
            => throw new NotSupportedException();
    }

    /// <summary>Cancels the run from inside the loop, once <paramref name="after"/> erasures have succeeded.</summary>
    /// <remarks>
    /// The only way to reach the sweep's mid-batch cancellation check: the token has to become
    /// cancelled while the loop is running, and nothing outside the loop can time that reliably.
    /// </remarks>
    private sealed class CancelsAfter(IBookingService inner, CancellationTokenSource cts, int after) : IBookingService
    {
        private int _erased;

        public Task<DomainResult<Booking>> PlaceAsync(BookingRequest request, CancellationToken cancellationToken = default)
            => inner.PlaceAsync(request, cancellationToken);

        public Task<DomainResult<Booking>> PlaceAsync(MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
            => inner.PlaceAsync(request, cancellationToken);

        public Task<DomainResult<Booking>> PlaceForServiceAsync(ServiceAttribution service, MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
            => inner.PlaceForServiceAsync(service, request, cancellationToken);

        public Task<DomainResult<Booking>> CancelAsync(Guid bookingId, CancellationToken cancellationToken = default)
            => inner.CancelAsync(bookingId, cancellationToken);

        public async Task<DomainResult<Booking>> EraseBookerAsync(Guid bookingId, CancellationToken cancellationToken = default)
        {
            // Erase FIRST, then cancel. Cancelling before the erasure would leave the booking due
            // and make the count assertion pass for the wrong reason.
            var result = await inner.EraseBookerAsync(bookingId, cancellationToken);

            if (result.Succeeded && ++_erased == after)
            {
                await cts.CancelAsync();
            }

            return result;
        }

        public DomainResult CheckPlacementRules(Resource resource, DateTimeOffset start, TimeSpan duration)
            => inner.CheckPlacementRules(resource, start, duration);
    }

    /// <summary>Counts the scopes the job creates, delegating to the real factory.</summary>
    private sealed class CountingScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        public int ScopesCreated { get; private set; }

        public IServiceScope CreateScope()
        {
            ScopesCreated++;
            return inner.CreateScope();
        }
    }

    /// <summary>Fails one booking with a domain failure and throws for another.</summary>
    /// <remarks>
    /// Drives the sweep's warning path and its error path in a single run, so the logging
    /// assertion can observe every message the job is capable of emitting rather than the success
    /// line alone.
    /// </remarks>
    private sealed class FailsAndThrows(IBookingService inner, Guid returnsFailureFor, Guid throwsFor) : IBookingService
    {
        public Task<DomainResult<Booking>> PlaceAsync(BookingRequest request, CancellationToken cancellationToken = default)
            => inner.PlaceAsync(request, cancellationToken);

        public Task<DomainResult<Booking>> PlaceAsync(MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
            => inner.PlaceAsync(request, cancellationToken);

        public Task<DomainResult<Booking>> PlaceForServiceAsync(ServiceAttribution service, MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
            => inner.PlaceForServiceAsync(service, request, cancellationToken);

        public Task<DomainResult<Booking>> CancelAsync(Guid bookingId, CancellationToken cancellationToken = default)
            => inner.CancelAsync(bookingId, cancellationToken);

        public Task<DomainResult<Booking>> EraseBookerAsync(Guid bookingId, CancellationToken cancellationToken = default)
        {
            if (bookingId == throwsFor)
            {
                throw new InvalidOperationException($"Booking {bookingId} was erased but could not be read back.");
            }

            return bookingId == returnsFailureFor
                ? Task.FromResult(DomainResult<Booking>.Failure(FailureCodes.BookingNotFound, "no"))
                : inner.EraseBookerAsync(bookingId, cancellationToken);
        }

        public DomainResult CheckPlacementRules(Resource resource, DateTimeOffset start, TimeSpan duration)
            => inner.CheckPlacementRules(resource, start, duration);
    }

    private sealed class ThrowsForOne(IBookingService inner, Guid throwsFor) : IBookingService
    {
        public Task<DomainResult<Booking>> PlaceAsync(BookingRequest request, CancellationToken cancellationToken = default)
            => inner.PlaceAsync(request, cancellationToken);

        public Task<DomainResult<Booking>> PlaceAsync(MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
            => inner.PlaceAsync(request, cancellationToken);

        public Task<DomainResult<Booking>> PlaceForServiceAsync(ServiceAttribution service, MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
            => inner.PlaceForServiceAsync(service, request, cancellationToken);

        public Task<DomainResult<Booking>> CancelAsync(Guid bookingId, CancellationToken cancellationToken = default)
            => inner.CancelAsync(bookingId, cancellationToken);

        public Task<DomainResult<Booking>> EraseBookerAsync(Guid bookingId, CancellationToken cancellationToken = default)
            => bookingId == throwsFor
                ? throw new InvalidOperationException($"Booking {bookingId} was erased but could not be read back.")
                : inner.EraseBookerAsync(bookingId, cancellationToken);

        public DomainResult CheckPlacementRules(Resource resource, DateTimeOffset start, TimeSpan duration)
            => inner.CheckPlacementRules(resource, start, duration);
    }

    private sealed class CapturingLogger : ILogger<BookerRetentionJob>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
