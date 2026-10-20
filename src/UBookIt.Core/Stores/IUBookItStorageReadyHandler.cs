namespace UBookIt.Core.Stores;

/// <summary>
/// Work that must wait until uBookIt's own storage is up to date when a site starts — called once
/// per start, after uBookIt's database migrations have been applied.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists rather than a handler for Umbraco's application-started notification.</b>
/// uBookIt applies its migrations from that same notification, and handlers for one notification
/// run in an order nothing here controls. A startup check that reads uBookIt's tables from it can
/// run first, and on the start after an upgrade that read meets a schema the upgrade has not yet
/// changed (service-email-templates, observed live). Whoever applies the migrations calls these
/// afterwards, so the order is owned by the one place that knows it.
/// </para>
/// <para>
/// <b>Deliberately free of Umbraco types</b>, like the rest of this assembly: it is an
/// interface, not a notification, so it can be declared here and implemented by any uBookIt
/// assembly without one referencing another.
/// </para>
/// <para>
/// <b>Internal, not an extension point</b> (QA round 1, Chris's decision): uBookIt's own assemblies
/// use it, through <c>InternalsVisibleTo</c>, and nothing promises a site anything about it.
/// Making it public later would be additive; making a public one internal would be a break.
/// </para>
/// <para>
/// <b>A handler cannot stop a site starting.</b> A failure other than cancellation is logged and
/// the next handler runs. Handlers are not called when the migrations themselves fail, because
/// then the storage is not ready. Each is registered with the service collection and resolved in
/// the same scope as the migrations.
/// </para>
/// </remarks>
internal interface IUBookItStorageReadyHandler
{
    /// <summary>Does the work, now that uBookIt's storage is up to date.</summary>
    Task OnStorageReadyAsync(CancellationToken cancellationToken);
}
