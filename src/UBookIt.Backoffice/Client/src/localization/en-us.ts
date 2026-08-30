export default {
  ubookitSection: {
    label: "uBookIt",
  },
  ubookitResources: {
    label: "Resources",
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
    // otherwise conclude it is gone rather than one toggle away — and the port
    // requires that this be learnable from the package rather than by
    // experiment.
    statusFilter: "Status",
    statusHint: "Showing bookings that hold their time. Tick a status to include others.",
    statusRequested: "Requested",
    statusConfirmed: "Confirmed",
    statusCancelled: "Cancelled",
    statusDeclined: "Declined",

    // ------------------------------------------------------------- the table
    when: "When",
    booker: "Booker",
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
  },
};
