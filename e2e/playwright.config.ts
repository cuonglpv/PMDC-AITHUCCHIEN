import { defineConfig } from "@playwright/test";

/** Specs that upload whole packets. One at a time, so they do not queue behind each other in Document Preparation. */
const UPLOADS = /(intake|ai-reading)\.spec\.ts$/;

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
  projects: [
    { name: "app", testIgnore: UPLOADS },
    { name: "uploads", testMatch: UPLOADS, workers: 1 },
  ],
});
