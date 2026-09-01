import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: "src/bundle.manifests.ts", // Bundle registers one or more manifests
      formats: ["es"],
      fileName: "u-book-it-backoffice",
    },
    outDir: "../wwwroot/App_Plugins/UBookItBackoffice", // your web component will be saved in this location
    emptyOutDir: true,

    // Source maps ship in the package, as a decision rather than as a default.
    //
    // They are about half of the backoffice package's ~176 KB, which is nothing beside
    // Umbraco's own backoffice, and they are the difference between a consumer debugging a
    // problem in our section and staring at minified output. Nothing here is secret: the
    // source is MIT and public.
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco/],
    },
  },
});
