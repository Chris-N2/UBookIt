using System.Net.Mail;
using UBookIt.Core.Common;

namespace UBookIt.Core.Bookings;

/// <summary>
/// A booker's contact details, carried as one value because they are withheld,
/// supplied and erased together.
/// </summary>
/// <remarks>
/// Clumped rather than held as three members on <see cref="Booker"/> so that "the details
/// are present" is a single observation. Three parallel nullable members could be seen
/// half-populated, and a reader meeting that state has no correct way to interpret it —
/// the same reasoning that governs how these cross the HTTP boundary.
/// </remarks>
public sealed record BookerContact(string Name, string Email, string? Phone);

/// <summary>
/// Who a booking is for: an optional opaque member key (an Umbraco member key
/// as a plain <see cref="Guid"/> — never an Umbraco type) plus contact details,
/// which are required of every booker a placement creates.
/// </summary>
/// <remarks>
/// <para>
/// <b>A booker is in one of exactly two states: carrying contact details, or erased.</b>
/// There is no third, and in particular none in which the details are present but empty —
/// a default value is not evidence of the underlying state, and a reader cannot tell one
/// from the other.
/// </para>
/// <para>
/// <b>Placement only ever produces the first state.</b> <see cref="Create"/> validates
/// exactly as it always has; the erased state is reachable only by erasing an existing
/// booking, or by rehydrating one already erased from storage.
/// </para>
/// <para>
/// <b>The states are distinguished by construction rather than by inspecting values.</b>
/// <see cref="Contact"/> is nullable, so under this project's nullable-reference-types
/// settings a reader cannot reach a name or an email without first establishing that the
/// details are present — the compiler enumerates every such site rather than leaving it to
/// a reviewer. That is why this is a nullable member and not a closed type hierarchy: the
/// hierarchy was wanted for exactly this property, and the language already supplies it.
/// </para>
/// </remarks>
public sealed record Booker
{
    private Booker(Guid? memberKey, BookerContact? contact, DateTimeOffset? erasedUtc)
    {
        MemberKey = memberKey;
        Contact = contact;
        ErasedUtc = erasedUtc;
    }

    /// <summary>
    /// The booker's Umbraco member key, where they had one. <c>null</c> once erased.
    /// </summary>
    /// <remarks>
    /// Cleared by erasure deliberately: a member key identifies a person as reliably as
    /// their email address, so keeping it while removing the name and address would leave
    /// the identity intact and make the erasure a gesture.
    /// </remarks>
    public Guid? MemberKey { get; }

    /// <summary>
    /// The contact details, or <c>null</c> where they have been erased.
    /// </summary>
    /// <remarks>
    /// <b><c>null</c> means erased, and it cannot mean anything else.</b> Every booker a
    /// placement creates carries details — a non-empty name and a well-formed email are
    /// required — so the only way this is absent is that somebody removed them, and
    /// <see cref="ErasedUtc"/> then says when.
    /// </remarks>
    public BookerContact? Contact { get; }

    /// <summary>
    /// When the contact details were erased, or <c>null</c> for a booker still carrying them.
    /// </summary>
    public DateTimeOffset? ErasedUtc { get; }

    /// <summary>True when the contact details have been erased.</summary>
    public bool IsErased => Contact is null;

    /// <summary>
    /// A booker with contact details. Unchanged in what it accepts and what it refuses:
    /// a non-empty name and a well-formed email are required whether or not a member key
    /// is present.
    /// </summary>
    public static DomainResult<Booker> Create(Guid? memberKey, string? name, string? email, string? phone = null)
    {
        var failures = new List<DomainFailure>();

        if (string.IsNullOrWhiteSpace(name))
        {
            failures.Add(new DomainFailure(
                FailureCodes.NameRequired, "A contact name is required.", nameof(BookerContact.Name)));
        }

        if (string.IsNullOrWhiteSpace(email) || !MailAddress.TryCreate(email.Trim(), out _))
        {
            failures.Add(new DomainFailure(
                FailureCodes.EmailInvalid, "A well-formed email address is required.", nameof(BookerContact.Email)));
        }

        return failures.Count > 0
            ? DomainResult<Booker>.Failure(failures)
            : DomainResult<Booker>.Success(new Booker(
                memberKey,
                new BookerContact(
                    name!.Trim(),
                    email!.Trim(),
                    string.IsNullOrWhiteSpace(phone) ? null : phone.Trim()),
                erasedUtc: null));
    }

    /// <summary>
    /// A booker whose contact details have been erased, recording when.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns a <see cref="Booker"/> directly rather than a <see cref="DomainResult{T}"/>:
    /// there is nothing to validate and nothing that can fail. An erasure that could be
    /// refused would be an erasure a caller might skip.
    /// </para>
    /// <para>
    /// No member key is carried through. The overload takes none on purpose — an erased
    /// booker with a member key is not a state this type will represent, so it is not one
    /// a caller can ask for.
    /// </para>
    /// </remarks>
    public static Booker Erased(DateTimeOffset erasedUtc)
        => new(memberKey: null, contact: null, erasedUtc.ToUniversalTime());
}
