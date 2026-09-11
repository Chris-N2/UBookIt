namespace UBookIt.Backoffice.Models;

/// <summary>
/// Responsibility contract models. Purpose-built DTOs like the rest of the management
/// contract — the persistence port's types never appear on the wire.
/// </summary>
public class ResponsibilityAssignmentModel
{
    /// <summary><c>user</c> or <c>group</c>. Anything else is a validation failure.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The Umbraco user key or user group key.</summary>
    public Guid Key { get; set; }
}

/// <summary>
/// The write model: the subject's complete assignment set. A save replaces wholesale —
/// what is saved is what was seen — so there is no add/remove shape to drift from it.
/// </summary>
public class ResponsibilityRequestModel
{
    public List<ResponsibilityAssignmentModel> Assignments { get; set; } = [];
}

/// <summary>
/// One stored assignment, annotated with what its party currently resolves to, so the
/// editor can mark a stale or disabled party rather than hide it.
/// </summary>
public class ResponsibilityPartyModel
{
    /// <summary><c>user</c> or <c>group</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    public Guid Key { get; set; }

    /// <summary>Whether the referenced user or group still exists.</summary>
    public bool Exists { get; set; }

    /// <summary>The party's current display name, where it exists.</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// For an existing user party, the name of its Umbraco state (<c>Active</c>,
    /// <c>Inactive</c>, <c>LockedOut</c>, <c>Disabled</c>, <c>Invited</c>), so the editor
    /// can mark the states sending skips. Null for groups and for missing parties.
    /// </summary>
    public string? UserState { get; set; }
}

public class ResponsibilityResponseModel
{
    public List<ResponsibilityPartyModel> Assignments { get; set; } = [];
}
