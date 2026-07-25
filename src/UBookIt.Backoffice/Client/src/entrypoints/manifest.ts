export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "UBook It Backoffice Entrypoint",
    alias: "UBookIt.Backoffice.Entrypoint",
    type: "backofficeEntryPoint",
    js: () => import("./entrypoint.js"),
  },
];
