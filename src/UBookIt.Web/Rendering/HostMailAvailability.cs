using Umbraco.Cms.Core.Mail;

namespace UBookIt.Web.Rendering;

/// <summary>
/// Asks the host whether it can send mail, from a rendering path, without letting the answer take
/// the booking form down.
/// </summary>
/// <remarks>
/// <para>
/// <b>A host that refuses to answer is treated as one that cannot send — here and only here.</b>
/// The sending path takes the opposite view deliberately: there, reading a throwing probe as "no"
/// would convert a misconfiguration into permanent silence, so the exception is left to escape and
/// be logged. On a rendering path the calculation is different in both directions.
/// </para>
/// <para>
/// What the answer decides here is a <i>sentence</i>. Getting it wrong in the cautious direction
/// means the notice says the site is able to make contact rather than that a confirmation will be
/// sent — weaker than the truth, never stronger, and so incapable of promising processing that
/// does not happen. Getting it wrong in the other direction, by letting the exception escape,
/// means an unhandled error on the page where a visitor is trying to book.
/// </para>
/// <para>
/// <b>This is not a fault being swallowed.</b> A host that cannot answer is reported at boot, by
/// name, as its own distinct condition — see the notification boot check. The diagnostic exists;
/// this is only declining to repeat it on every render of a public page.
/// </para>
/// </remarks>
internal static class HostMailAvailability
{
    public static bool CanSend(IEmailSender emailSender)
    {
        try
        {
            return emailSender.CanSendRequiredEmail();
        }
        catch (Exception)
        {
            return false;
        }
    }
}
