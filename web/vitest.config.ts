import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

// Node environment only: what is under test here is the dashboard's pure logic (funnel maths,
// distribution scaling, number formatting), not rendered components — so no jsdom, no React
// testing library, no extra surface to keep working.
export default defineConfig({
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
});
