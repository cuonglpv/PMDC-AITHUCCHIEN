import path from "node:path";
import { AxeBuilder } from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";

// A local test value from infra/.env.example, for the seeded accounts in db/seed/dev/020_staff.sql.
const PASSWORD = process.env.SEED_DEV_PASSWORD ?? "local-dev-password";

/**
 * The golden documents in fixtures/golden. In the full stack, the Document AI fake gateway recognises their pages and
 * replays the recorded answers in fixtures/golden/<scenario>/ai-responses/pages.json.
 */
const golden = (...parts: string[]) => path.join(import.meta.dirname, "..", "fixtures", "golden", ...parts);
const CLEAN_PACKET = ["invoice.pdf", "receipt.jpg", "prescription.docx", "itemised-bill.xlsx", "visit-summary.pdf"];

async function signIn(page: Page, email: string) {
  await page.goto("/sign-in");
  await page.getByLabel("Work email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).not.toHaveURL(/sign-in/);
}

/** SCR-11 and SCR-12 for the golden member and policy. */
async function registerGoldenClaim(page: Page) {
  await page.goto("/intake/new");
  await page.getByLabel("Member name, national ID or policy number").fill("Nguyen Van An");
  await page.getByRole("button", { name: "Search" }).click();
  await page.getByRole("radio", { name: "Choose Nguyễn Văn An, policy PL-2026-00731" }).check();
  await page.getByRole("button", { name: "Continue" }).click();
  await page.getByLabel("Service date").fill("12/10/2026");
  await page.getByLabel("Provider").fill("Phòng khám Đa khoa Hồng Ngọc");
  await page.getByLabel("Claimed total (₫)").fill("1,400,000");
  await page.getByLabel("Counter").check();
  await page.getByRole("button", { name: "Create claim" }).click();
  await expect(page).toHaveURL(/\/intake\/[0-9a-f-]{36}$/);
}

/** The page card of a file's first page on SCR-13. */
const pageOf = (page: Page, file: string) =>
  page
    .getByRole("listitem")
    .filter({ has: page.getByRole("img", { name: new RegExp(`page 1 of ${file.replaceAll(".", "\\.")}$`) }) });

test.describe("AI page check and extraction (US04, US05)", () => {
  test.beforeEach(async ({ page }) => {
    await signIn(page, "intake@claimlens.test");
  });

  test("each page of the golden packet gets its type from the AI, and the submitted claim needs verification", async ({ page }) => {
    test.setTimeout(150_000);
    await registerGoldenClaim(page);
    await page.getByLabel("Add files").setInputFiles(CLEAN_PACKET.map((name) => golden("clean", "documents", name)));
    await expect(page.locator('img[src^="blob:"]')).toHaveCount(6, { timeout: 90_000 });

    // The type the AI gave, with the ✦ AI marker and the level the API computed (BR-03: 0.80 for a page type).
    for (const [file, type] of [
      ["invoice.pdf", "Invoice"],
      ["receipt.jpg", "Receipt"],
      ["prescription.docx", "Prescription"],
      ["visit-summary.pdf", "Visit summary"],
      // The itemised bill repeats the invoice's lines; as Other, it gives no expense lines (US05).
      ["itemised-bill.xlsx", "Other"],
    ] as const) {
      const card = pageOf(page, file);
      await expect(card.getByText(type, { exact: true })).toBeVisible({ timeout: 60_000 });
      await expect(card.getByText("High", { exact: true })).toBeVisible();
      await expect(card.getByTitle("Suggested by AI")).toBeVisible();
    }

    const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();
    expect(results.violations).toEqual([]);

    // Extraction auto-accepts what it read above the thresholds. The diagnosis was read below 0.70, so a person must
    // enter it, and the claim waits for verification.
    await page.getByRole("button", { name: "Submit" }).click();
    await expect(page.getByText("Needs verification").first()).toBeVisible({ timeout: 60_000 });
  });

  test("a blurred receipt is Unclassified and low quality, and the officer chooses its type", async ({ page }) => {
    await registerGoldenClaim(page);
    await page.getByLabel("Add files").setInputFiles(golden("blurry", "documents", "receipt-blurry.jpg"));

    const card = pageOf(page, "receipt-blurry.jpg");
    await expect(card.getByText("Unclassified", { exact: true })).toBeVisible({ timeout: 60_000 });
    await expect(card.getByText("Check", { exact: true })).toBeVisible();
    await expect(card.getByText("Low quality, re-capture")).toBeVisible();

    await card.getByLabel(/Choose type for page/).selectOption({ label: "Receipt" });

    await expect(card.getByText("Receipt", { exact: true })).toBeVisible();
    await expect(card.getByText("Unclassified", { exact: true })).toHaveCount(0);
    // The officer's initials replace the AI marker once a person sets the type.
    await expect(card.getByTitle("Set by Nguyễn Minh Anh")).toHaveText(/MA$/);
    await expect(card.getByTitle("Suggested by AI")).toHaveCount(0);
  });
});
