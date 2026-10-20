using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UBookIt.Core.Notifications;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using UBookIt.Web.Emails;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Exceptions;
using Umbraco.Cms.Core.Services;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// The startup report for template sets (service-email-templates: the <c>email-templates</c>
/// delta's "The startup report covers template sets"; design 5), against the fixture's real views —
/// set <c>fixture-set</c> supplies <c>BookerPlaced</c>, <c>BookerMoved</c> and <c>InternalPlaced</c>.
/// </summary>
public class EmailTemplateSetBootCheckTests
{
    private const string FixtureSet = "fixture-set";

    private sealed class StubRuntimeState(RuntimeLevel level) : IRuntimeState
    {
        public RuntimeLevel Level => level;

        public string? CurrentMigrationState => null;

        public string? FinalMigrationState => null;

        public RuntimeLevelReason Reason => RuntimeLevelReason.Run;

        public Umbraco.Cms.Core.Semver.SemVersion SemanticVersion => null!;

        public Version Version => new(17, 0, 0);

        public string VersionComment => string.Empty;

        public BootFailedException? BootFailedException => null;

        public IReadOnlyDictionary<string, object> StartupState => new Dictionary<string, object>();

        public void Configure(RuntimeLevel level, RuntimeLevelReason reason, Exception? bootFailedException = null)
            => throw new NotSupportedException("The check only reads the level.");

        public void DetermineRuntimeLevel()
            => throw new NotSupportedException("The check only reads the level.");
    }

    /// <summary>Pages through the services it holds, counting reads. A single read throws on demand.</summary>
    private sealed class ListingServiceStore(params Service[] services) : IServiceStore
    {
        public int Reads { get; private set; }

        public bool Throw { get; init; }

        public Task<Service?> GetAsync(Guid serviceId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The check lists services; it never reads one.");

        public Task<ServicePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
        {
            Reads++;

            return Throw
                ? throw new InvalidOperationException("Invalid object name 'uBookItService'.")
                : Task.FromResult(new ServicePage([.. services.Skip(skip).Take(take)], services.Length));
        }
    }

    private static Service Named(string name, string? set)
        => Service.Create(name, null, [new ServiceRole("room", 1)]).Value.WithTemplateSet(set).Value;

    private static (UBookItEmailTemplateSetBootCheck Check, EmailTemplateSetRenderingTests.Logs Logs) Check(
        IServiceStore store, RuntimeLevel level = RuntimeLevel.Run)
    {
        var (renderer, _) = EmailTemplateRenderingTests.Rig();
        var logs = new EmailTemplateSetRenderingTests.Logs();

        return (
            new UBookItEmailTemplateSetBootCheck(
                renderer, store, new StubRuntimeState(level), logs.CreateLogger<UBookItEmailTemplateSetBootCheck>()),
            logs);
    }

    [Fact]
    public async Task A_sets_supplied_messages_are_reported_with_both_halves()
    {
        var (check, logs) = Check(new ListingServiceStore(Named("Online consultation", FixtureSet)));

        var report = await check.RunAsync();

        var (set, messages) = Assert.Single(report);
        Assert.Equal(FixtureSet, set);
        Assert.Equal(Enum.GetValues<BookingMessageKind>().Length, messages.Count);
        Assert.Equal(
            [BookingMessageKind.BookerPlaced, BookingMessageKind.BookerMoved, BookingMessageKind.InternalPlaced],
            messages.Where(m => m.Supplied).Select(m => m.Kind).Order());

        var line = Assert.Single(logs.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains("fixture-set", line.Message, StringComparison.Ordinal);
        Assert.Contains("supplies: BookerPlaced, BookerMoved, InternalPlaced", line.Message, StringComparison.Ordinal);
        Assert.Contains("BookerConfirmed", line.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(logs.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task A_set_supplying_nothing_is_a_warning_naming_the_folder_and_the_lower_case_rule()
    {
        var (check, logs) = Check(new ListingServiceStore(Named("Online consultation", "remte")));

        var report = await check.RunAsync();

        Assert.All(Assert.Single(report).Messages, m => Assert.False(m.Supplied));

        var warning = Assert.Single(logs.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("remte", warning.Message, StringComparison.Ordinal);
        Assert.Contains("~/Views/Partials/UBookIt/Emails/remte/", warning.Message, StringComparison.Ordinal);
        Assert.Contains("lower case", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(logs.Entries, e => e.Level == LogLevel.Information);
    }

    [Fact]
    public async Task A_set_shared_by_two_services_is_reported_once()
    {
        var (check, logs) = Check(new ListingServiceStore(
            Named("Online consultation", FixtureSet),
            Named("Online follow-up", FixtureSet),
            Named("In person", null)));

        var report = await check.RunAsync();

        Assert.Equal(FixtureSet, Assert.Single(report).TemplateSet);
        Assert.Single(logs.Entries, e => e.Level == LogLevel.Information);
    }

    [Fact]
    public async Task Each_set_is_reported_in_ordinal_order()
    {
        var (check, _) = Check(new ListingServiceStore(
            Named("B", "zeta"), Named("A", FixtureSet), Named("C", "alpha")));

        var report = await check.RunAsync();

        Assert.Equal(["alpha", FixtureSet, "zeta"], report.Select(r => r.TemplateSet));
    }

    [Fact]
    public async Task No_sets_named_is_one_debug_line_and_nothing_louder()
    {
        var (check, logs) = Check(new ListingServiceStore(Named("In person", null)));

        Assert.Empty(await check.RunAsync());

        Assert.Single(logs.Entries, e => e.Level == LogLevel.Debug);
        Assert.DoesNotContain(logs.Entries, e => e.Level > LogLevel.Debug);
    }

    [Fact]
    public async Task Unreadable_services_are_one_warning_and_startup_continues()
    {
        var (check, logs) = Check(new ListingServiceStore { Throw = true });

        var report = await check.RunAsync();

        Assert.Empty(report);
        var warning = Assert.Single(logs.Entries, e => e.Level >= LogLevel.Warning);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("could not be checked", warning.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RuntimeLevel.Install)]
    [InlineData(RuntimeLevel.Upgrade)]
    [InlineData(RuntimeLevel.Boot)]
    [InlineData(RuntimeLevel.BootFailed)]
    [InlineData(RuntimeLevel.Unknown)]
    public async Task A_site_that_is_not_running_is_not_read_at_all(RuntimeLevel level)
    {
        var store = new ListingServiceStore(Named("Online consultation", FixtureSet));
        var (check, logs) = Check(store, level);

        Assert.Empty(await check.RunAsync());
        Assert.Equal(0, store.Reads);
        Assert.Empty(logs.Entries);
    }

    [Fact]
    public async Task Every_page_of_services_is_read()
    {
        // More services than one page holds, with the only set on the last page: a check that read
        // one page would report nothing at all.
        var services = Enumerable.Range(0, 450).Select(i => Named($"Service {i:D3}", null)).ToList();
        services.Add(Named("Service 999", FixtureSet));
        var store = new ListingServiceStore([.. services]);
        var (check, _) = Check(store);

        var report = await check.RunAsync();

        Assert.Equal(FixtureSet, Assert.Single(report).TemplateSet);
        Assert.Equal(3, store.Reads);
    }

    [Fact]
    public async Task The_report_for_the_views_for_every_service_is_unchanged()
    {
        var (renderer, _) = EmailTemplateRenderingTests.Rig();
        var general = new UBookItEmailTemplateBootCheck(renderer, NullLogger<UBookItEmailTemplateBootCheck>.Instance);
        var before = general.Run();

        var (check, logs) = Check(new ListingServiceStore(Named("Online consultation", FixtureSet), Named("Typo", "remte")));
        await check.RunAsync();

        Assert.Equal(before, general.Run());

        // And this check never restates it: every line it wrote is about a set.
        Assert.All(logs.Entries, e => Assert.Contains("set", e.Message, StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Entries, e => e.Message.Contains("is using site-supplied email content", StringComparison.Ordinal));
    }
}
