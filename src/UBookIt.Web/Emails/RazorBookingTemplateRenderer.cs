using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using UBookIt.Core.Notifications;

namespace UBookIt.Web.Emails;

/// <summary>
/// Renders a site's own message content from a Razor view.
/// </summary>
/// <remarks>
/// <para>
/// <b>It builds its own <see cref="DefaultHttpContext"/> from the root service provider rather
/// than using an ambient one</b>, and that is the whole reason this type can be used at all.
/// Messages are composed from work that has no request — the retention sweep already runs that
/// way, and a reminder would — so a renderer that reached for <c>IHttpContextAccessor</c> would
/// work in a controller and fail in a job, which is the least observable place to fail. Umbraco's
/// own view-to-string recipe (<c>PartialViewBlockEngine</c>) resolves its services from
/// <c>httpContext.RequestServices</c>; that is that type's choice, not a framework requirement.
/// </para>
/// <para>
/// <b>It never throws.</b> A caller has a booking that is already stored and a message that must
/// still go out, so a template's fault is reported as <see cref="BookingTemplateOutcome.Failed"/>
/// and the package's own wording is sent instead. Letting an exception escape would cost the
/// message entirely — the opposite of the trade every failure path in this package makes.
/// </para>
/// <para>
/// <b>Absent and broken are reported differently</b>, because they mean different things to the
/// person who wrote the template: one is "you have not supplied this", the other is "yours did
/// not work".
/// </para>
/// </remarks>
public sealed class RazorBookingTemplateRenderer(
    IRazorViewEngine viewEngine,
    ITempDataProvider tempDataProvider,
    IServiceProvider services,
    ILogger<RazorBookingTemplateRenderer> logger) : IBookingTemplateRenderer
{
    /// <summary>
    /// Where a site puts its content, mirroring Umbraco Forms' own convention for the same
    /// thing. The file is named for the message — see <see cref="BookingMessageKind"/>, whose
    /// members are the names.
    /// </summary>
    public const string TemplateFolder = "~/Views/Partials/UBookIt/Emails/";

    /// <summary>The full path a given message's content would live at.</summary>
    public static string PathFor(BookingMessageKind kind) => $"{TemplateFolder}{kind}.cshtml";

    /// <summary>Whether a site has supplied content for a message, without rendering it.</summary>
    /// <remarks>
    /// Used by the boot check so it can report the outcome the way sending will establish it —
    /// by asking the view engine — rather than by restating what was registered.
    /// </remarks>
    public bool IsSupplied(BookingMessageKind kind)
    {
        try
        {
            return FindView(kind).Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A boot check must not be the thing that stops a site booting.
            logger.LogError(
                exception, "uBookIt could not look for content for {MessageKind}.", kind);

            return false;
        }
    }

    public async Task<BookingTemplateResult> RenderAsync(
        BookingMessageKind kind,
        BookingMessageModel model,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ViewEngineResult found;

        try
        {
            found = FindView(kind);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "uBookIt could not look for content for {MessageKind}.", kind);

            return BookingTemplateResult.Failed;
        }

        if (!found.Success)
        {
            // The ordinary state, and the one every site is in until it supplies something. Not
            // logged: a message per send per unsupplied message would bury the failures that
            // matter.
            return BookingTemplateResult.NotSupplied;
        }

        try
        {
            return await RenderAsync(found.View, model).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The exception is logged HERE, where the template and its own error are both in
            // hand; the composer logs the consequence. Neither writes anything about the booker.
            logger.LogError(
                exception,
                "uBookIt failed to render the content supplied at {TemplatePath}.",
                PathFor(kind));

            return BookingTemplateResult.Failed;
        }
    }

    private ViewEngineResult FindView(BookingMessageKind kind)
        => viewEngine.GetView(executingFilePath: null, PathFor(kind), isMainPage: true);

    private async Task<BookingTemplateResult> RenderAsync(IView view, BookingMessageModel model)
    {
        // SYNTHETIC, not ambient. See the remarks on the type: this is what makes a message
        // composed from a background job render exactly as one composed in a request does.
        var httpContext = new DefaultHttpContext { RequestServices = services };
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
            new TempDataDictionary(httpContext, tempDataProvider),
            writer,
            new HtmlHelperOptions());

        await view.RenderAsync(viewContext).ConfigureAwait(false);

        // Read back off the CONTEXT this method owns, not off the ViewDataDictionary.
        //
        // MVC activates the page with a COPY of the ViewDataDictionary supplied here, so a
        // template's `Subject = "..."` lands in the copy and never reaches this scope — the
        // first version of this read `viewData` and silently produced no subject at all. The
        // context is a single instance shared by everything that holds it, and it is created
        // fresh per message just above, so nothing leaks between two renders either.
        return new BookingTemplateResult(
            BookingTemplateOutcome.Rendered,
            Body: writer.ToString(),
            Subject: httpContext.Items.TryGetValue(
                UBookItEmailPage<BookingMessageModel>.SubjectKey, out var subject)
                    ? subject as string
                    : null,
            IsHtml: httpContext.Items.TryGetValue(
                UBookItEmailPage<BookingMessageModel>.IsHtmlKey, out var isHtml) && isHtml is true);
    }
}
