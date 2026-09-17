export default {
  ubookitSection: {
    label: "uBookIt",
  },
  ubookitResources: {
    label: "Resources",
    responsibilityNotSaved: "The resource was saved. See the Responsibility box for what was not.",
    create: "Create resource",
    edit: "Edit",
    delete: "Delete",
    empty: "No resources yet. Create the first one to start taking bookings.",
    name: "Name",
    type: "Type",
    typeKey: "Type key",
    description: "Description",
    availability: "Availability",
    details: "Details",
    openingHours: "Opening hours",
    exceptions: "Exceptions",
    exception: "Exception",
    constraints: "Constraints",
    capabilities: "Capabilities",
    capabilityAdd: "Add capability",
    capabilityRemove: "Remove %0%",
    capabilityNone: "This resource has no capabilities.",
    capabilityHint: "Lower-case, hyphenated, e.g. cert-x. Choose one already in use, or enter a new one.",

    // ---------------------------------------------------------- direct booking
    //
    // The control must not read as "can this be booked at all". A resource that
    // withholds this is still fully bookable as part of a service — withholding
    // makes it unbookable BY ITSELF, never unbookable — and an editor who reads
    // it the other way will tick it for a therapist and reopen the hole the
    // field exists to close.
    //
    // So the label says "on its own" and the hint states the consequence in both
    // directions, rather than describing the checkbox.
    directlyBookable: "Can be booked on its own",
    directlyBookableHint:
      "Tick this if someone can book this resource by itself, like hiring a room. Leave it unticked for a resource that only makes sense as part of a service — a therapist who also needs a room, say. Either way it can still be used by services.",

    // The list column. Words rather than a tick, so it is readable by a screen
    // reader and not carried by colour; "Service only" says the resource IS
    // bookable, where a bare "No" would suggest otherwise.
    directBooking: "Direct booking",
    directBookingOffered: "On its own",
    directBookingServiceOnly: "Service only",
    save: "Save",
    cancel: "Cancel",
    back: "Back to resources",
    newResource: "New resource",
    addWindow: "Add window",
    removeWindow: "Remove window",
    addException: "Add exception",
    removeException: "Remove exception",
    closedAllDay: "Closed all day",
    from: "From",
    to: "To",
    date: "Date",
    granularity: "Slot granularity (minutes)",
    minDuration: "Minimum duration (minutes)",
    maxDuration: "Maximum duration (minutes)",
    leadTime: "Minimum notice (minutes)",
    horizon: "Booking horizon (days)",
    previousPage: "Previous page",
    nextPage: "Next page",
    showing: "Showing %0%–%1% of %2%",
    actions: "Actions",
    tableLabel: "Bookable resources",
    pagingLabel: "Resource list pages",
    loadingList: "Loading resources",
    loadingResource: "Loading resource",
    errorSummary: "The resource could not be saved:",
    exceptionNeedsDate: "Each exception needs a date before saving.",
    dayMonday: "Monday",
    dayTuesday: "Tuesday",
    dayWednesday: "Wednesday",
    dayThursday: "Thursday",
    dayFriday: "Friday",
    daySaturday: "Saturday",
    daySunday: "Sunday",
    listLoadFailed: "The resource list could not be loaded.",
    resourceLoadFailed: "The resource could not be loaded.",
    resourceSaveFailed: "The resource could not be saved.",
    confirmDeleteHeadline: "Delete resource",
    confirmDeleteContent: "Delete %0%? This cannot be undone.",
    confirmDelete: "Delete",
    confirmFailed: "The confirmation dialog could not be shown, so nothing was deleted.",
  },
  ubookitServices: {
    label: "Services",
    responsibilityNotSaved: "The service was saved. See the Responsibility box for what was not.",
    create: "Create service",
    edit: "Edit",
    delete: "Delete",
    empty: "No services yet. Create one to offer a bookable service.",
    name: "Name",
    requirements: "Service Requirements",
    requirementsSummary: "Requires",
    requirementEntry: "%0% × %1%",
    requirementNone: "—",
    details: "Details",
    duration: "Duration",
    durationVariable: "Variable length",
    durationVariableHint:
      "The person booking chooses how long they need. Leave a limit empty to use whatever the booked resource allows — a resource's own limits always apply, so a booking can never be longer than the resource permits.",
    durationFixed: "Fixed length",
    durationMode: "Duration mode",
    durationMinutes: "Minutes",
    durationMin: "Shortest (minutes, optional)",
    durationMax: "Longest (minutes, optional)",
    durationRequired: "Enter a fixed length in minutes, or choose a variable length.",
    durationVariableSummary: "Variable",
    durationVariableRangeSummary: "Variable, %0%–%1% minutes",
    durationVariableMinSummary: "Variable, from %0% minutes",
    durationVariableMaxSummary: "Variable, up to %0% minutes",
    durationFixedSummary: "%0% minutes",
    resourceType: "Resource type",
    resourceTypeHint: "Choose a type already in use, or enter a new one.",
    requiredCapabilities: "Required capabilities",
    requiredCapabilitiesNone: "Any resource of this type.",
    // States a necessary condition, never a sufficient one. "Only resources
    // with all of these CAN be booked" sat directly above the match count and
    // the pair read as "those N can be booked" — the inference design D8 exists
    // to prevent, since duration also excludes resources and is not checked here.
    requiredCapabilitiesHint: "A resource without all of these cannot fulfil this service.",
    capabilityAdd: "Add capability",
    capabilityRemove: "Remove %0%",

    // ------------------------------------------------------------ requirement rows
    //
    // A service may require several resources at once — a room AND a therapist —
    // one per row. Rows are numbered in their group labels so a screen reader
    // announces which requirement a control belongs to; nothing depends on the
    // order, so the number is a label rather than a position with meaning.
    requirementLegend: "Requirement %0%",
    requirementAdd: "Add requirement",
    requirementRemove: "Remove requirement %0%",

    // A count is "how many at once", not "how many bookings". Worded as
    // resources rather than as a bare number because the distinctness is the
    // whole point: two of a type means two different ones, free simultaneously,
    // and an editor reading "2" alone could as easily take it to mean the same
    // resource twice or a repeat visit.
    requirementCount: "How many at once",
    requirementCountHint: "Two or more means that many different resources, all free at the same time.",

    // Turning this on publishes that requirement's resources as a list of
    // choices on the site, so the label says the consequence rather than leaving
    // it to be discovered there. Off by default: whether to publish who works
    // here is the business's call — a salon wants it, a clinic rotating whoever
    // is free may actively not.
    requirementSelectable: "Let visitors choose who",
    requirementSelectableHint:
      "Visitors booking this service will see the resources that can fill this requirement, by name, and may choose one.",

    // Where the count is greater than one the choice is one OF that many, and the
    // rest are assigned. An editor who believed they were offering a choice of
    // all of them would be configuring something the product does not do.
    requirementSelectableManyHint:
      "Visitors will see the resources that can fill this requirement, by name, and may choose ONE of them. The rest are assigned automatically.",

    // ---------------------------------------------------------- resolution summary
    //
    // ⑧'s capability-only wording is gone. The summary now evaluates every
    // filter candidate resolution applies, so it MAY say what can provide the
    // service (design D5). It must never say available, free, or bookable: the
    // chain says nothing about opening hours, lead time, booking horizon, or
    // existing bookings, and an editor reading "available" would take it as a
    // promise about slots that this cannot make.
    resolutionSummary: "Resource resolution",
    // The healthy case, where all three stages agree. Three lines saying the
    // same number tell an editor less than one line does.
    resolutionHealthy: "%0% resources can provide this service.",
    resolutionHealthyOne: "1 resource can provide this service.",
    // Stage 1 — the resource type. Named explicitly so a mistyped key is
    // visible as a mistyped key, which is the ⑧ defect this replaces.
    resolutionType: "%0% resources have the type “%1%”.",
    resolutionTypeOne: "1 resource has the type “%0%”.",
    resolutionTypeNone: "No resources have the type “%0%”.",
    // Stage 2 — "of those" is load-bearing: it says this count is a subset of
    // the line above rather than an independent number.
    resolutionCapabilities: "%0% of those have the required capabilities.",
    resolutionCapabilitiesOne: "1 of those has the required capabilities.",
    resolutionCapabilitiesNone: "None of those have the required capabilities.",
    // Stage 3.
    resolutionDuration: "%0% of those can provide this service.",
    resolutionDurationOne: "1 of those can provide this service.",
    resolutionDurationNone: "None of those can provide this service.",
    // The bound is named per resource because it is the number the editor
    // changes: "Red Room" says something is wrong, "Red Room (maximum 120
    // minutes)" says which field to open.
    resolutionExcluded: "Excluded by the length: %0%",
    resolutionExcludedMaximum: "%0% (maximum %1% minutes)",
    resolutionExcludedMinimum: "%0% (minimum %1% minutes)",
    // Avoids "bookable" even though it describes a resource's grid rather than
    // a free slot: design D5 bans the availability vocabulary outright rather
    // than case by case, and a rule with judgement calls in it is a rule that
    // drifts. "in %1%-minute steps" says the same thing.
    resolutionExcludedGranularity: "%0% (lengths in %1%-minute steps only)",
    resolutionExcludedMore: "and %0% more",

    // ------------------------------------------------------------ start alignment
    //
    // A separate statement from the resolution chains, about a different thing:
    // the chains say which resources CAN provide the service, this says whether
    // two of them can ever start at the same moment. It is reported only when
    // they never can — its absence is not a claim that the service is bookable,
    // so the same ban applies here as to the chains: never available, free, or
    // bookable. "Start at the same time" says what is true without implying that
    // anything is open, unbooked, or within horizon.
    alignmentReport: "Start times",
    alignmentNever:
      "The “%0%” and “%1%” requirements can never start at the same time, on any day.",
    // The two numbers an editor changes, per resource. Naming the resource is
    // the point: the fix is on a resource, not on this service.
    alignmentWindow: "%0% opens at %1% and starts every %2% minutes.",
    alignmentFix:
      "Change one resource's opening time or step size, or add a resource whose start times line up with the other requirement.",

    // ------------------------------------------------------------- role labels
    //
    // How a requirement is named wherever one is named — the collection view's
    // summary, the resolution chains, and the sufficiency report. Two roles may
    // name one resource type, told apart only by the capabilities each requires,
    // so a label of type alone renders a configuration the domain accepts
    // identically to one it rejects. Stated only where two roles DO share a type:
    // the common case stays short.
    //
    // "requires nothing" is an answer, not a gap — it is precisely what makes
    // such a pair legal — so it is spelled out rather than left as an empty
    // parenthesis.
    roleLabelCapabilities: "%0% (%1%)",
    roleLabelNoCapabilities: "%0% (no required capabilities)",

    // The resolution chains disambiguate the SAME pair differently, by the
    // requirement number the row already carries. Their own requirement forbids
    // the report referring to capabilities for a role that names none — a
    // heading of "therapist (no required capabilities)" describes the
    // configuration in terms the editor never entered — and the number is the
    // better answer anyway, because the fix for what a chain reports is on that
    // row. Matches `requirementLegend` deliberately: the two name one thing.
    roleLabelOrdinal: "Requirement %0%: %1%",

    // -------------------------------------------------------- pool sufficiency
    //
    // A third statement beside the chains and the start-times report, and about a
    // third thing: whether the resources that exist can fill every requirement AT
    // ONCE. Each requirement having candidates is not enough — one resource can
    // be the candidate for two of them, and a count of two needs two.
    //
    // Reported only when they cannot be. Its absence means only that no
    // structural impossibility was found, never that the service can be booked,
    // so the same ban applies as to the chains and the start-times report: never
    // available, free, or bookable. Nothing here evaluates opening hours, lead
    // time, booking horizon, or the booking calendar.
    sufficiencyReport: "Resources needed at once",
    sufficiencyShort:
      "This service needs %0% distinct resources at once, and %1% resources are eligible for the requirements below.",
    sufficiencyOneEligible:
      "This service needs %0% distinct resources at once, and 1 resource is eligible for the requirements below.",
    sufficiencyNoneEligible:
      "This service needs %0% distinct resources at once, and no resource is eligible for the requirements below.",
    sufficiencyRole: "%0% — %1% required.",
    // Both repairs, because the fault is as often a missing resource as a wrong
    // count — and saving is explicitly unaffected, so an editor can define the
    // service first and add the people afterwards.
    sufficiencyFix:
      "Add a qualifying resource, or lower a requirement's count. This does not prevent saving.",
    save: "Save",
    cancel: "Cancel",
    back: "Back to services",
    newService: "New service",
    previousPage: "Previous page",
    nextPage: "Next page",
    showing: "Showing %0%–%1% of %2%",
    actions: "Actions",
    tableLabel: "Bookable services",
    pagingLabel: "Service list pages",
    loadingList: "Loading services",
    loadingService: "Loading service",
    errorSummary: "The service could not be saved:",
    listLoadFailed: "The service list could not be loaded.",
    serviceLoadFailed: "The service could not be loaded.",
    serviceSaveFailed: "The service could not be saved.",
    serviceDeleteFailed: "%0% could not be deleted.",
    confirmDeleteHeadline: "Delete service",
    confirmDeleteContent: "Delete %0%? This cannot be undone.",
    confirmDelete: "Delete",
    confirmFailed: "The confirmation dialog could not be shown, so nothing was deleted.",
  },
  ubookitBookings: {
    label: "Bookings",

    // ------------------------------------------------------------- the window
    //
    // The dates are shown rather than hidden behind a preset, because the site
    // refuses an over-wide window and names the dates it refused. A failure
    // that names something not on screen cannot be acted on.
    from: "From",
    to: "To",

    // ------------------------------------------------------------ the filter
    //
    // The hint is load-bearing. The endpoint returns only what blocks time when
    // no status is asked for, so an operator hunting a cancelled booking would
    // otherwise conclude it is gone rather than one tick away — and the port
    // requires that this be learnable from the package rather than by
    // experiment.
    statusFilter: "Status",

    // "instead", not "as well". Ticking Cancelled shows ONLY cancelled
    // bookings — the endpoint uses supplied statuses exactly as given rather
    // than adding them to the default. The first wording said "tick a status to
    // include others", which reads as adding to what is already shown, and an
    // operator ticking Cancelled to see a cancelled booking alongside today's
    // confirmed ones would instead watch the confirmed ones disappear.
    //
    // Found by operating the screen, not by a test: the behaviour was correct
    // and the sentence describing it was not.
    statusHint: "Showing bookings that hold their time. Tick statuses to show only those instead.",
    statusRequested: "Requested",
    statusConfirmed: "Confirmed",
    statusCancelled: "Cancelled",
    statusDeclined: "Declined",

    // ------------------------------------------------------------- the table
    reference: "Reference",
    when: "When",
    booker: "Booker",

    // ------------------------------------------------- booker details hidden
    //
    // Shown in the Booker cell when the endpoint withheld the details. Words
    // rather than a blank cell, for the same reason "Booked directly" is: an
    // empty cell reads as data that failed to load, and here it would read as a
    // defect in the package.
    //
    // NOT asterisks or a masked form — those imply a value of a particular
    // length and invite guessing at it. NOT "Not permitted", which describes the
    // reader rather than the data.
    bookerHidden: "Contact details hidden",

    // ------------------------------------------------- booker details erased
    // Shown in the Booker cell when the details were ERASED rather than withheld.
    // Deliberately different words from `bookerHidden`, because the two send an
    // operator to different places: "hidden" means a colleague in the Sensitive
    // data group can read them, and "erased" means nobody can, ever. Telling
    // somebody to go and ask when there is nothing to ask for wastes their time
    // and the caller's, and reads as the package being broken when the request
    // comes back empty-handed.
    bookerErased: "Contact details erased",

    // Shown once, above the table, when any row on the page is hidden.
    //
    // The second sentence is the load-bearing one. Umbraco's installer puts only
    // the site's ORIGINAL super user in the Sensitive data group, so a colleague
    // made an administrator tomorrow sees every cell hidden while holding the
    // highest role the site offers. Without this sentence the obvious conclusion
    // is that uBookIt is broken, and the next action is a defect report.
    bookerHiddenNote:
      "Booker contact details are shown only to backoffice users in Umbraco's Sensitive data "
      + "group. Being an administrator does not grant this on its own.",

    resources: "Resources",
    service: "Service",
    status: "Status",

    // Words, not a blank cell. A booking with no service was placed directly
    // against a resource offered on its own — a recorded fact, where an empty
    // cell reads as data that failed to load.
    bookedDirectly: "Booked directly",

    empty: "No bookings in this window.",
    previousPage: "Previous page",
    nextPage: "Next page",
    showing: "Showing %0%–%1% of %2%",
    tableLabel: "Bookings",
    pagingLabel: "Booking list pages",
    loadingList: "Loading bookings",
    listLoadFailed: "The bookings could not be loaded.",

    // ---------------------------------------------------------------- cancel
    //
    // The confirmation names the booking by its REFERENCE, for every operator.
    //
    // It said who the booking was for until contact details became withholdable,
    // at which point that wording had two problems: it renders "the booking for
    // undefined" for an operator who may not see the name, and preserving it
    // would mean branching — two behaviours to reason about and two to test, for
    // no gain. The reference is also the better identifier: it is unique where
    // two bookings may share a booker's name, and it is what the customer on the
    // telephone is reading out.
    //
    // It also states what the package sends, AS A CONDITIONAL. It said flatly that
    // uBookIt tells nobody, and that sentence was true when it was written and
    // falsified by the booking-emails change: on a site with SendBookerEmails on,
    // cancelling from this screen sends the booker a cancellation notice. The docs
    // were corrected at the time; this dialog was missed — found and fixed by the
    // approval-decline change, which adds sibling dialogs to this screen and would
    // otherwise have shipped a truthful one beside a false one. The conditional is
    // statically true in both configurations, which matters because this string
    // cannot see the configuration. It belongs at the moment of deciding, not in
    // documentation nobody is reading just then.
    actions: "Actions",
    cancel: "Cancel booking",
    confirmCancelHeadline: "Cancel booking",
    confirmCancelContent:
      "Cancel booking %0%? The time is released immediately. uBookIt only tells the "
      + "person who booked if booking emails are configured — otherwise, if they "
      + "should know, you will need to contact them.",
    // NOT "Cancel booking". The modal's own dismiss button says "Cancel", so a confirm
    // button reading "Cancel booking" puts the same word on both — one meaning "do the
    // irreversible thing" and one meaning "back out" — in a dialog whose whole purpose is
    // to make that distinction. This change is careful that a broken dialog cannot read as
    // a refusal; identical wording would undo that at the point of decision.
    confirmCancel: "Yes, cancel it",
    confirmFailed: "The confirmation could not be shown, so nothing was cancelled.",
    cancelFailed: "The booking could not be cancelled.",

    // ------------------------------------------------------- confirm / decline
    //
    // Offered only for Requested bookings — the two verbs that resolve a booking
    // placed while AutoConfirm is off. Confirm has no dialog: it is the expected
    // disposition of a request, and a confirmed booking can still be cancelled.
    // Decline gets one on cancel's terms — terminal for the booking and
    // outward-facing for the customer — and its content carries the same truthful
    // notification conditional as cancel's, for the same reason: the string cannot
    // see whether booking emails are configured, so it says the thing that is true
    // either way.
    confirm: "Confirm booking",
    decline: "Decline booking",
    confirmDeclineHeadline: "Decline booking",
    confirmDeclineContent:
      "Decline booking %0%? The time is released immediately. uBookIt only tells the "
      + "person who booked if booking emails are configured — otherwise, if they "
      + "should know, you will need to contact them.",
    confirmDecline: "Yes, decline it",
    declineConfirmFailed: "The confirmation could not be shown, so nothing was declined.",
    confirmBookingFailed: "The booking could not be confirmed.",
    declineBookingFailed: "The booking could not be declined.",

    // ------------------------------------------------------------------ move
    //
    // Offered wherever cancel is: a booking holding time has a time to move. The dialog
    // carries the same truthful notification conditional as cancel and decline, for the
    // same reason. The refusal sentences are written for an operator holding a telephone,
    // one per stable code the domain can answer with — not the endpoint's message, which is
    // written for a developer reading a response. There is deliberately no sentence
    // promising where the booking COULD go: this dialog has no availability read, and a
    // sentence implying one would be a promise the screen cannot keep.
    move: "Move booking",
    moveHeadline: "Move booking",
    moveIntro: "Move booking %0% to a new date, time or length. Its reference stays the same.",
    moveNotificationHint:
      "uBookIt only tells the person who booked about the new time if booking emails are "
      + "configured — otherwise, if they should know, you will need to contact them.",
    moveDate: "Date",
    moveTime: "Start time",
    moveLength: "Length (minutes)",
    moveCancel: "Cancel",
    moveSubmit: "Move",
    moveIncomplete: "Enter a date, a start time and a length in minutes.",
    moveFailed: "The booking could not be moved.",
    moveDialogFailed: "The move dialog could not be shown, so nothing was moved.",
    moveRefusedOutsideOpenHours: "That time is outside the resource's opening hours. Choose another.",
    moveRefusedConflict: "Something else is booked at that time. Choose another.",
    moveRefusedInThePast: "That time has already passed. Choose a time that has not.",
    moveRefusedUnchanged: "The booking already holds that time. Change the date, time or length to move it.",
    moveRefusedStatus: "This booking no longer holds a time to move — it may have been cancelled or declined.",
    moveRefusedGranularity: "That start time is not on the resource's booking grid. Choose a start that is.",
    moveRefusedDuration: "That length is outside what the service or resource allows. Choose another.",
    moveRefusedServiceUnavailable: "A resource on this booking can no longer provide its service at any length, so it cannot be moved.",
    moveRefusedInterval: "That date and time could not be read. Check them and try again.",
    moveRefusedNotFound: "This booking could not be found. It may have been removed; reload the list.",
    movedNotice: "Booking %0% moved to %1%.",

    // Recording a booking on somebody's behalf. The refusal sentences are one per stable code
    // the domain can answer with, and — as for a move — there is deliberately no sentence
    // promising where a booking COULD go: this dialog has no availability read, and implying
    // one would be a promise the screen cannot keep.
    place: "New booking",
    placeHeadline: "Record a booking",
    placeNotificationHint:
      "uBookIt only tells the person who booked about it if booking emails are configured — "
      + "otherwise, read them the reference shown when the booking is made.",
    placeSubject: "What to book",
    placeSubjectUnchosen: "Choose a service or a resource",
    placeServices: "Services",
    placeResources: "Resources",
    placeDate: "Date",
    placeTime: "Start time",
    placeLength: "Length (minutes)",
    placeBookerName: "Booker's name",
    placeBookerEmail: "Booker's email address",
    placeBookerPhone: "Booker's telephone number (optional)",
    placeCancel: "Cancel",
    placeSubmit: "Record booking",
    placeIncomplete:
      "Choose what to book, then enter a date, a start time, a length in minutes, and the "
      + "booker's name and email address.",
    placeFailed: "The booking could not be recorded.",
    placeDialogFailed: "The booking dialog could not be shown, so nothing was recorded.",
    placeSubjectsFailed:
      "The list of what can be booked could not be loaded, so nothing can be recorded yet. "
      + "Close this and try again.",
    placeRefusedOutsideOpenHours: "That time is outside the opening hours. Choose another.",
    placeRefusedConflict: "Something else is booked at that time. Choose another.",
    placeRefusedInThePast: "That time has already passed. Choose a time that has not.",
    placeRefusedGranularity: "That start time is not on the booking grid. Choose a start that is.",
    placeRefusedDuration: "That length is outside what the service or resource allows. Choose another.",
    placeRefusedInterval: "That date and time could not be read. Check them and try again.",
    placeRefusedSubjectNotFound:
      "What you chose could not be found. It may have been removed; close this and try again.",
    placeRefusedServiceUnavailable: "Nothing that can provide this service is free at that time. Choose another.",
    placeRefusedResourceNotEligible: "That resource cannot provide this service.",
    placeRefusedSubject: "Choose one service or one resource to book.",
    placeRefusedEmail: "That email address is not one we can send to. Check it and try again.",
    placeRefusedName: "The booker's name is required.",
    placedNotice: "Booking recorded. Its reference is %0%.",
    placedOutsideWindowNotice:
      "Booking recorded — its reference is %0%. It is on %1%, which is outside the dates shown, "
      + "so it is not in the list below.",
  },
  // Extends Umbraco's own "user" localization section: the group editor derives the
  // heading for each entity type's permission group as user_permissionsEntityGroup_<type>,
  // and without this entry the RAW KEY renders as the heading (observed live).
  user: {
    permissionsEntityGroup_ubookit: "uBookIt",
  },
  ubookitPermissions: {
    bookingsReadLabel: "See bookings",
    bookingsReadDescription:
      "View the Bookings list. Contact details still need the Sensitive data group on top.",
    bookingsManageLabel: "Act on bookings",
    bookingsManageDescription:
      "Cancel, confirm, decline and move bookings. Includes seeing them — acting on what you cannot see makes no sense, so this does not need 'See bookings' ticked as well.",
    configureLabel: "Configure resources and services",
    configureDescription:
      "Create, edit and delete resources and services, and assign who is responsible for them.",
    settingsLabel: "Change site settings",
    settingsDescription:
      "Change how bookings behave and who is told about them. Separate from configuring resources on purpose, and not granted automatically — tick it for the people who should decide these.",
  },
  ubookitSettings: {
    label: "Settings",
    intro:
      "How bookings behave and who is told about them. Some settings are shown here but changed in the site's configuration — those are the ones whose cost of being wrong falls to whoever deploys the site rather than whoever runs the bookings.",
    notPermitted:
      "You do not have permission to change uBookIt settings. An administrator can grant it in Users → User Groups → Default permissions, by ticking “Change site settings”. It is not granted automatically, including on upgrade.",
    loadFailed: "The settings could not be loaded.",
    saveFailed: "That change could not be saved.",
    resetFailed: "That setting could not be reset.",
    reset: "Reset to configured value",
    notSet: "Not set",
    requiresRestart: "Changing this in configuration takes effect when the site restarts.",
    overriddenConfiguredValue: "Overriding the configured value: %0%",
    overriddenNothingConfigured: "Overriding — the site's configuration sets no value for this.",

    // The tier-2 consequence statement. STATIC and unconditional: true of every site, and it
    // reads no booking, resource or availability data. Availability rules are stored as
    // day-and-time with no zone, while bookings are stored as absolute instants — so the
    // zone changes what rules MEAN while bookings keep the times they were made for.
    timeZoneConsequence:
      "Availability rules are wall-clock in the site's time zone. Changing this makes a 9:00–17:00 rule mean 9:00–17:00 in the new zone. Existing bookings keep the actual times they were made for, so some may no longer fall inside their resource's hours. Nothing is rewritten, and setting it back restores what every rule meant.",

    autoConfirmLabel: "Confirm bookings automatically",
    autoConfirmDescription:
      "On, bookings are confirmed as they are placed. Off, they arrive as requests for somebody to confirm or decline.",
    notificationsSendBookerEmailsLabel: "Email the person who booked",
    notificationsSendBookerEmailsDescription:
      "Whether the booker is emailed when their booking is placed, confirmed, declined or cancelled.",
    notificationsInternalRecipientsLabel: "Internal recipients",
    notificationsInternalRecipientsDescription:
      "Your own addresses to tell about bookings, separated by commas. Supplying addresses is what turns internal messages on.",
    privacyPolicyUrlLabel: "Privacy policy link",
    privacyPolicyUrlDescription:
      "Linked from the booking form's privacy notice. With none, the notice renders without a link rather than with a broken one.",
    timeZoneIdLabel: "Time zone",
    timeZoneIdDescription:
      "The IANA time zone the site's availability rules are written in, such as Europe/London.",
    retentionDaysLabel: "Erase booker details after (days)",
    retentionDaysDescription:
      "Changed in configuration only. Erasure is irreversible and happens on a timer rather than when you save, so it is deliberately not changed from here.",
    maxQueryRangeDaysLabel: "Maximum availability query range (days)",
    maxQueryRangeDaysDescription:
      "Changed in configuration only. A cost guardrail: too high does not look broken, it just makes the site slower.",
    deliveryApiEnableReadsLabel: "Delivery API: reads",
    deliveryApiEnableReadsDescription:
      "Whether the anonymous read endpoints are served. Changed in configuration only — exposure is decided as the application starts, so a disabled direction is absent rather than refused.",
    deliveryApiEnablePlacementLabel: "Delivery API: booking placement",
    deliveryApiEnablePlacementDescription:
      "Whether anonymous booking placement is served. Changed in configuration only, for the same reason as reads.",
  },
  ubookitResponsibility: {
    headline: "Responsibility",
    hint: "Who is emailed about this item's bookings — being responsible does not grant or restrict access to anything.",
    users: "Responsible users",
    groups: "Responsible groups",
    marksHeading: "Assignments needing attention",
    markMissing: "no longer exists and will not be emailed",
    markDisabled: "disabled and will not be emailed",
    markInvited: "invitation not accepted, so they will not be emailed",
    unnamedParty: "A user or group that no longer exists",
    loadFailed: "The responsible users and groups could not be loaded.",
    saveFailed: "The item was saved, but its responsible users and groups were not. They are unchanged; try saving again.",
    saveRefusedAfterLoadFailure: "The item was saved, but its responsible users and groups were left unchanged: they could not be loaded, so saving would have replaced them with an empty list. Close and reopen this item to try again.",
  },
};
