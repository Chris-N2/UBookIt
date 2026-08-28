using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UBookIt.Tests.Rendering.Support;
using UBookIt.Web.Rendering;
using UBookIt.Web.Theming;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// What a complete theme supplies, what the check says when one is not, and what a
/// site does with an incomplete theme.
/// </summary>
public class ThemeCompletenessTests
{
    private static readonly System.Reflection.Assembly Fixture =
        ThemedRendering.ThemeAssemblyUnderTest;

    [Fact]
    public void The_required_view_set_names_every_view_of_both_components_with_its_model()
    {
        // Published so a theme author learns it from the package rather than by
        // reading the package's source or by observing which pages come out wrong.
        // Tied to the shipped views rather than restated, so adding an eleventh view
        // to a component cannot leave the contract behind.
        var contract = UBookItThemeContract.RequiredViews
            .Select(view => $"~/Views/Shared/Components/{view.ComponentName}/{view.ViewName}.cshtml")
            .Order(StringComparer.Ordinal);

        var componentViews = ViewInventory.Shipped
            .Where(view => view.Contains("/Components/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

        Assert.Equal(componentViews, contract);
    }

    [Fact]
    public void Every_published_model_is_the_model_the_packages_own_view_declares()
    {
        // The half of the contract that had no covering test. The view NAMES were bound to
        // the shipped set; the MODELS were asserted only to be non-null, which is
        // unfalsifiable for a list of literals of a non-nullable type. A wrong ModelType
        // would have been wrong in the contract and in the fixture together, and every
        // test would still have passed.
        //
        // Read off the package's own compiled views — the same artefact the site serves —
        // so this is a comparison against what actually renders rather than against a
        // second copy of the contract. The model reader is duplicated from
        // UBookItThemeCompleteness on purpose: sharing it would make this test agree with
        // the thing it is checking.
        var declared = DescriptorsOf(ViewRenderer.ViewAssembly)
            .ToDictionary(
                descriptor => descriptor.RelativePath,
                descriptor => DeclaredModelOf(descriptor.Type),
                StringComparer.OrdinalIgnoreCase);

        foreach (var required in UBookItThemeContract.RequiredViews)
        {
            var path =
                $"/Views/Shared/Components/{required.ComponentName}/{required.ViewName}.cshtml";

            Assert.True(
                declared.TryGetValue(path, out var model),
                $"The contract publishes {required}, but the package ships no view at {path}.");

            Assert.Equal(required.ModelType, model);
        }
    }

    private static IReadOnlyList<Microsoft.AspNetCore.Mvc.Razor.Compilation.CompiledViewDescriptor>
        DescriptorsOf(System.Reflection.Assembly assembly)
    {
        var part = new CompiledRazorAssemblyPart(assembly);

        return
        [
            .. ((IRazorCompiledItemProvider)part).CompiledItems
                .Select(item => new Microsoft.AspNetCore.Mvc.Razor.Compilation.CompiledViewDescriptor(item)),
        ];
    }

    /// <summary>
    /// The model a compiled Razor view declares. Deliberately a second implementation —
    /// see the caller.
    /// </summary>
    private static Type? DeclaredModelOf(Type? viewType)
    {
        for (var type = viewType; type is not null; type = type.BaseType)
        {
            if (type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(Microsoft.AspNetCore.Mvc.Razor.RazorPage<>))
            {
                return type.GetGenericArguments()[0];
            }
        }

        return null;
    }

    [Fact]
    public void A_complete_theme_has_no_problems()
    {
        Assert.Empty(UBookItThemeCompleteness.Check(ThemeFixture.ThemeFixture.Complete, Fixture));
    }

    [Fact]
    public void An_incomplete_theme_fails_the_theme_authors_own_build_naming_each_missing_view()
    {
        // This is the call a theme author makes from their own test suite, which is
        // where an incomplete theme is meant to be caught.
        var problems = UBookItThemeCompleteness.Check(ThemeFixture.ThemeFixture.Partial, Fixture);

        // The fixture supplies two of the ten.
        Assert.Equal(8, problems.Count);

        var supplied = new[] { "Booking/Default", "BookingFlow/Catalogue" };

        foreach (var required in UBookItThemeContract.RequiredViews)
        {
            var name = $"{required.ComponentName}/{required.ViewName}";

            if (supplied.Contains(name, StringComparer.Ordinal))
            {
                Assert.DoesNotContain(problems, problem => problem.StartsWith(name, StringComparison.Ordinal));
                continue;
            }

            // Named, and named with the path — a theme author fixing this needs to
            // know where to put the file, not only that something is absent.
            Assert.Contains(
                problems,
                problem => problem.StartsWith(name, StringComparison.Ordinal)
                    && problem.Contains(required.PathIn(ThemeFixture.ThemeFixture.Partial), StringComparison.Ordinal));
        }
    }

    [Fact]
    public void A_view_with_the_wrong_model_is_incomplete_rather_than_complete()
    {
        // The failure a name-only check passes and a visitor's request throws on. The
        // fixture's `wrongmodel` theme supplies Booking/Default declaring
        // CatalogueModel, which is a real view at the right path.
        var problems = UBookItThemeCompleteness.Check(ThemeFixture.ThemeFixture.WrongModel, Fixture);

        var model = Assert.Single(
            problems,
            problem => problem.StartsWith("Booking/Default", StringComparison.Ordinal));

        // Not reported as missing — it is there. Reported as the wrong model, naming
        // the model it declares and the one it receives, because those are the two
        // facts needed to fix it.
        Assert.DoesNotContain("is missing", model, StringComparison.Ordinal);
        Assert.Contains(typeof(CatalogueModel).FullName!, model, StringComparison.Ordinal);
        Assert.Contains(typeof(BookingFormModel).FullName!, model, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_theme_may_declare_a_base_type_or_interface_the_model_satisfies()
    {
        // Assignability rather than equality. `@model IBookingFormView` for a view
        // handed a BookingFormModel is correct and renders, and rejecting it would be
        // a rule about the package's taste rather than about what a visitor can
        // observe. The fixture's `basemodel` theme declares exactly that.
        var problems = UBookItThemeCompleteness.Check(ThemeFixture.ThemeFixture.BaseModel, Fixture);

        Assert.DoesNotContain(
            problems,
            problem => problem.StartsWith("Booking/Default", StringComparison.Ordinal));

        // And it really does render with the model the component passes, so the
        // check's leniency is not leniency about something broken.
        var renderer = new ViewRenderer(new ThemedRendering(ThemeFixture.ThemeFixture.BaseModel));

        var html = await renderer.RenderByNameAsync(
            "Components/Booking/Default",
            ViewFixtures.For(ViewInventory.ResourceFlow)[0].Model);

        Assert.Contains("data-theme=\"basemodel\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void An_incomplete_theme_is_logged_at_boot_naming_each_missing_view()
    {
        var (check, log) = BootCheck(ThemeFixture.ThemeFixture.Partial);

        var problems = check.Run();

        var entry = Assert.Single(log.Errors);

        // The content, not merely that something was logged. A log line that does not
        // name the missing views leaves a reader exactly where the silent fallback
        // left them.
        Assert.Contains(ThemeFixture.ThemeFixture.Partial, entry, StringComparison.Ordinal);

        foreach (var problem in problems)
        {
            Assert.Contains(problem, entry, StringComparison.Ordinal);
        }

        Assert.Contains("BookingFlow/Service", entry, StringComparison.Ordinal);
        Assert.Contains("Booking/Unavailable", entry, StringComparison.Ordinal);
    }

    [Fact]
    public void A_complete_theme_logs_nothing()
    {
        // So the check cannot pass by always complaining — which is the shape a
        // boot-time reporter degrades into once its message stops being read.
        var (check, log) = BootCheck(ThemeFixture.ThemeFixture.Complete);

        Assert.Empty(check.Run());
        Assert.Empty(log.Errors);
    }

    [Fact]
    public void No_theme_is_not_an_incomplete_theme()
    {
        var (check, log) = BootCheck(themeName: null);

        Assert.Empty(check.Run());
        Assert.Empty(log.Errors);
    }

    [Fact]
    public void The_boot_check_reports_a_theme_that_is_registered_but_does_not_resolve_first()
    {
        // The half of the boot check that exists because the registration mechanism has
        // been wrong once. It asserts the guarantee — what the engine will search first —
        // not that an expander is present or where it came from.
        var (check, log) = BootCheck(
            ThemeFixture.ThemeFixture.Complete, ThemeRegistrationOrder.RejectedConfigureBeforeHost);

        var problems = check.Run();

        Assert.Contains(problems, problem => problem.Contains("does not resolve first", StringComparison.Ordinal));

        var entry = Assert.Single(log.Errors, error => error.Contains("will not be used", StringComparison.Ordinal));

        // Names both locations, because "the theme is not winning" without saying what IS
        // winning leaves a reader with nowhere to go.
        Assert.Contains(
            "/Views/Shared/UBookIt/Themes/complete/{0}.cshtml", entry, StringComparison.Ordinal);
        Assert.Contains("App_Plugins", entry, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ThemeResolutionTests.SupportedOrders), MemberType = typeof(ThemeResolutionTests))]
    public void The_boot_check_is_silent_for_every_supported_call_order(ThemeRegistrationOrder order)
    {
        // So the ordering check cannot pass by always complaining, and so a call order a
        // site is told is safe is asserted to be safe rather than assumed.
        var (check, log) = BootCheck(ThemeFixture.ThemeFixture.Complete, order);

        Assert.Empty(check.Run());
        Assert.Empty(log.Errors);
    }

    [Fact]
    public async Task A_view_the_theme_omits_renders_the_packages_own_view()
    {
        // Per-view fallback is the framework's default, so this test has to prove the
        // PACKAGE's handling of it rather than the framework's: with the incomplete
        // theme registered by the package's own registration step, the view the theme
        // supplies is the theme's and the view it omits is the package's — in one
        // configuration, resolved through one chain.
        var renderer = new ViewRenderer(new ThemedRendering(ThemeFixture.ThemeFixture.Partial));

        var themed = renderer.ResolveByName("Components/Booking/Default");

        Assert.True(themed.Success);
        Assert.Equal(
            "/Views/Shared/UBookIt/Themes/partial/Components/Booking/Default.cshtml",
            themed.View.Path);

        var fellBack = renderer.ResolveByName("Components/Booking/Unavailable");

        Assert.True(fellBack.Success);
        Assert.Equal("/Views/Shared/Components/Booking/Unavailable.cshtml", fellBack.View.Path);

        // And it renders, rather than merely resolving: an incomplete theme leaves a
        // site operable, which is the whole reason boot logs instead of failing.
        var html = await renderer.RenderByNameAsync(
            "Components/Booking/Unavailable",
            ViewFixtures.For("~/Views/Shared/Components/Booking/Unavailable.cshtml")[0].Model);

        Assert.Contains("ubookit-", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-theme=", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds the boot check over options produced by the package's <b>real</b>
    /// registration, so the ordering half of the check is exercised against the chain a
    /// site would actually get rather than one hand-assembled to pass.
    /// </summary>
    private static (UBookItThemeBootCheck Check, CapturingLogger Log) BootCheck(
        string? themeName,
        ThemeRegistrationOrder order = ThemeRegistrationOrder.PackageAfterHost)
    {
        var services = new ServiceCollection();
        services.AddOptions();

        if (themeName is not null)
        {
            new ThemedRendering(themeName, order).ApplyTo(services);
        }

        var provider = services.BuildServiceProvider();

        var parts = new ApplicationPartManager();
        parts.ApplicationParts.Add(new CompiledRazorAssemblyPart(Fixture));
        parts.FeatureProviders.Add(new PartFeatureProvider());

        var log = new CapturingLogger();

        return (
            new UBookItThemeBootCheck(
                parts,
                provider.GetRequiredService<IOptions<UBookItThemeOptions>>(),
                provider.GetRequiredService<IOptions<RazorViewEngineOptions>>(),
                log),
            log);
    }

    /// <summary>
    /// The views feature provider MVC registers for a compiled-assembly part. MVC's
    /// own is internal, so a hand-built part manager needs one.
    /// </summary>
    private sealed class PartFeatureProvider
        : IApplicationFeatureProvider<Microsoft.AspNetCore.Mvc.Razor.Compilation.ViewsFeature>
    {
        public void PopulateFeature(
            IEnumerable<ApplicationPart> parts,
            Microsoft.AspNetCore.Mvc.Razor.Compilation.ViewsFeature feature)
        {
            foreach (var item in parts.OfType<IRazorCompiledItemProvider>()
                         .SelectMany(provider => provider.CompiledItems))
            {
                feature.ViewDescriptors.Add(
                    new Microsoft.AspNetCore.Mvc.Razor.Compilation.CompiledViewDescriptor(item));
            }
        }
    }

    /// <summary>Keeps the formatted error messages, so a test can assert their content.</summary>
    private sealed class CapturingLogger : ILogger<UBookItThemeBootCheck>
    {
        public List<string> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                Errors.Add(formatter(state, exception));
            }
        }
    }
}
