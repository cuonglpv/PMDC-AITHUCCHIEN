import { AxeBuilder } from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";

// A local test value from infra/.env.example, for the seeded accounts in db/seed/dev/020_staff.sql.
const PASSWORD = process.env.SEED_DEV_PASSWORD ?? "local-dev-password";

const ACCOUNTS = [
  { role: "Intake Officer", email: "intake@claimlens.test", home: "/intake", nav: ["Intake"] },
  { role: "Claims Reviewer", email: "ha.tran@claimlens.test", home: "/queue", nav: ["Review queue", "Benefit plans"] },
  { role: "Senior Approver", email: "quan.le@claimlens.test", home: "/approvals", nav: ["Approval queue", "Benefit plans"] },
  { role: "Administrator", email: "admin@claimlens.test", home: "/plans", nav: ["Benefit plans"] },
];

async function signIn(page: Page, email: string, password = PASSWORD) {
  await page.getByLabel("Work email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
}

for (const account of ACCOUNTS) {
  test(`${account.role} lands on their home and sees only their menu (US01)`, async ({ page }) => {
    await page.goto("/sign-in");
    await signIn(page, account.email);

    await expect(page).toHaveURL(account.home);
    const links = page.getByRole("navigation", { name: "Main" }).getByRole("link");
    await expect(links).toHaveCount(account.nav.length);
    for (const label of account.nav) {
      await expect(links.filter({ hasText: label })).toBeVisible();
    }
    await expect(page.getByText(account.role, { exact: true })).toBeVisible();
  });
}

test("a wrong password shows one generic message and keeps the input (US01)", async ({ page }) => {
  await page.goto("/sign-in");
  await signIn(page, "duc.pham@claimlens.test", "not-the-password");

  // Next.js keeps its own empty alert region for route changes, so this one is picked by its text.
  await expect(page.getByRole("alert").filter({ hasText: "Wrong email or password." })).toBeVisible();
  await expect(page.getByLabel("Work email")).toHaveValue("duc.pham@claimlens.test");
});

test("a protected page sends a visitor to Sign in and back afterwards", async ({ page }) => {
  await page.goto("/queue");
  await expect(page).toHaveURL(/\/sign-in\?next=%2Fqueue/);

  await signIn(page, "ha.tran@claimlens.test");

  await expect(page).toHaveURL("/queue");
});

test("a page outside the role shows 403 with a way home (US01)", async ({ page }) => {
  await page.goto("/sign-in");
  await signIn(page, "ha.tran@claimlens.test");
  await expect(page).toHaveURL("/queue");

  await page.goto("/approvals");

  await expect(page).toHaveURL("/403");
  await page.getByRole("link", { name: "Back to your home page" }).click();
  await expect(page).toHaveURL("/queue");
});

test("reloading keeps the session, because the refresh cookie restores it", async ({ page }) => {
  await page.goto("/sign-in");
  await signIn(page, "ha.tran@claimlens.test");
  await expect(page).toHaveURL("/queue");

  await page.reload();

  await expect(page).toHaveURL("/queue");
  await expect(page.getByText("Trần Thu Hà")).toBeVisible();
});

test("the token never reaches script-readable storage, and the cookie is HttpOnly", async ({ page, context }) => {
  await page.goto("/sign-in");
  await signIn(page, "ha.tran@claimlens.test");
  await expect(page).toHaveURL("/queue");

  const visible = await page.evaluate(() => ({
    cookie: document.cookie,
    local: Object.keys(localStorage).length,
    session: Object.keys(sessionStorage).length,
  }));
  expect(visible).toEqual({ cookie: "", local: 0, session: 0 });

  const cookie = (await context.cookies()).find((item) => item.name === "claimlens_refresh");
  expect(cookie?.httpOnly).toBe(true);
  expect(cookie?.secure).toBe(true);
  expect(cookie?.sameSite).toBe("Strict");
  expect(cookie?.path).toBe("/api/v1/auth");
});

test("signing out ends the session for good", async ({ page }) => {
  await page.goto("/sign-in");
  await signIn(page, "ha.tran@claimlens.test");
  await expect(page).toHaveURL("/queue");

  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL(/\/sign-in/);

  await page.goto("/queue");
  await expect(page).toHaveURL(/\/sign-in/);
});

test("Sign in meets WCAG AA", async ({ page }) => {
  await page.goto("/sign-in");
  await expect(page.getByRole("heading", { name: "Sign in" })).toBeVisible();

  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21aa"]).analyze();

  expect(results.violations.map((item) => `${item.id}: ${item.nodes[0]?.target}`)).toEqual([]);
});

test("the signed-in shell meets WCAG AA", async ({ page }) => {
  await page.goto("/sign-in");
  await signIn(page, "ha.tran@claimlens.test");
  await expect(page).toHaveURL("/queue");
  await expect(page.getByText("Trần Thu Hà")).toBeVisible();

  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21aa"]).analyze();

  expect(results.violations.map((item) => `${item.id}: ${item.nodes[0]?.target}`)).toEqual([]);
});
