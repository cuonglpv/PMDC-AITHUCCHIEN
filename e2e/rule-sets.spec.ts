import { AxeBuilder } from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";

// A local test value from infra/.env.example, for the seeded accounts in db/seed/dev/020_staff.sql.
const PASSWORD = process.env.SEED_DEV_PASSWORD ?? "local-dev-password";

async function signIn(page: Page, email: string) {
  await page.goto("/sign-in");
  await page.getByLabel("Work email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).not.toHaveURL(/sign-in/);
}

/** Today in Vietnam, where the API keeps business dates, as the date input wants it and as the app shows it. */
function todayInVietnam() {
  const parts = new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Ho_Chi_Minh", day: "2-digit", month: "2-digit", year: "numeric" })
    .formatToParts(new Date());
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((item) => item.type === type)?.value ?? "";
  return { iso: `${part("year")}-${part("month")}-${part("day")}`, shown: `${part("day")}/${part("month")}/${part("year")}` };
}

async function openGoldPlan(page: Page) {
  await page.goto("/plans");
  await page.getByRole("link", { name: "GOLD-OP" }).click();
  await expect(page.getByRole("heading", { name: "Gold Outpatient" })).toBeVisible();
}

/** Leaves the seeded plan as it was: a draft left behind would block the next run. */
async function discardOpenDraft(page: Page) {
  await openGoldPlan(page);
  const open = page.getByRole("link", { name: /Open draft/ });
  if (await open.isVisible()) {
    await open.click();
    await page.getByRole("button", { name: "Discard draft" }).first().click();
    await page.getByRole("dialog").getByRole("button", { name: "Discard draft" }).click();
    await expect(page.getByRole("button", { name: /New draft/ })).toBeVisible();
  }
}

test.describe("Benefit rule sets (US19)", () => {
  test.beforeEach(async ({ page }) => {
    await signIn(page, "admin@claimlens.test");
    await discardOpenDraft(page);
  });

  test.afterEach(async ({ page }) => {
    await discardOpenDraft(page);
  });

  test("the Administrator drafts from the published version, edits a rule and saves it", async ({ page }) => {
    await page.getByRole("button", { name: "New draft from v3" }).click();
    await expect(page.getByRole("heading", { name: /Rule set v\d+/ })).toBeVisible();
    await expect(page.getByRole("tab", { name: "Categories (5)" })).toBeVisible();

    await page.getByRole("tab", { name: /Coinsurance/ }).click();
    const rate = page.getByLabel("Insurer pays (%), COINS-80");
    await expect(rate).toHaveValue("80");
    await rate.fill("75");
    await page.getByRole("button", { name: "Save draft" }).click();
    await expect(page.getByText("Saved", { exact: true })).toBeVisible();

    await page.reload();
    await page.getByRole("tab", { name: /Coinsurance/ }).click();
    await expect(page.getByLabel("Insurer pays (%), COINS-80")).toHaveValue("75");
  });

  test("a rule without its values is refused next to the field", async ({ page }) => {
    await page.getByRole("button", { name: "New draft from v3" }).click();
    await page.getByRole("button", { name: "Add covered category" }).click();
    await page.getByLabel("Rule code, new rule").last().fill("COV-99");
    await page.getByLabel("Clause text, COV-99").fill("Dental care is covered.");
    await page.getByRole("button", { name: "Save draft" }).click();

    await expect(page.getByText("Choose a benefit category.")).toBeVisible();
    await expect(page.getByLabel("Category, COV-99")).toHaveAttribute("aria-invalid", "true");
    await expect(page.getByText("Unsaved changes")).toBeVisible();
  });

  test("publishing refuses a date in the past and names the earliest it accepts", async ({ page }) => {
    const today = todayInVietnam();
    await page.getByRole("button", { name: "New draft from v3" }).click();
    await page.getByRole("button", { name: "Publish" }).click();
    const date = page.getByLabel("Effective from");
    await expect(date).toHaveAttribute("min", today.iso);
    await date.fill("2026-01-01");
    await page.getByRole("dialog").getByRole("button", { name: /Publish v\d+/ }).click();

    await expect(page.getByText(`It must be today, ${today.shown}, or later.`)).toBeVisible();
    await expect(date).toHaveAttribute("aria-invalid", "true");
  });

  test("the editor meets WCAG AA", async ({ page }) => {
    await page.getByRole("button", { name: "New draft from v3" }).click();
    await expect(page.getByRole("tab", { name: "Categories (5)" })).toBeVisible();

    const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21aa"]).analyze();

    expect(results.violations.map((item) => `${item.id}: ${item.nodes[0]?.target}`)).toEqual([]);
  });
});

test("a reviewer reads the published version and cannot change it (US19)", async ({ page }) => {
  await signIn(page, "ha.tran@claimlens.test");
  await openGoldPlan(page);

  await expect(page.getByRole("button", { name: /New draft/ })).toHaveCount(0);
  await page.getByRole("link", { name: "v3" }).click();

  await expect(page.getByText("A published version cannot be changed.", { exact: false })).toBeVisible();
  await expect(page.getByRole("textbox")).toHaveCount(0);
  await expect(page.getByText("COV-01")).toBeVisible();
});
