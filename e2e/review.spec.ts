import path from "node:path";
import { AxeBuilder } from "@axe-core/playwright";
import { expect, test, type Browser, type Page } from "@playwright/test";

// A local test value from infra/.env.example, for the seeded accounts in db/seed/dev/020_staff.sql.
const PASSWORD = process.env.SEED_DEV_PASSWORD ?? "local-dev-password";

/**
 * The golden documents in fixtures/golden. In the full stack, the Document AI fake gateway recognises their pages and
 * replays the recorded answers in fixtures/golden/<scenario>/ai-responses/pages.json.
 */
const golden = (...parts: string[]) => path.join(import.meta.dirname, "..", "fixtures", "golden", ...parts);
const CLEAN_PACKET = ["invoice.pdf", "receipt.jpg", "prescription.docx", "itemised-bill.xlsx", "visit-summary.pdf"];

/** Each person has their own browser context, as they would at their own desk. */
async function signedIn(browser: Browser, email: string): Promise<Page> {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();
  await page.goto("/sign-in");
  await page.getByLabel("Work email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).not.toHaveURL(/sign-in/);
  return page;
}

/** SCR-11 and SCR-12 for the golden member and policy. Returns the new claim number. */
async function registerGoldenClaim(page: Page): Promise<string> {
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
  const header = await page.getByText(/CLM-\d{4}-\d{6}/).first().textContent();
  return header!.match(/CLM-\d{4}-\d{6}/)![0];
}

/** The officer submits the claim once its files are read, and the AI Job Runner moves it on. */
async function submit(officer: Page) {
  await officer.getByRole("button", { name: "Submit" }).click();
  await expect(officer.getByText("Needs verification").first()).toBeVisible({ timeout: 90_000 });
}

const fieldRows = (page: Page) => page.locator("section[aria-label] ul > li[aria-label]");
const actionBar = (page: Page) => page.getByRole("region", { name: "Progress and actions" });
const counter = (page: Page) => page.getByTestId("fields-counter");
const claimStatus = (page: Page, status: string) => page.getByRole("main").getByText(status, { exact: true }).first();

async function selectRow(row: ReturnType<typeof fieldRows>) {
  await row.getByRole("button").first().click();
}

/** Drags over the page in the viewer. Page numbers in zone C depend on upload timing, so the region is not asserted against one. */
async function drawRegion(page: Page) {
  const surface = page.getByRole("application", { name: /Draw the source region/ });
  await surface.waitFor();
  const box = (await surface.boundingBox())!;
  await page.mouse.move(box.x + box.width * 0.2, box.y + box.height * 0.2);
  await page.mouse.down();
  await page.mouse.move(box.x + box.width * 0.6, box.y + box.height * 0.3, { steps: 5 });
  await page.mouse.up();
}

async function startFromQueue(reviewer: Page, claimNumber: string) {
  await reviewer.goto("/queue");
  await reviewer.getByLabel("Filter by claim no.").fill(claimNumber);
  await reviewer.getByRole("button", { name: `Start review of ${claimNumber}` }).click();
  await expect(reviewer).toHaveURL(/\/claims\/[0-9a-f-]{36}\/evidence$/, { timeout: 15_000 });
  await expect(reviewer.getByText("Assigned to Trần Thu Hà")).toBeVisible();
}

test.describe("Review queue and Evidence (US23, US06)", () => {
  test("a reviewer takes the golden claim, enters the diagnosis from a drawn region, and it goes In review", async ({ browser }) => {
    test.setTimeout(240_000);
    const officer = await signedIn(browser, "intake@claimlens.test");
    const claimNumber = await registerGoldenClaim(officer);
    await officer.getByLabel("Add files").setInputFiles(CLEAN_PACKET.map((name) => golden("clean", "documents", name)));
    await expect(officer.locator('img[src^="blob:"]')).toHaveCount(6, { timeout: 90_000 });
    await expect(officer.getByTitle("Suggested by AI")).toHaveCount(6, { timeout: 60_000 });
    await submit(officer);

    // BR-11: both see it in Available, with the plan and no member name, until one of them takes it.
    const reviewer = await signedIn(browser, "ha.tran@claimlens.test");
    const other = await signedIn(browser, "duc.pham@claimlens.test");
    for (const page of [reviewer, other]) {
      await page.goto("/queue");
      await page.getByLabel("Filter by claim no.").fill(claimNumber);
      await expect(page.getByRole("button", { name: `Start review of ${claimNumber}` })).toBeVisible();
      // The heading row and this claim alone: the filter has landed.
      await expect(page.getByRole("row")).toHaveCount(2);
      await expect(page.getByRole("row").nth(1)).toContainText("GOLD-OP");
      await expect(page.getByRole("row").nth(1)).not.toContainText("Nguyễn");
    }
    const queueScan = await new AxeBuilder({ page: reviewer }).withTags(["wcag2a", "wcag2aa"]).analyze();
    expect(queueScan.violations).toEqual([]);

    await reviewer.getByRole("button", { name: `Start review of ${claimNumber}` }).click();
    await expect(reviewer).toHaveURL(/\/claims\/[0-9a-f-]{36}\/evidence$/, { timeout: 15_000 });
    const claimUrl = reviewer.url();
    await expect(reviewer.getByRole("main").getByText("Nguyễn Văn An").first()).toBeVisible();

    // The second reviewer's list was out of date: told, and the row goes.
    await other.getByRole("button", { name: `Start review of ${claimNumber}` }).click();
    await expect(other.getByText(/Another reviewer took this claim first/)).toBeVisible();
    await expect(other.getByRole("button", { name: `Start review of ${claimNumber}` })).toHaveCount(0);

    // Lowest confidence first: the two diagnoses read below 0.70 come up empty, for a person to enter.
    await expect(fieldRows(reviewer).first()).toBeVisible({ timeout: 15_000 });
    await expect(fieldRows(reviewer).first()).toContainText("Diagnosis");
    await expect(fieldRows(reviewer).first()).toContainText("Needs entry");
    await expect(counter(reviewer)).toHaveText("1 field to verify");
    await expect(actionBar(reviewer)).toContainText("1 field to verify");
    await expect(actionBar(reviewer)).not.toContainText("left to verify");
    await expect(fieldRows(reviewer).filter({ hasText: "ID number" })).toContainText("********0001");
    const evidenceScan = await new AxeBuilder({ page: reviewer }).withTags(["wcag2a", "wcag2aa"]).analyze();
    expect(evidenceScan.violations).toEqual([]);

    // The required diagnosis, by keyboard to open and to save, with its source drawn on the page.
    const diagnosis = fieldRows(reviewer).filter({ hasText: "Needs entry" }).filter({ hasText: "required" }).first();
    await selectRow(diagnosis);
    await reviewer.keyboard.press("e");
    const input = diagnosis.getByRole("textbox");
    await expect(input).toBeFocused();
    await input.fill("J02.9");
    await diagnosis.getByRole("button", { name: /Draw region|Redraw region/ }).click();
    await drawRegion(reviewer);
    await expect(reviewer.getByRole("img", { name: "Source region: Drawn region" })).toBeVisible();
    await input.press("Enter");

    // The last required field: the claim passes through Ready for review to In review, because it has an assignee.
    await expect(claimStatus(reviewer, "In review")).toBeVisible();
    await expect(counter(reviewer)).toHaveText("No fields to verify");
    const entered = fieldRows(reviewer).filter({ hasText: "J02.9" }).first();
    await expect(entered.getByTitle("Set by Trần Thu Hà")).toBeVisible();

    // Re-run AI, moving to another page while it runs: the result still arrives, and what a person entered stays.
    await reviewer.getByRole("listitem", { name: "Page 1", exact: true }).getByRole("button").first().click();
    await reviewer.getByRole("button", { name: "Re-run AI on this page" }).click();
    await reviewer.getByRole("listitem", { name: "Page 2", exact: true }).getByRole("button").first().click();
    await expect(reviewer.getByText(/AI read p1 again/)).toBeVisible({ timeout: 45_000 });
    await expect(fieldRows(reviewer).filter({ hasText: "J02.9" }).first()).toBeVisible();

    // The top bar opens it for its assignee, and says the same for someone else's claim as for no claim at all.
    await reviewer.goto("/queue");
    await reviewer.getByLabel("Go to claim no.").fill(claimNumber.toLowerCase());
    await reviewer.getByLabel("Go to claim no.").press("Enter");
    await expect(reviewer).toHaveURL(claimUrl);
    await expect(reviewer.getByLabel("Go to claim no.")).not.toBeFocused();

    const noClaim = "No claim with this number that you can open.";
    await other.getByLabel("Go to claim no.").fill(claimNumber);
    await other.getByLabel("Go to claim no.").press("Enter");
    await expect(other.getByText(noClaim)).toBeVisible();
    await other.getByLabel("Go to claim no.").fill("CLM-2026-999999");
    await other.getByLabel("Go to claim no.").press("Enter");
    await expect(other.getByText(noClaim)).toBeVisible();
    await other.goto(claimUrl);
    await expect(other.getByText("You do not have access to this page")).toBeVisible();

    // The approver reads the evidence and has nothing to press.
    const approver = await signedIn(browser, "quan.le@claimlens.test");
    await approver.goto(claimUrl);
    await expect(fieldRows(approver).first()).toBeVisible({ timeout: 15_000 });
    await selectRow(fieldRows(approver).first());
    await expect(
      approver.getByRole("button", { name: /^(Accept|Edit|Enter value|Not present|Add expense line|Re-run AI on this page)$/ }),
    ).toHaveCount(0);
  });

  test("a page the AI could not classify: the reviewer chooses its type, enters by hand and adds a line", async ({ browser }) => {
    test.setTimeout(180_000);
    const officer = await signedIn(browser, "intake@claimlens.test");
    const claimNumber = await registerGoldenClaim(officer);
    await officer.getByLabel("Add files").setInputFiles(golden("blurry", "documents", "receipt-blurry.jpg"));
    await expect(officer.getByText("Unclassified", { exact: true }).first()).toBeVisible({ timeout: 90_000 });
    await submit(officer);

    const reviewer = await signedIn(browser, "ha.tran@claimlens.test");
    await startFromQueue(reviewer, claimNumber);

    // Nothing to enter until the page has a type.
    await expect(actionBar(reviewer)).toContainText("1 page still needs a type.");
    await expect(fieldRows(reviewer)).toHaveCount(0);
    await reviewer.getByLabel(/Choose type for page 1/).selectOption({ label: "Invoice" });
    await expect(fieldRows(reviewer).first()).toBeVisible();
    await expect(actionBar(reviewer)).toContainText("5 fields to verify");
    await expect(actionBar(reviewer)).not.toContainText("needs a type");

    // BR-02: a value with no source opens the drawing tool, and the same Save goes through after drawing.
    const total = fieldRows(reviewer).filter({ hasText: "Invoice total" });
    await selectRow(total);
    await total.getByRole("button", { name: "Enter value" }).click();
    await total.getByRole("textbox").fill("1,400,000");
    await total.getByRole("textbox").press("Enter");
    await expect(total.getByRole("alert")).toContainText("no source on the page");
    await drawRegion(reviewer);
    await total.getByRole("textbox").press("Enter");
    await expect(total).toContainText("1,400,000 ₫");
    await expect(counter(reviewer)).toHaveText("4 fields to verify");
    await expect(actionBar(reviewer)).not.toContainText("left to verify");

    // A second tab saves first: this one is told what is there now and keeps what was typed.
    const provider = fieldRows(reviewer).filter({ hasText: "Provider" }).filter({ hasText: "required" });
    await selectRow(provider);
    await provider.getByRole("button", { name: "Enter value" }).click();
    await provider.getByRole("textbox").fill("Typed here");

    const otherTab = await reviewer.context().newPage();
    await otherTab.goto(reviewer.url());
    const theirs = fieldRows(otherTab).filter({ hasText: "Provider" }).filter({ hasText: "required" });
    await selectRow(theirs);
    await theirs.getByRole("button", { name: "Enter value" }).click();
    await theirs.getByRole("textbox").fill("Saved in the other tab");
    await theirs.getByRole("button", { name: "Draw region" }).click();
    await drawRegion(otherTab);
    await theirs.getByRole("textbox").press("Enter");
    await expect(theirs).toContainText("Saved in the other tab");
    await otherTab.close();

    await provider.getByRole("button", { name: "Draw region" }).click();
    await drawRegion(reviewer);
    await provider.getByRole("textbox").press("Enter");
    await expect(provider).toContainText("Someone else changed this first");
    await expect(provider).toContainText("Saved in the other tab");
    await expect(provider.getByRole("textbox")).toHaveValue("Typed here");

    // A line added by hand brings its fields, and goes again when removed; a new one does not reuse the number.
    await provider.getByRole("button", { name: "Cancel" }).click();
    await reviewer.getByRole("button", { name: "Add expense line" }).click();
    await expect(fieldRows(reviewer).filter({ hasText: "Line 1 amount" })).toBeVisible();
    await expect(counter(reviewer)).toHaveText("5 fields to verify");
    const amount = fieldRows(reviewer).filter({ hasText: "Line 1 amount" });
    await selectRow(amount);
    await amount.getByRole("button", { name: "Remove line" }).click();
    await reviewer.getByRole("dialog").getByRole("button", { name: "Remove line" }).click();
    await expect(fieldRows(reviewer).filter({ hasText: "Line 1" })).toHaveCount(0);
    await expect(counter(reviewer)).toHaveText("3 fields to verify");

    await reviewer.getByRole("button", { name: "Add expense line" }).click();
    await expect(fieldRows(reviewer).filter({ hasText: "Line 2 amount" })).toBeVisible();
  });
});
