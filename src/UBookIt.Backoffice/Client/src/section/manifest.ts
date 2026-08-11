export const manifests: Array<UmbExtensionManifest> = [
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
