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
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Website.Collections;
using Umbraco.Cms.Web.Website.Controllers;

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
        : this(null)
    {
    }

    /// <param name="theme">
    /// The theme configuration to build the rig with, or <c>null</c> for an unthemed
    /// site — which is what every rule about the shipped views uses, and what the
    /// parameterless constructor gives.
    /// </param>
    public ViewRenderer(ThemedRendering? theme)
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
            //
            // A theme adds a SECOND compiled assembly, exactly as a theme package
            // would — two precompiled application parts, at different paths, which is
            // the arrangement whose precedence this suite measures.
            .ConfigureApplicationPartManager(parts =>
            {
                parts.ApplicationParts.Add(
                    new CompiledRazorAssemblyPart(typeof(BookingKeys).Assembly));

                if (theme is not null)
                {
                    parts.ApplicationParts.Add(
                        new CompiledRazorAssemblyPart(theme.ThemeAssembly));
                }
            });

        // What `Html.BeginUmbracoForm` needs, and nothing more.
        //
        // Measured against the resolved Umbraco 17.6.2 binaries by rendering the
        // views, not inferred: the call resolves exactly these two services.
        // `AddDataProtection()` and `AddAntiforgery()` are deliberately ABSENT —
        // `AddMvcCore` already supplies both, and the forms emit a real
        // `__RequestVerificationToken` and a real encrypted `ufprt` without them.
        // Adding them "for safety" would make this rig overstate what the shipped
        // artefact needs, which is the one thing it exists not to do (design D2).
        services.AddSingleton(new SurfaceControllerTypeCollection(SurfaceControllerTypes));
        services.AddSingleton<IUmbracoContextAccessor, RenderingUmbracoContextAccessor>();

        theme?.ApplyTo(services);

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
    public Task<string> RenderAsync(string viewPath, object? model)
        => RenderAsync(FindView(viewPath), model);

    /// <summary>
    /// Resolves a view the way a <b>view component</b> resolves one: by qualified
    /// name, through the whole view-location chain, rather than by absolute path.
    /// <para>
    /// This is the call that consults <c>ViewLocationFormats</c> and therefore the
    /// expander chain. <c>GetView</c> — what <see cref="RenderAsync(string, object?)"/>
    /// uses — does not, because it only handles <c>~/</c>, <c>/</c> and a
    /// <c>.cshtml</c> suffix, which is why view-component resolution tries it first,
    /// gets NotFound for a bare name, and falls through to here.
    /// </para>
    /// </summary>
    /// <param name="qualifiedViewName">
    /// The name a view component asks for, e.g. <c>Components/Booking/Default</c>.
    /// </param>
    public ViewEngineResult ResolveByName(string qualifiedViewName)
    {
        var httpContext = new DefaultHttpContext { RequestServices = _services };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        return _viewEngine.FindView(actionContext, qualifiedViewName, isMainPage: false);
    }

    /// <summary>
    /// Renders whatever <see cref="ResolveByName"/> resolves — so what is rendered is
    /// decided by the chain rather than by the test naming a file.
    /// </summary>
    public Task<string> RenderByNameAsync(string qualifiedViewName, object? model)
    {
        var result = ResolveByName(qualifiedViewName);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Could not resolve view '{qualifiedViewName}'. Searched: "
                + string.Join(", ", result.SearchedLocations ?? []));
        }

        return RenderAsync(result.View, model);
    }

    private async Task<string> RenderAsync(IView view, object? model)
    {
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
    /// The package's surface controllers, discovered rather than listed.
    /// <para>
    /// `BeginUmbracoForm&lt;T&gt;` resolves this collection to map a surface
    /// controller <b>type</b> to its controller <b>name</b>, and throws if the type
    /// is not a member. Scanning means a surface controller added later is a member
    /// automatically; a hand-kept list would leave its view failing to render for a
    /// reason pointing at this file rather than at the omission.
    /// </para>
    /// </summary>
    private static IEnumerable<Type> SurfaceControllerTypes()
        => typeof(BookingKeys).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsPublic: true }
                && typeof(SurfaceController).IsAssignableFrom(type));

    /// <summary>
    /// Answers "is there an Umbraco request in flight" with a context that knows
    /// one thing: the URL the page was requested at.
    /// <para>
    /// It must answer <c>true</c>. The call site is
    /// <c>GetRequiredUmbracoContext()</c>, so an accessor that holds nothing throws
    /// "Wasn't able to get an UmbracoContext" rather than degrading.
    /// </para>
    /// </summary>
    private sealed class RenderingUmbracoContextAccessor : IUmbracoContextAccessor
    {
        public bool TryGetUmbracoContext(out IUmbracoContext umbracoContext)
        {
            umbracoContext = new RenderingUmbracoContext();
            return true;
        }

        public void Clear()
        {
        }

        public void Set(IUmbracoContext umbracoContext)
        {
        }
    }

    /// <summary>
    /// The one fact <c>BeginUmbracoForm</c> reads off an Umbraco context, and
    /// nothing else.
    /// <para>
    /// <c>HtmlHelperRenderExtensions</c> takes exactly
    /// <c>OriginalRequestUrl.PathAndQuery</c> and makes it the form's
    /// <c>action</c>. Every other member is unread, so every other member throws.
    /// </para>
    /// <para>
    /// <b>The throwing members are the design, not an unfinished stub — do not
    /// give them benign return values</b> (design D1). If a future Umbraco version
    /// reads <c>PublishedRequest</c> or a cache, this rig fails loudly, naming the
    /// member, and someone decides what the honest answer is. A stub returning
    /// plausible empties would instead let the rig render a page the site cannot
    /// produce and report it green — which is precisely the class of fault this
    /// suite exists to catch, reintroduced inside the suite itself.
    /// </para>
    /// <para>
    /// <c>CleanedUmbracoUrl</c> throws alongside the rest even though it exists and
    /// would be trivial to populate: it is not read, and populating it would invent
    /// a fact about the collaboration.
    /// </para>
    /// </summary>
    private sealed class RenderingUmbracoContext : IUmbracoContext
    {
        /// <summary>The form's <c>action</c> is this URL's path and query.</summary>
        public Uri OriginalRequestUrl { get; } = new("https://example.org/book");

        public Uri CleanedUmbracoUrl => throw Unread();

        public DateTime ObjectCreated => throw Unread();

        public IPublishedContentCache Content => throw Unread();

        public IPublishedMediaCache Media => throw Unread();

        public IDomainCache Domains => throw Unread();

        public IPublishedRequest? PublishedRequest
        {
            get => throw Unread();
            set => throw Unread();
        }

        public bool IsDebug => throw Unread();

        public bool InPreviewMode => throw Unread();

        public void Dispose()
        {
        }

        private static NotSupportedException Unread(
            [System.Runtime.CompilerServices.CallerMemberName] string member = "")
            => new(
                $"The rendering rig's Umbraco context does not answer '{member}'. "
                + "Only OriginalRequestUrl is read when rendering the shipped views "
                + "(BeginUmbracoForm takes its PathAndQuery as the form action). "
                + "Something now reads more than that: decide what the honest answer "
                + "is rather than making this member return an empty value.");
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
