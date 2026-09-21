namespace UBookIt.Tests.Support;

/// <summary>
/// A claim a shipped document once made, which a later capability made false.
/// </summary>
/// <param name="Needle">
/// The literal held out of every shipped document by the falsified-claims sweep.
/// </param>
/// <param name="AsWritten">
/// The text <b>as the document actually carried it</b>, original line wrapping and markdown
/// included. This is evidence, not decoration: it is what makes <paramref name="Needle"/>
/// falsifiable.
/// </param>
/// <param name="Evidence">Where <paramref name="AsWritten"/> was recovered from.</param>
/// <param name="RetiredBy">The capability whose landing made the claim false.</param>
public sealed record RetiredClaim(
    string Needle,
    string AsWritten,
    string Evidence,
    string RetiredBy);

/// <summary>
/// The sentences shipped documents may no longer make, each paired with the text it was
/// written against.
/// </summary>
/// <remarks>
/// <para>
/// <b>A <c>DoesNotSay</c> needle is unfalsifiable on its own, and that is not a theoretical
/// complaint.</b> It asserts absence, so a needle that could never match anything and a needle
/// doing its job produce the same green run, for years, with nothing to distinguish them. The
/// sweep this feeds has held out
/// <c>"there is no search by name, email or reference"</c> since <c>find-by-booker</c>. The
/// README stopped carrying that wording at <c>ae55773</c> — the sentence was narrowed to
/// <c>"There is no search by name or reference"</c> when the email search shipped, which was a
/// correct edit — and the needle was written afterwards, against the pre-edit wording. It has
/// matched nothing from the day it was written, and the README's real sentence went on to be
/// falsified by <c>find-booking</c> with every guard green.
/// </para>
/// <para>
/// <b>So each needle carries the text it was written against, and a test asserts it matches.</b>
/// The fixture is the evidence, the needle is the instrument, and the test is the crossing
/// between them. A needle written from memory now fails in the run that introduces it rather
/// than in the release that discovers it.
/// </para>
/// <para>
/// <b><c>AsWritten</c> must be recovered from git, never retyped</b> — and that is enforced by
/// <c>RetiredClaimEvidenceTests</c>, which re-runs <c>git show &lt;commit&gt;:&lt;path&gt;</c>
/// for every entry and compares. <b>This paragraph used to assert the recovery as a fact, and
/// the assertion was false:</b> one of the sixteen entries had been retyped, close enough to
/// read as right and wrong in four words, and QA found it by hand because nothing here could.
/// A sentence claiming a property no check holds is the defect this whole change is about, so
/// the claim now names its enforcement instead of asking to be believed.
/// </para>
/// <para>
/// <b>One entry is not from a shipped document, and says so.</b>
/// <c>"does not tell the person who booked"</c> never appeared in <c>README.md</c> or under
/// <c>docs/</c> in any commit; it was written as a needle in <c>cancel-and-notify</c>'s own task
/// list, to keep a claim from arriving rather than to retire one that had. It is kept — a claim
/// worth refusing is worth refusing whether or not it ever landed — and its evidence names what
/// it actually is, because an entry that implied a document once carried it would be the same
/// species of invention as the needle above.
/// </para>
/// </remarks>
public static class RetiredClaims
{
    /// <summary>
    /// Claims retired before <c>17.1.0</c>, recovered from history when the fixture was built.
    /// </summary>
    private static readonly RetiredClaim[] BeforeThisRelease =
    [
        new(
            "No v1 pathway produces those statuses",
            """
            - Approving or declining a booking. No v1 pathway produces those statuses.
            """,
            "docs/notifications.md @ d5ee3c96",
            "approval-decline"),
        new(
            "no pathway produces them",
            """
            - **It does not approve or decline.** Placement confirms immediately. The statuses for an
              approval workflow exist in the data model so it can be added without a breaking change,
              but no pathway produces them today.
            """,
            "docs/backoffice.md @ 59eed26d",
            "approval-decline"),
        new(
            "notifies nobody by itself",
            """
            placement and cancellation carrying enough to act on; and the backoffice documentation says
            plainly that the package notifies nobody by itself.
            """,
            "docs/mvp.md @ 59eed26d",
            "booking-emails"),
        new(
            "does not tell the person who booked",
            """
            Now pinned by meaning ("does not tell the person who booked", "contact them") plus a
            presence check over every key the cancel flow can emit.
            """,
            "openspec/changes/cancel-and-notify/tasks.md @ e545deeb — a needle from birth, never a "
            + "sentence any shipped document carried",
            "booking-emails"),
        new(
            "It does not approve or decline",
            """
            - **It does not approve or decline.** Placement confirms immediately. The statuses for an
              approval workflow exist in the data model so it can be added without a breaking change,
              but no pathway produces them today.
            """,
            "docs/backoffice.md @ cd6c3fb9",
            "approval-decline"),
        new(
            "Nothing is sent by the package",
            """
            - **Emails.** uBookIt raises a notification; your site owns the channel and the wording.
              Nothing is sent by the package, including to somebody whose booking you cancel.
            """,
            "README.md @ 567480aa",
            "booking-emails"),
        new(
            "placement auto-confirms",
            """
            - **Approving or declining** a booking — placement auto-confirms.
            """,
            "README.md @ 567480aa",
            "approval-decline"),
        new(
            "there is no search by name, email or reference",
            """
            - **Finding a booking without knowing roughly when it is.** The backoffice list is windowed
              by date; there is no search by name, email or reference.
            """,
            "README.md @ ca18ccd — narrowed at ae55773, before this needle was ever written",
            "find-by-booker"),
        new(
            "the shape of that operation is a cancellation and a new booking",
            """
            - **It does not amend a booking's time.** There is no reschedule; the shape of that
              operation is a cancellation and a new booking.
            """,
            "docs/backoffice.md @ cd6c3fb9",
            "move-booking"),
        new(
            "There is no reschedule",
            """
            - **It does not amend a booking's time.** There is no reschedule; the shape of that
              operation is a cancellation and a new booking.
            """,
            "docs/backoffice.md @ cd6c3fb9",
            "move-booking"),
        new(
            "Amending a booking's time. There is no such operation",
            """
            - Amending a booking's time. There is no such operation; the shape of it is a cancellation
              and a new booking.
            """,
            "docs/notifications.md @ d5ee3c96",
            "move-booking"),
    ];

    /// <summary>
    /// Claims falsified by <c>17.1.0</c>'s capabilities and retired by
    /// <c>docs-truth-and-screenshots</c> — the ones each of those changes should have retired
    /// itself.
    /// </summary>
    /// <remarks>
    /// Recovered from <c>HEAD</c> before deletion rather than after, which is the only moment the
    /// wording is available without going through history.
    /// </remarks>
    private static readonly RetiredClaim[] FalsifiedBy171 =
    [
        new(
            "Bookings arrive through the front-end flow",
            """
            - **Taking a booking on someone's behalf.** Bookings arrive through the front-end flow. (A
              booking can be **moved** to a new time from the backoffice, keeping its reference — but not
              placed from it.)
            """,
            "README.md @ a875088 (HEAD when retired)",
            "booking-on-behalf"),
        new(
            "It does not take a booking on someone's behalf",
            """
            From the Bookings view **you can see bookings, cancel them, and — where a booking awaits
            approval — confirm or decline it**, and you can **move a booking to a new date, time or
            length**. It does not take a booking on someone's behalf.
            """,
            "docs/backoffice.md @ a875088 (HEAD when retired)",
            "booking-on-behalf"),
        new(
            "It does not place bookings",
            """
            - **It does not place bookings.** Recording a booking on someone's behalf — a phone
              booking — is not built. Bookings arrive through the front-end flow.
            """,
            "docs/backoffice.md @ a875088 (HEAD when retired)",
            "booking-on-behalf"),
        new(
            "It does not find a person across bookings",
            """
            - **It does not find a person across bookings.** Erasure works on one booking at a time; see
              above.
            """,
            "docs/backoffice.md @ a875088 (HEAD when retired)",
            "find-booking"),
        new(
            "There is no search by name or reference",
            """
            - **Finding a booking without knowing roughly when it is.** The backoffice list is windowed
              by date — but you can find every booking holding a given email address, which is how an
              erasure request is honoured. There is no search by name or reference.
            """,
            "README.md @ a875088 (HEAD when retired) — the sentence the longer needle above was "
            + "written to cover and never once matched",
            "find-booking"),
    ];

    /// <summary>
    /// Every retired claim. The sweep holds all of these out of every shipped document.
    /// </summary>
    public static IReadOnlyList<RetiredClaim> All { get; } =
        [.. BeforeThisRelease, .. FalsifiedBy171];
}
