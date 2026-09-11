import { defineConfig } from "@playwright/test";
import { BASE_URL } from "./constants";

export default defineConfig({
  testDir: "./tests",
  fullyParallel: false,
  reporter: "list",
  use: {
    baseURL: BASE_URL,
    trace: "retain-on-failure",
  },
  webServer: {
    command: "node scripts/start-server.mjs",
    url: BASE_URL,
    reuseExistingServer: false,
    timeout: 120_000,
  },
});
