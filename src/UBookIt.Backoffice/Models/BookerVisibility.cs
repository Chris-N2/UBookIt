namespace UBookIt.Backoffice.Models;

/// <summary>
/// Whether a caller may be shown a booking's booker contact details.
/// </summary>
/// <remarks>
/// <para>
/// <b>A two-valued type rather than a <c>bool</c>, deliberately.</b>
/// <c>ToModel(summary, BookerVisibility.Withheld)</c> cannot be read backwards at a call site;
/// <c>ToModel(summary, false)</c> can, and the mistake it invites is silent and discloses
/// personal data. There is one call site today, so this is cheap insurance rather than a
/// necessity — but the cost of the insurance is one file.
/// </para>
/// <para>
/// It says what the caller may see, not who they are. The mapping layer has no business
/// knowing about users, groups or Umbraco identity; deciding is the endpoint's job, and this
/// is the answer travelling from one to the other.
/// </para>
/// </remarks>
/// <remarks>
/// <b>Internal, matching its only consumer.</b> <c>BookingModelMapper</c> is internal, so this
/// was public surface with no caller outside the assembly — and the front-end contract this
/// package publishes is API endpoints and view models, not C# mapping types. An alternative UI
/// consumes the JSON, where withholding is already expressed by a null booker.
/// </remarks>
internal enum BookerVisibility
{
    /// <summary>The details are withheld: the row carries no booker at all.</summary>
    /// <remarks>
    /// First deliberately, so that the zero value is the safe one. A default-initialised
    /// <see cref="BookerVisibility"/> withholds rather than discloses.
    /// </remarks>
    Withheld = 0,

    /// <summary>The details are shown.</summary>
    Shown = 1,
}
