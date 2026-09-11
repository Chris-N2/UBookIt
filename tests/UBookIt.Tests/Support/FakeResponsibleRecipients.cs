using UBookIt.Core.Bookings;
using UBookIt.Persistence.Responsibility;

namespace UBookIt.Tests.Support;

/// <summary>
/// A resolver whose answers are handed to it. The two questions are configured
/// SEPARATELY on purpose: the gate asks "has the site asked" and the send asks "who",
/// and the seam between them — assignments that exist but resolve to nobody — is a
/// state the real resolver produces (a dangling or disabled party) and tests must be
/// able to produce too.
/// </summary>
internal sealed class FakeResponsibleRecipients(
    bool hasAssignments,
    IReadOnlyList<string> addresses) : IResponsibleRecipientResolver
{
    /// <summary>Counts, so a test can assert user resolution never ran (e.g. before the host check).</summary>
    public int ResolveCalls { get; private set; }

    public Task<bool> HasAssignmentsAsync(Booking booking, CancellationToken cancellationToken = default)
        => Task.FromResult(hasAssignments);

    public Task<IReadOnlyList<string>> ResolveAddressesAsync(
        Booking booking, CancellationToken cancellationToken = default)
    {
        ResolveCalls++;

        return Task.FromResult(addresses);
    }

    public Task<IReadOnlyList<ResponsibilityPartyStatus>> DescribeAsync(
        IReadOnlyCollection<ResponsibilityAssignment> assignments,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ResponsibilityPartyStatus>>([]);
}

/// <summary>No assignments anywhere — the state of every site before this capability.</summary>
internal static class NoResponsibility
{
    public static readonly FakeResponsibleRecipients Instance = new(hasAssignments: false, addresses: []);
}
