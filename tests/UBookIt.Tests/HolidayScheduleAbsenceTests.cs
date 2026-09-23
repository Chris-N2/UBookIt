using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Nothing in the package reaches a site's holiday source except in response to an operator's
/// request.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the change's central claim, so it is scanned for rather than reasoned about.</b> If
/// an import ran on a timer, a closure an operator deleted would return at the next run and they
/// could not be rid of it without the package remembering every deletion — invisible state that
/// must then expire on a rule nobody can see. "Nothing schedules it" is what makes the absence of
/// that machinery safe, and a claim like that decays silently: somebody adds a job, and every
/// other test still passes.
/// </para>
/// <para>
/// Two assertions, because they fail for different reasons: <b>who may hold a source</b> (a type
/// that cannot reach it cannot call it on a timer), and <b>who may call one</b> (the surviving
/// holder must be the request-scoped service, not a job).
/// </para>
/// </remarks>
public class HolidayScheduleAbsenceTests
{
    private const string SourceDirectory = "src";

    private const string Port = "IPublicHolidaySource";

    private static IReadOnlyList<(string Name, string Text)> ShippedSources()
        => RepoFiles.Paths(SourceDirectory, "*.cs")
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}UBookIt.TestSite{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => (Path.GetFileName(path), File.ReadAllText(path)))
            .ToList();

    [Fact]
    public void Only_three_shipped_files_mention_a_holiday_source_at_all()
    {
        // The definition, the one service that reads it, and the composer that registers it.
        // A fourth file naming this port is a new way to reach a site's own code, and it should
        // have to be argued for here rather than arriving quietly.
        var mentions = ShippedSources()
            .Where(file => file.Text.Contains(Port, StringComparison.Ordinal))
            .Select(file => file.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            new[]
            {
                "HolidayPreviewService.cs",
                "PublicHolidaySource.cs",
                "UBookItPersistenceComposer.cs",
            },
            mentions);
    }

    [Fact]
    public void Only_the_preview_service_calls_a_source()
    {
        // Holding a reference is not calling one: the composer NAMES the port in order to resolve
        // it optionally, which is registration rather than a call. What must stay singular is the
        // place the site's own code is actually invoked.
        var callers = ShippedSources()
            .Where(file => Regex.IsMatch(file.Text, @"source\??\.GetAsync\("))
            .Select(file => file.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(new[] { "HolidayPreviewService.cs" }, callers);
    }

    /// <summary>
    /// No type that RUNS ON ITS OWN can reach a holiday source or the service that reads one.
    /// </summary>
    /// <remarks>
    /// Type-level rather than file-level, and deliberately: the persistence composer legitimately
    /// contains both the retention job and the holiday registration, so a file that mentions both
    /// proves nothing. What matters is whether a self-starting TYPE can obtain one — asserted over
    /// its constructor, which is how every collaborator in this package is supplied.
    /// </remarks>
    [Fact]
    public void No_self_starting_type_can_reach_a_holiday_source()
    {
        var schedulingShapes = new[]
        {
            "IDistributedBackgroundJob",
            "IRecurringBackgroundJob",
            "IHostedService",
            "INotificationAsyncHandler`1",
            "INotificationHandler`1",
        };

        var assemblies = new[]
        {
            typeof(UBookIt.Core.Availability.IPublicHolidaySource).Assembly,
            typeof(UBookIt.Persistence.UBookItDbContext).Assembly,
            typeof(UBookIt.Backoffice.Constants).Assembly,
        };

        var selfStarting = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.GetInterfaces()
                .Any(contract => schedulingShapes.Contains(contract.Name, StringComparer.Ordinal)))
            .ToList();

        // The scan must be finding the jobs that DO exist, or it proves nothing about the one
        // that must not.
        Assert.NotEmpty(selfStarting);

        var forbidden = new[]
        {
            typeof(UBookIt.Core.Availability.IPublicHolidaySource),
            typeof(UBookIt.Core.Availability.IHolidayPreviewService),
        };

        var offenders = selfStarting
            .Where(type => type.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => forbidden.Contains(parameter.ParameterType)))
            .Select(type => type.Name)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "These types run on their own AND can reach a holiday source, which would make the "
            + "import automatic — and an automatic import makes a deleted closure impossible to "
            + "delete: " + string.Join(", ", offenders));
    }

    [Fact]
    public void The_scan_is_looking_at_something()
    {
        // A scan over nothing passes every assertion made about it. This is what keeps a moved
        // directory or a renamed port from reading as a clean bill of health.
        var files = ShippedSources();

        Assert.True(files.Count > 100, $"Only {files.Count} shipped source files were scanned.");
        Assert.Contains(files, file => file.Text.Contains(Port, StringComparison.Ordinal));
    }
}
