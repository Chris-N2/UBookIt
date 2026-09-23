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

        /// <summary>
        /// The permission verbs, exactly as a user group stores them once toggled in the
        /// group editor — free strings on <c>IUserGroup.Permissions</c>, persisted verbatim
        /// by Umbraco with no server-side registration. This class is the single vocabulary:
        /// the client manifest, the policies and the seed all read these, and a guard fails
        /// when the manifest and these constants disagree.
        /// </summary>
        public static class Verbs
        {
            /// <summary>The bookings list and every read over bookings.</summary>
            public const string BookingsRead = "UBookIt.Bookings.Read";

            /// <summary>Cancelling, confirming, declining and moving bookings. Implies <see cref="BookingsRead"/> — in the authorization rule, never by copying verbs onto groups.</summary>
            public const string BookingsManage = "UBookIt.Bookings.Manage";

            /// <summary>Resources, services, their supporting reads, and responsibility assignment.</summary>
            public const string Configure = "UBookIt.Configure";

            /// <summary>
            /// Reading and changing the site's own settings.
            /// </summary>
            /// <remarks>
            /// <b>Implies nothing and is implied by nothing — in particular it is NOT a senior form
            /// of <see cref="Configure"/>.</b> Configuring a bookable resource and configuring the
            /// site are different privileges: these settings reach the site's retention posture,
            /// its anonymous delivery-API exposure and the addresses bookers' details are sent to,
            /// and a grant meaning "may add a meeting room" does not carry them. The separation is
            /// the point of the verb, so an implication in either direction would undo it.
            /// </remarks>
            public const string Settings = "UBookIt.Settings";
        }

        /// <summary>
        /// The verb policies. Each carries the section requirement as well as its verb, so
        /// naming one on an action can only ever ADD a condition — the same reasoning the
        /// sensitive-data policy records.
        /// </summary>
        public static class VerbPolicies
        {
            public const string BookingsRead = "UBookItBookingsRead";

            public const string BookingsManage = "UBookItBookingsManage";

            public const string Configure = "UBookItConfigure";

            public const string Settings = "UBookItSettings";

            /// <summary>
            /// Reading the site closure list, satisfied by <see cref="Verbs.Configure"/> OR
            /// <see cref="Verbs.Settings"/>.
            /// </summary>
            /// <remarks>
            /// <b>Not an implication between the two verbs.</b> Each reaches this read on its own
            /// account and acquires nothing else the other holds: an operator editing a resource
            /// must see what it is inheriting in order to exempt it, and whoever decides the
            /// site's closures must be able to see them. <b>Changing</b> the list stays on
            /// <see cref="Settings"/> alone — one entry shuts every resource the site has,
            /// including those created after it.
            /// </remarks>
            public const string ClosuresRead = "UBookItClosuresRead";
        }
    }
}
