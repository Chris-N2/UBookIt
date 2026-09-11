using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Persistence.Entities;
using UBookIt.Persistence.Stores;

namespace UBookIt.Persistence.Responsibility;

/// <summary>
/// SQL Server implementation of <see cref="IResponsibilityStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Replace takes the subject's <b>configuration</b> application lock — the same lock the
/// subject's own update and delete take — so a replace racing the owner's delete
/// serializes against it: whichever wins, the loser sees the settled state, and a deleted
/// subject can never be left holding fresh assignment rows. That matters because the
/// table deliberately carries no foreign keys (the party side cannot have one — users
/// live in Umbraco's tables — and giving only the subject side one would make the halves
/// behave differently), so the lock is what does the job a subject-side FK would have.
/// </para>
/// <para>
/// The booking-facing reads are lock-free and read-committed: sending happens after the
/// booking is stored, and a recipient list read concurrently with an assignment edit is
/// allowed to see either side of it — a message is not a booking.
/// </para>
/// </remarks>
internal sealed class SqlResponsibilityStore(UBookItDbContext db) : IResponsibilityStore
{
    public async Task<IReadOnlyList<ResponsibilityAssignment>> GetAsync(
        ResponsibilitySubject subject, Guid subjectId, CancellationToken cancellationToken = default)
    {
        var subjectType = StoredSubjectType(subject);

        var rows = await db.Responsibilities
            .AsNoTracking()
            .Where(r => r.SubjectType == subjectType && r.SubjectId == subjectId)
            .OrderBy(r => r.PartyType).ThenBy(r => r.PartyKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(ToAssignment).ToList();
    }

    public async Task<DomainResult> ReplaceAsync(
        ResponsibilitySubject subject,
        Guid subjectId,
        IReadOnlyCollection<ResponsibilityAssignment> assignments,
        CancellationToken cancellationToken = default)
    {
        var subjectType = StoredSubjectType(subject);

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // The subject's own configuration lock, not a new one — see the class remarks.
        // It also serializes two concurrent replaces, which would otherwise both delete
        // zero rows at READ COMMITTED and commit a merged union (the QA finding the
        // resource stores already carry).
        await AppLock.AcquireAsync(db, ConfigLock(subject, subjectId), cancellationToken)
            .ConfigureAwait(false);

        var exists = subject == ResponsibilitySubject.Resource
            ? await db.Resources.AnyAsync(r => r.Id == subjectId, cancellationToken).ConfigureAwait(false)
            : await db.Services.AnyAsync(s => s.Id == subjectId, cancellationToken).ConfigureAwait(false);

        if (!exists)
        {
            return subject == ResponsibilitySubject.Resource
                ? DomainResult.Failure(
                    FailureCodes.ResourceNotFound, $"No resource exists with id {subjectId}.")
                : DomainResult.Failure(
                    FailureCodes.ServiceNotFound, $"No service exists with id {subjectId}.");
        }

        await db.Responsibilities
            .Where(r => r.SubjectType == subjectType && r.SubjectId == subjectId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        // Distinct BEFORE insert: the compound primary key makes a duplicate impossible in
        // storage, and this makes it a non-event at the API as well — a set containing the
        // same assignment twice means it once.
        db.Responsibilities.AddRange(assignments
            .Distinct()
            .Select(a => new ResponsibilityRow
            {
                SubjectType = subjectType,
                SubjectId = subjectId,
                PartyType = StoredPartyType(a.Kind),
                PartyKey = a.Key,
            }));

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return DomainResult.Success();
    }

    public async Task<bool> HasAnyForBookingAsync(
        Booking booking, CancellationToken cancellationToken = default)
    {
        var (resourceIds, serviceId) = Subjects(booking);

        return await db.Responsibilities
            .AsNoTracking()
            .Where(BookingSubjectPredicate(resourceIds, serviceId))
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ResponsibilityAssignment>> GetForBookingAsync(
        Booking booking, CancellationToken cancellationToken = default)
    {
        var (resourceIds, serviceId) = Subjects(booking);

        var rows = await db.Responsibilities
            .AsNoTracking()
            .Where(BookingSubjectPredicate(resourceIds, serviceId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Distinct: the same party assigned to the service and to a claimed resource is
        // one party. The union across subjects is the requirement's whole point; the
        // flattening here is what makes "one person, many routes, one message" cheap for
        // everything downstream.
        return rows.Select(ToAssignment).Distinct().ToList();
    }

    private static (List<Guid> ResourceIds, Guid? ServiceId) Subjects(Booking booking) => (
        booking.Claims.Select(c => c.ResourceId).ToList(),
        booking.Service?.ServiceId);

    /// <summary>
    /// One query for both subject kinds. The discriminator keeps the id spaces apart, so a
    /// service sharing a Guid with a resource (or a recycled id) can never cross-match.
    /// </summary>
    private static System.Linq.Expressions.Expression<Func<ResponsibilityRow, bool>> BookingSubjectPredicate(
        List<Guid> resourceIds, Guid? serviceId)
        => serviceId is { } sid
            ? r => (r.SubjectType == ResponsibilitySubjectTypes.Resource && resourceIds.Contains(r.SubjectId))
                || (r.SubjectType == ResponsibilitySubjectTypes.Service && r.SubjectId == sid)
            : r => r.SubjectType == ResponsibilitySubjectTypes.Resource && resourceIds.Contains(r.SubjectId);

    private static string ConfigLock(ResponsibilitySubject subject, Guid subjectId)
        => subject == ResponsibilitySubject.Resource
            ? AppLock.ForResourceConfig(subjectId)
            : AppLock.ForServiceConfig(subjectId);

    private static string StoredSubjectType(ResponsibilitySubject subject)
        => subject == ResponsibilitySubject.Resource
            ? ResponsibilitySubjectTypes.Resource
            : ResponsibilitySubjectTypes.Service;

    private static string StoredPartyType(ResponsibilityPartyKind kind)
        => kind == ResponsibilityPartyKind.User
            ? ResponsibilityPartyTypes.User
            : ResponsibilityPartyTypes.Group;

    private static ResponsibilityAssignment ToAssignment(ResponsibilityRow row)
        => new(
            row.PartyType == ResponsibilityPartyTypes.User
                ? ResponsibilityPartyKind.User
                : ResponsibilityPartyKind.Group,
            row.PartyKey);
}
