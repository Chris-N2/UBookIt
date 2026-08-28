using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;

namespace UBookIt.Web.Theming;

/// <summary>
/// Whether a theme supplies everything a complete theme supplies — and, when it does
/// not, exactly what is missing.
/// <para>
/// <b>Public API on purpose.</b> Per-view fallback is the framework's default and it
/// is entirely silent: a theme supplying <c>Booking/Default</c> but not
/// <c>Booking/Unavailable</c> renders a mixture of two designs with no diagnostic at
/// all. What this class changes is the silence, not the fallback. It is public so a
/// theme author runs it from their own test suite and an incomplete theme fails
/// <i>their</i> build, before any site sees it — which is where the mistake is meant
/// to be caught. The residue is stated rather than argued away: a theme author who
/// never runs it still ships a mixed rendering, and only a log line reports it.
/// </para>
/// <para>
/// It checks <b>declared models as well as presence</b>. A view at the correct path
/// declaring a model it never receives satisfies a name-only check and then throws on
/// a visitor's request, which is a worse failure than the one being prevented.
/// </para>
/// </summary>
public static class UBookItThemeCompleteness
{
    /// <summary>
    /// Checks a theme supplied by one assembly. This is the overload a theme author
    /// calls from their own tests:
    /// <code>
    ///   Assert.Empty(UBookItThemeCompleteness.Check("mytheme", typeof(SomeTypeInMyTheme).Assembly));
    /// </code>
    /// </summary>
    /// <returns>
    /// One human-readable problem per required view the theme does not correctly
    /// supply, in the contract's own order. Empty means complete.
    /// </returns>
    public static IReadOnlyList<string> Check(string themeName, Assembly themeAssembly)
    {
        ArgumentNullException.ThrowIfNull(themeAssembly);

        var parts = new ApplicationPartManager();
        parts.ApplicationParts.Add(new CompiledRazorAssemblyPart(themeAssembly));

        // MVC's own views feature provider is internal, so a hand-built part manager
        // has none and PopulateFeature would return nothing — a check that reported
        // every view missing, for every theme, which is the shape of failure that
        // looks like a working guard. The provider below is what the framework's does
        // for a compiled-assembly part, and the two overloads therefore answer the
        // same question by the same route.
        parts.FeatureProviders.Add(new CompiledViewsFeatureProvider());

        return Check(themeName, parts);
    }

    /// <summary>
    /// Yields the views precompiled into each application part that carries any.
    /// </summary>
    private sealed class CompiledViewsFeatureProvider : IApplicationFeatureProvider<ViewsFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ViewsFeature feature)
        {
            foreach (var item in parts.OfType<IRazorCompiledItemProvider>()
                         .SelectMany(provider => provider.CompiledItems))
            {
                feature.ViewDescriptors.Add(new CompiledViewDescriptor(item));
            }
        }
    }

    /// <summary>
    /// Checks a theme against every view an application has, whichever assembly
    /// supplied it. This is the overload the boot-time report uses: at boot the
    /// question is what will actually resolve, and that is decided by the whole set
    /// of application parts rather than by one assembly.
    /// </summary>
    public static IReadOnlyList<string> Check(string themeName, ApplicationPartManager partManager)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeName);
        ArgumentNullException.ThrowIfNull(partManager);

        var feature = new ViewsFeature();
        partManager.PopulateFeature(feature);

        // RelativePath is the exact identifier the compiled view carries, which is
        // the same string the view-location format produces — so the two sides are
        // compared without either being reformatted. Case-insensitively, because
        // view paths are.
        var supplied = feature.ViewDescriptors
            .GroupBy(descriptor => descriptor.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var problems = new List<string>();

        foreach (var required in UBookItThemeContract.RequiredViews)
        {
            var path = required.PathIn(themeName);

            if (!supplied.TryGetValue(path, out var descriptor))
            {
                problems.Add($"{required} is missing: no view at {path}.");
                continue;
            }

            var declared = DeclaredModelOf(descriptor.Type);

            // Assignability, deliberately, and the rule is exactly "would this throw when a
            // visitor rendered it".
            //
            // A theme may declare a base type or an interface the model implements —
            // `@model IBookingFormView` for a view handed a BookingFormModel is correct and
            // renders — and equality would reject that for no reason a visitor could
            // observe. The same reasoning accepts a view with NO `@model` at all: it
            // compiles to RazorPage<dynamic>, whose type argument is `object`, so it takes
            // whatever it is handed. That is stated rather than left to be inferred from
            // `object.IsAssignableFrom` always being true.
            if (declared is null || !declared.IsAssignableFrom(required.ModelType))
            {
                problems.Add(
                    $"{required} at {path} declares model "
                    + $"{declared?.FullName ?? "none — it is not a compiled Razor view"}, but "
                    + $"receives {required.ModelType.FullName}, which is not assignable to it. "
                    + "This throws when a visitor renders it.");
            }
        }

        return problems;
    }

    /// <summary>
    /// The model a compiled Razor view declares, read off the generated type.
    /// <para>
    /// A view compiled from <c>@model T</c> derives from <c>RazorPage&lt;T&gt;</c>,
    /// so the declared model is that base's type argument. A view with no
    /// <c>@model</c> derives from <c>RazorPage&lt;dynamic&gt;</c> rather than from
    /// nothing, and yields <c>object</c> — which is why the caller's assignability rule
    /// accepts it. <c>null</c> therefore means the type is not a Razor page at all, which
    /// no view reached through a <c>ViewsFeature</c> should be; the caller reports it as a
    /// model mismatch rather than pretending it cannot happen.
    /// </para>
    /// </summary>
    private static Type? DeclaredModelOf(Type? viewType)
    {
        for (var type = viewType; type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(RazorPage<>))
            {
                return type.GetGenericArguments()[0];
            }
        }

        return null;
    }
}
