import {
  BOOKINGS_MANAGE_VERB,
  BOOKINGS_READ_VERB,
  CONFIGURE_VERB,
  SETTINGS_VERB,
} from "./permission-verbs.js";
import { UBOOKIT_VERB_CONDITION_ALIAS } from "./verb.condition.js";

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "condition",
    alias: UBOOKIT_VERB_CONDITION_ALIAS,
    name: "uBookIt Verb Condition",
    js: () => import("./verb.condition.js"),
  },

  // The four permission verbs, surfaced as toggles in the user group editor's
  // Default permissions pane by Umbraco's own extension surface — no custom UI.
  // The verbs are the server's constants verbatim; a server-side guard fails when
  // the two vocabularies disagree.
  {
    type: "entityUserPermission",
    alias: "UBookIt.Permission.Bookings.Read",
    name: "uBookIt Bookings Read Permission",
    forEntityTypes: ["ubookit"],
    weight: 300,
    meta: {
      verbs: [BOOKINGS_READ_VERB],
      label: "#ubookitPermissions_bookingsReadLabel",
      description: "#ubookitPermissions_bookingsReadDescription",
    },
  },
  {
    type: "entityUserPermission",
    alias: "UBookIt.Permission.Bookings.Manage",
    name: "uBookIt Bookings Manage Permission",
    forEntityTypes: ["ubookit"],
    weight: 290,
    meta: {
      verbs: [BOOKINGS_MANAGE_VERB],
      label: "#ubookitPermissions_bookingsManageLabel",
      description: "#ubookitPermissions_bookingsManageDescription",
    },
  },
  {
    type: "entityUserPermission",
    alias: "UBookIt.Permission.Configure",
    name: "uBookIt Configure Permission",
    forEntityTypes: ["ubookit"],
    weight: 280,
    meta: {
      verbs: [CONFIGURE_VERB],
      label: "#ubookitPermissions_configureLabel",
      description: "#ubookitPermissions_configureDescription",
    },
  },
  {
    type: "entityUserPermission",
    alias: "UBookIt.Permission.Settings",
    name: "uBookIt Settings Permission",
    forEntityTypes: ["ubookit"],
    weight: 270,
    meta: {
      verbs: [SETTINGS_VERB],
      label: "#ubookitPermissions_settingsLabel",
      description: "#ubookitPermissions_settingsDescription",
    },
  },
  {
    type: "section",
    alias: "UBookIt.Section",
    name: "uBookIt Section",
    weight: 250,
    meta: {
      label: "#ubookitSection_label",
      pathname: "ubookit",
    },
  },
  {
    type: "sectionView",
    alias: "UBookIt.SectionView.Resources",
    name: "uBookIt Resources Section View",
    js: () => import("./resources-view.element.js"),
    weight: 100,
    meta: {
      label: "#ubookitResources_label",
      pathname: "resources",
      icon: "icon-calendar",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "UBookIt.Section",
      },
      {
        alias: UBOOKIT_VERB_CONDITION_ALIAS,
        oneOf: [CONFIGURE_VERB],
      },
    ],
  },
  {
    type: "sectionView",
    alias: "UBookIt.SectionView.Services",
    name: "uBookIt Services Section View",
    js: () => import("./services-view.element.js"),
    weight: 90,
    meta: {
      label: "#ubookitServices_label",
      pathname: "services",
      icon: "icon-list",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "UBookIt.Section",
      },
      {
        alias: UBOOKIT_VERB_CONDITION_ALIAS,
        oneOf: [CONFIGURE_VERB],
      },
    ],
  },
  {
    type: "sectionView",
    alias: "UBookIt.SectionView.Bookings",
    name: "uBookIt Bookings Section View",
    js: () => import("./bookings-view.element.js"),
    // Below both configuration views, so the section still opens on Resources.
    // Seeing bookings is the common task, but configuring what can be booked is
    // the one an empty site has to do first.
    weight: 80,
    meta: {
      label: "#ubookitBookings_label",
      pathname: "bookings",
      icon: "icon-book-alt",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "UBookIt.Section",
      },
      {
        // Read OR Manage — the implication as a rule, mirroring the server.
        alias: UBOOKIT_VERB_CONDITION_ALIAS,
        oneOf: [BOOKINGS_READ_VERB, BOOKINGS_MANAGE_VERB],
      },
    ],
  },
  {
    type: "sectionView",
    alias: "UBookIt.SectionView.Settings",
    name: "uBookIt Settings Section View",
    js: () => import("./settings-view.element.js"),
    // Last. Settings are changed rarely and by fewer people than anything else here, so the
    // section never opens on them.
    weight: 60,
    meta: {
      label: "#ubookitSettings_label",
      pathname: "settings",
      icon: "icon-settings",
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "UBookIt.Section",
      },
      // DELIBERATELY NOT gated on the settings verb, unlike every other view here.
      //
      // The verb is never seeded, so on every upgraded site it starts out held by nobody — and
      // UBookItVerbHandler has no super-user bypass, so not even an Umbraco administrator has it
      // until somebody ticks the box. A tab that simply did not appear would read as a feature
      // that failed to ship. The view itself renders the explanation instead, and the server
      // refuses the data regardless, so nothing is exposed by showing the tab.
    ],
  },
  {
    type: "localization",
    alias: "UBookIt.Localization.EnUS",
    name: "uBookIt English (United States)",
    meta: {
      culture: "en-us",
    },
    js: () => import("../localization/en-us.js"),
  },
];
