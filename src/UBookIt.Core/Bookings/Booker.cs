using System.Net.Mail;
using UBookIt.Core.Common;

namespace UBookIt.Core.Bookings;

/// <summary>
/// Who a booking is for: an optional opaque member key (an Umbraco member key
/// as a plain <see cref="Guid"/> — never an Umbraco type) plus contact details,
/// which are required whether or not a member key is present.
/// </summary>
public sealed record Booker
{
    private Booker(Guid? memberKey, string name, string email, string? phone)
    {
        MemberKey = memberKey;
        Name = name;
        Email = email;
        Phone = phone;
    }

    public Guid? MemberKey { get; }

    public string Name { get; }

    public string Email { get; }

    public string? Phone { get; }

    public static DomainResult<Booker> Create(Guid? memberKey, string? name, string? email, string? phone = null)
    {
        var failures = new List<DomainFailure>();

        if (string.IsNullOrWhiteSpace(name))
        {
            failures.Add(new DomainFailure(
                FailureCodes.NameRequired, "A contact name is required.", nameof(Name)));
        }

        if (string.IsNullOrWhiteSpace(email) || !MailAddress.TryCreate(email.Trim(), out _))
        {
            failures.Add(new DomainFailure(
                FailureCodes.EmailInvalid, "A well-formed email address is required.", nameof(Email)));
        }

        return failures.Count > 0
            ? DomainResult<Booker>.Failure(failures)
            : DomainResult<Booker>.Success(new Booker(
                memberKey,
                name!.Trim(),
                email!.Trim(),
                string.IsNullOrWhiteSpace(phone) ? null : phone.Trim()));
    }
}
