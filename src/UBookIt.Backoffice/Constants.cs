namespace UBookIt.Backoffice
{
    public class Constants
    {
        public const string ApiName = "ubookitbackoffice";

        /// <summary>
        /// The backoffice section alias, exactly as a user group stores it once granted.
        /// </summary>
        /// <remarks>
        /// Measured against a running site rather than inferred: a custom section is stored
        /// in <c>umbracoUserGroup2App</c> under its <b>manifest alias</b>, verbatim, while
        /// Umbraco's built-in sections are stored under short lowercase names
        /// (<c>content</c>, <c>media</c>, <c>users</c>). The two shapes differ, so this
        /// value must match the client manifest's <c>alias</c> and not its <c>name</c>.
        /// A test ties it to the manifest so the two cannot drift apart.
        /// </remarks>
        public const string SectionAlias = "UBookIt.Section";

        /// <summary>
        /// The authorization policy every uBookIt management endpoint requires: the user
        /// holds <see cref="SectionAlias"/>.
        /// </summary>
        public const string SectionAccessPolicy = "UBookItSectionAccess";

        /// <summary>
        /// The additional authorization policy an endpoint requires when it acts on a
        /// booker's personal data: the user belongs to Umbraco's built-in <b>Sensitive
        /// data</b> user group.
        /// </summary>
        /// <remarks>
        /// Applied alongside <see cref="SectionAccessPolicy"/>, never instead of it. Section
        /// access decides whether a user may reach uBookIt; this decides whether they may act
        /// on the people inside it.
        /// </remarks>
        public const string SensitiveDataAccessPolicy = "UBookItSensitiveDataAccess";
    }
}
