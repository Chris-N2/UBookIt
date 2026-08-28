namespace UBookIt.Tests.ThemeFixture;

/// <summary>
/// The fixture themes, named once so a test says which theme it means rather than
/// repeating a string that has to agree with a folder name.
/// <para>
/// The type also gives the test projects a compile-time handle on this assembly
/// (<c>typeof(ThemeFixture).Assembly</c>). A Razor class library of views alone has
/// no type to reference, and a test resolving it by name would pass while the
/// project silently stopped being built.
/// </para>
/// </summary>
public static class ThemeFixture
{
    /// <summary>All ten required views, at the right paths, with the right models.</summary>
    public const string Complete = "complete";

    /// <summary>
    /// Two views only — <c>Booking/Default</c> and <c>BookingFlow/Catalogue</c> — so
    /// the eight it omits fall through to the package's own views.
    /// </summary>
    public const string Partial = "partial";

    /// <summary>
    /// One view at a required path declaring a model that view never receives: the
    /// failure a name-only completeness check passes and a visitor's request throws on.
    /// </summary>
    public const string WrongModel = "wrongmodel";

    /// <summary>
    /// One view declaring <c>IBookingFormView</c> — a base the model it receives
    /// implements. Correct, and it renders, so the completeness check must accept it:
    /// the rule is assignability, not equality.
    /// </summary>
    public const string BaseModel = "basemodel";
}
