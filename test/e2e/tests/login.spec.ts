import { test, expect } from "@playwright/test";
import { E2E_ADMIN_USERNAME, E2E_ADMIN_PASSWORD } from "../constants";

test("Admin can log in with the seeded fixed password", async ({ page }) => {
  await page.goto("/Account/Login");

  await page.getByPlaceholder("name@example.com").fill(E2E_ADMIN_USERNAME);
  await page.getByPlaceholder("password").fill(E2E_ADMIN_PASSWORD);
  await page.locator('button[type="submit"]').click();

  await expect(page.getByRole("link", { name: "Logout" })).toBeVisible();
  await expect(page).not.toHaveURL(/Account\/Login/);
});
