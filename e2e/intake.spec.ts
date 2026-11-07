import path from "node:path";
import { AxeBuilder } from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";

// A local test value from infra/.env.example, for the seeded accounts in db/seed/dev/020_staff.sql.
const PASSWORD = process.env.SEED_DEV_PASSWORD ?? "local-dev-password";

/** The golden documents in fixtures/golden, made by tools/golden/generate.mjs. */
const golden = (...parts: string[]) => path.join(import.meta.dirname, "..", "fixtures", "golden", ...parts);
const CLEAN_PACKET = ["invoice.pdf", "receipt.jpg", "prescription.docx", "itemised-bill.xlsx", "visit-summary.pdf"];

async function signIn(page: Page, email: string) {
  await page.goto("/sign-in");
  await page.getByLabel("Work email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).not.toHaveURL(/sign-in/);
}

/** SCR-11 and SCR-12: the golden member and policy, then the claim details. Returns the new claim number. */
async function registerGoldenClaim(page: Page): Promise<string> {
  await page.goto("/intake");
  await page.getByRole("link", { name: "New claim" }).first().click();

  // Typed without diacritics, as an officer often does; the citizen ID is shown masked only.
  await page.getByLabel("Member name, national ID or policy number").fill("Nguyen Van An");
  await page.getByRole("button", { name: "Search" }).click();
  const match = page.getByRole("row", { name: /Nguyễn Văn An/ }).first();
  await expect(match).toContainText("PL-2026-00731");
  await expect(match).toContainText("800,000 ₫");
  await expect(match).not.toContainText("000000000001");
  await page.getByRole("radio", { name: "Choose Nguyễn Văn An, policy PL-2026-00731" }).check();
  await page.getByRole("button", { name: "Continue" }).click();

  await page.getByLabel("Service date").fill("12/10/2026");
  await page.getByLabel("Provider").fill("Phòng khám Đa khoa Hồng Ngọc");
  await page.getByLabel("Claimed total (₫)").fill("1,400,000");
  await page.getByLabel("Counter").check();
  await page.getByRole("button", { name: "Create claim" }).click();

  await expect(page).toHaveURL(/\/intake\/[0-9a-f-]{36}$/);
  const header = await page.getByText(/CLM-\d{4}-\d{6}/).first().textContent();
  return header!.match(/CLM-\d{4}-\d{6}/)![0];
}

const pageImages = (page: Page) => page.locator('img[src^="blob:"]');

test.describe("Intake (US02, US03)", () => {
  test.beforeEach(async ({ page }) => {
    await signIn(page, "intake@claimlens.test");
  });

  test("the officer registers the golden claim, uploads its packet and submits it", async ({ page }) => {
    test.setTimeout(120_000);
    const claimNumber = await registerGoldenClaim(page);

    // JPG, PDF, DOCX and XLSX: each becomes Ready, and its pages are drawn, 6 in all.
    await page.getByLabel("Add files").setInputFiles(CLEAN_PACKET.map((name) => golden("clean", "documents", name)));
    for (const name of CLEAN_PACKET) {
      await expect(page.getByRole("listitem", { name }).getByText("Ready")).toBeVisible({ timeout: 60_000 });
    }
    await expect(pageImages(page)).toHaveCount(6, { timeout: 30_000 });

    // The receipt is stored on its side with EXIF orientation 6; its page comes out upright.
    const receipt = page.locator('img[src^="blob:"][alt*="receipt.jpg"]');
    await expect(receipt).toHaveJSProperty("complete", true);
    const { width, height } = await receipt.evaluate((image: HTMLImageElement) => ({
      width: image.naturalWidth,
      height: image.naturalHeight,
    }));
    expect(height).toBeGreaterThan(width);

    // Removing a file takes its pages away and the others close the gap.
    await page.getByRole("button", { name: "Remove itemised-bill.xlsx" }).click();
    await expect(page.getByRole("listitem", { name: "itemised-bill.xlsx" })).toHaveCount(0);
    await expect(pageImages(page)).toHaveCount(5);
    await expect(page.getByText("p5", { exact: true })).toBeVisible();
    await expect(page.getByText("p6", { exact: true })).toHaveCount(0);

    await page.getByRole("button", { name: "Submit" }).click();
    // Submitted: Processing, until the AI Job Runner moves the claim on (ai-reading.spec.ts follows that part).
    await expect(page.getByText(/^(Processing|Needs verification|Ready for review)$/).first()).toBeVisible();
    await expect(page.getByLabel("Add files")).toHaveCount(0);

    await page.goto("/intake?tab=submitted");
    await expect(page.getByText(claimNumber)).toBeVisible();
  });

  test("a file that is not what its name says is refused by its bytes, next to the file", async ({ page }) => {
    await registerGoldenClaim(page);

    await page.getByLabel("Add files").setInputFiles(golden("invalid", "not-a-docx.docx"));

    await expect(page.getByText("This file type is not accepted. Use JPG, PNG, PDF, DOCX or XLSX.")).toBeVisible();
    await expect(page.getByRole("button", { name: "Submit" })).toBeDisabled();
  });

  test("the documents screen meets WCAG AA", async ({ page }) => {
    await registerGoldenClaim(page);
    await page.getByLabel("Add files").setInputFiles(golden("clean", "documents", "invoice.pdf"));
    await expect(page.getByRole("listitem", { name: "invoice.pdf" }).getByText("Ready")).toBeVisible({ timeout: 60_000 });

    const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa"]).analyze();

    expect(results.violations).toEqual([]);
  });
});
