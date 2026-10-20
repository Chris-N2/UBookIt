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
using UBookIt.Core.Services;

namespace UBookIt.Web.Emails;

/// <summary>
/// Renders a site's own message content from a Razor view.
/// </summary>
/// <remarks>
/// <para>
/// <b>It builds its own <see cref="DefaultHttpContext"/> from the root service provider rather
/// than using an ambient one</b>, and that is the whole reason this type can be used at all.
/// Nothing sends from outside a request today — the retention sweep
/// erases bookers and sends nothing — but a reminder would, so a renderer reaching for
/// <c>IHttpContextAccessor</c> would work everywhere it is used now and fail in the first job
/// that sends one, which is the least observable place to fail. Umbraco's
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
/// <para>
/// <b>A service's template set is looked in first.</b> Where the model names a set, the message's
/// view is looked for at <see cref="SetPathFor"/>, then at <see cref="PathFor"/>. Only "nothing
/// supplied" moves down that order: a set view that fails to render is
/// <see cref="BookingTemplateOutcome.Failed"/>, and the view for every service is NOT tried,
/// because it may have been written for a different kind of service and the package's own wording
/// is the safer thing to send.
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

    /// <summary>
    /// The full path a given message's content would live at in a service's template set: the set's
    /// name as a folder beneath <see cref="TemplateFolder"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="templateSet"/> is not a valid set name (<see cref="Service.IsValidTemplateSetName"/>).
    /// The name becomes a path segment, so no other value is ever turned into a path. Sending does
    /// not reach this with an invalid name — <see cref="RenderAsync"/> checks first — so a caller
    /// that does has made a programming error.
    /// </exception>
    public static string SetPathFor(string templateSet, BookingMessageKind kind)
        => Service.IsValidTemplateSetName(templateSet)
            ? $"{TemplateFolder}{templateSet}/{kind}.cshtml"
            : throw new ArgumentException(
                "Not a valid email template set name: only a-z, 0-9 and single inner hyphens, "
                + $"at most {Service.TemplateSetMaxLength} characters.",
                nameof(templateSet));

    /// <summary>Whether a site has supplied content for a message, without rendering it.</summary>
    /// <remarks>
    /// Used by the boot check so it can report the outcome the way sending will establish it —
    /// by asking the view engine — rather than by restating what was registered.
    /// </remarks>
    public bool IsSupplied(BookingMessageKind kind) => IsFound(PathFor(kind), kind);

    /// <summary>
    /// Whether a site has supplied content for a message in a service's template set, without
    /// rendering it — asked of the view engine exactly as sending will, so a set folder inside an
    /// assembly is seen as well as one on disk.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="templateSet"/> is not a valid set name; see <see cref="SetPathFor"/>.
    /// </exception>
    public bool IsSuppliedInSet(string templateSet, BookingMessageKind kind)
        => IsFound(SetPathFor(templateSet, kind), kind);

    private bool IsFound(string path, BookingMessageKind kind)
    {
        try
        {
            return FindView(path).Success;
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

        if (model.TemplateSet is { } templateSet)
        {
            // CHECKED AGAIN, not trusted. The composer only ever sets a name the service rule
            // accepted, but the model is public and anything can construct it — and this is the
            // line where a name becomes a path. Anything else is a failure, never a lookup. The
            // set is site configuration rather than anything about the booker, so it is named.
            if (!Service.IsValidTemplateSetName(templateSet))
            {
                logger.LogError(
                    "uBookIt was given '{TemplateSet}' as the email template set for {MessageKind}, which is "
                    + "not a valid set name. No content was looked for under it.",
                    templateSet,
                    kind);

                return BookingTemplateResult.Failed;
            }

            var setPath = SetPathFor(templateSet, kind);

            if (!TryFindView(setPath, kind, out var inSet))
            {
                return BookingTemplateResult.Failed;
            }

            if (inSet.Success)
            {
                // THE CHAIN STOPS HERE, whatever the outcome. A set view that fails to render is
                // Failed, and the view for every service is not tried: it may be written for a
                // different kind of service, and sending one service's wording to the booker of
                // another is worse than the package's neutral wording.
                return await RenderFoundAsync(inSet.View, model, setPath).ConfigureAwait(false);
            }

            // Nothing in the set for this message — the one outcome that moves down the order.
        }

        var path = PathFor(kind);

        if (!TryFindView(path, kind, out var found))
        {
            return BookingTemplateResult.Failed;
        }

        if (!found.Success)
        {
            // The ordinary state, and the one every site is in until it supplies something. Not
            // logged: a message per send per unsupplied message would bury the failures that
            // matter.
            return BookingTemplateResult.NotSupplied;
        }

        return await RenderFoundAsync(found.View, model, path).ConfigureAwait(false);
    }

    private bool TryFindView(string path, BookingMessageKind kind, out ViewEngineResult found)
    {
        try
        {
            found = FindView(path);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "uBookIt could not look for content for {MessageKind}.", kind);

            found = null!;
            return false;
        }
    }

    private async Task<BookingTemplateResult> RenderFoundAsync(IView view, BookingMessageModel model, string path)
    {
        try
        {
            return await RenderAsync(view, model).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The exception is logged HERE, where the template and its own error are both in
            // hand; the composer logs the consequence. Neither writes anything about the booker.
            logger.LogError(
                exception,
                "uBookIt failed to render the content supplied at {TemplatePath}.",
                path);

            return BookingTemplateResult.Failed;
        }
    }

    private ViewEngineResult FindView(string path)
        => viewEngine.GetView(executingFilePath: null, path, isMainPage: true);

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
