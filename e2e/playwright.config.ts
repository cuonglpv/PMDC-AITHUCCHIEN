import { defineConfig } from "@playwright/test";

// Runs against the whole stack: docker compose -f infra/compose.yaml --profile full up -d --build
export default defineConfig({
  testDir: ".",
  outputDir: "test-results",
  reporter: [["list"]],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:8088",
    // Locally this uses the Chrome already installed. CI installs Playwright's own Chromium.
    channel: process.env.CI ? undefined : "chrome",
    trace: "retain-on-failure",
  },
});
