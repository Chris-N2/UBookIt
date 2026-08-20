using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests.Rendering.Support;

/// <summary>
/// Renders a shipped view to HTML, through the real Razor view engine.
/// <para>
/// The views are <b>compiled into <c>UBookIt.Web.dll</c></b> — the project builds
/// with <c>Microsoft.NET.Sdk.Razor</c> and <c>AddRazorSupportForMvc</c> — so this
/// renders the very artefact the site serves rather than re-parsing the
/// <c>.cshtml</c> files from disk. A rig that re-parsed the source would be
/// testing a second copy of the views, free to disagree with the shipped one:
/// exactly the class of fault this project keeps finding elsewhere, and absurd to
/// introduce in the suite built to catch markup defects (design D1).
/// </para>
/// <para>
/// Views are addressed by <b>absolute path</b>, the way the flow views already
/// reference their partials. Name-based lookup would depend on controller and
/// view-component conventions this rig has no business reproducing.
/// </para>
/// <para>
/// <c>UBookIt.Web</c> has no <c>_ViewStart.cshtml</c> and no
/// <c>_ViewImports.cshtml</c>, so there is no layout to satisfy and no implicit
/// <c>@using</c> to reproduce: each view renders standalone. (That absent
/// <c>_ViewImports</c> is also why a tag helper degrades to visible text on the
/// page rather than failing the build — the hazard this suite exists to catch.)
/// </para>
/// </summary>
public sealed class ViewRenderer
{
    private readonly IServiceProvider _services;
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempData;

    public ViewRenderer()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILoggerFactory>(new LoggerFactory());
        services.AddLogging();
        // MVC resolves both shapes of the same listener.
        var listener = new DiagnosticListener("UBookIt.Tests.Rendering");
        services.AddSingleton<DiagnosticSource>(listener);
        services.AddSingleton(listener);
        services.AddSingleton<IWebHostEnvironment>(new RenderingEnvironment());

        services
            .AddMvcCore()
            .AddRazorViewEngine()

            // The compiled views live here. Adding the assembly as an application
            // part is what makes them discoverable; nothing is read from disk.
            .ConfigureApplicationPartManager(parts =>
                parts.ApplicationParts.Add(
                    new CompiledRazorAssemblyPart(typeof(BookingKeys).Assembly)));

        _services = services.BuildServiceProvider();
        _viewEngine = _services.GetRequiredService<IRazorViewEngine>();
        _tempData = _services.GetRequiredService<ITempDataProvider>();
    }

    /// <summary>
    /// The content root the view engine was given — a <c>NullFileProvider</c>, so
    /// there is no directory of <c>.cshtml</c> files to fall back to. Exposed so a
    /// test can assert the rig's central claim structurally rather than infer it.
    /// </summary>
    public IFileProvider ContentRootFileProvider
        => _services.GetRequiredService<IWebHostEnvironment>().ContentRootFileProvider;

    /// <summary>The assembly the rig renders from — asserted, not assumed.</summary>
    public static System.Reflection.Assembly ViewAssembly => typeof(BookingKeys).Assembly;

    /// <summary>
    /// Renders one view with one model, returning the HTML a browser would receive.
    /// </summary>
    /// <param name="viewPath">
    /// Absolute view path, e.g. <c>~/Views/Shared/UBookIt/_Times.cshtml</c>.
    /// </param>
    public async Task<string> RenderAsync(string viewPath, object? model)
    {
        var view = FindView(viewPath);

        var httpContext = new DefaultHttpContext { RequestServices = _services };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        var viewData = new ViewDataDictionary(
            new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model,
        };

        await using var writer = new StringWriter();

        var viewContext = new ViewContext(
            actionContext,
            view,
            viewData,
            new TempDataDictionary(httpContext, _tempData),
            writer,
            new HtmlHelperOptions());

        await view.RenderAsync(viewContext);

        return writer.ToString();
    }

    /// <summary>
    /// Resolves a view by absolute path, failing loudly with the engine's own
    /// searched locations — a rig that cannot find a view should say where it
    /// looked, or every downstream failure reads as "the view is broken".
    /// </summary>
    private IView FindView(string viewPath)
    {
        var result = _viewEngine.GetView(executingFilePath: null, viewPath, isMainPage: true);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Could not resolve view '{viewPath}'. Searched: "
                + string.Join(", ", result.SearchedLocations ?? []));
        }

        return result.View;
    }

    /// <summary>
    /// The minimum an <see cref="IWebHostEnvironment"/> must answer for the view
    /// engine to start.
    /// <para>
    /// The content root is deliberately a <see cref="NullFileProvider"/>: there is
    /// no directory of <c>.cshtml</c> files for the engine to fall back to, so a
    /// view that resolves can only have come from the compiled assembly. That is
    /// the rig's central claim made structural rather than asserted in a comment.
    /// </para>
    /// </summary>
    private sealed class RenderingEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = typeof(BookingKeys).Assembly.GetName().Name!;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = "Test";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = string.Empty;
    }
}
