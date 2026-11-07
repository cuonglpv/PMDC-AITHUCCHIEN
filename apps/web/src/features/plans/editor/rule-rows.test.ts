import { describe, expect, it } from "vitest";
import type { components } from "@/lib/api/schema";
import { errorsByRow, fromRules, newRow, toInput, unconfiguredCategories } from "./rule-rows";

type BenefitRule = components["schemas"]["BenefitRule"];

const rule = (overrides: Partial<BenefitRule>): BenefitRule => ({
  id: "r",
  ruleCode: "X",
  ruleType: "COVERAGE",
  categoryCode: null,
  clauseText: "c",
  clauseRef: null,
  applyOrder: 40,
  rateBp: null,
  limitAmount: null,
  limitScope: null,
  waitingDays: null,
  ...overrides,
});

describe("rule rows", () => {
  it("leaves out the system eligibility rule, which has no tab", () => {
    const rows = fromRules([rule({ ruleType: "ELIGIBILITY", ruleCode: "ELIG-01" }), rule({ ruleCode: "COV-01" })]);

    expect(rows.map((row) => row.ruleCode)).toEqual(["COV-01"]);
  });

  it("shows the rate as a percentage and sends basis points", () => {
    const [row] = fromRules([rule({ ruleType: "COINSURANCE", ruleCode: "COINS-80", rateBp: 8000 })]);

    expect(row!.ratePercent).toBe("80");
    expect(toInput({ ...row!, ratePercent: "80.5" }).rateBp).toBe(8050);
  });

  it("sends a limit with its scope, and money as a whole number", () => {
    const row = { ...newRow("LIMIT"), ruleCode: "LIM", clauseText: "c", limitAmount: "5,000,000", limitScope: "ANNUAL" as const };

    expect(toInput(row)).toMatchObject({ ruleType: "LIMIT", limitAmount: 5_000_000, limitScope: "ANNUAL", applyOrder: 60 });
  });

  it("sends only the values a type uses", () => {
    const row = { ...newRow("COVERAGE"), ruleCode: "C", clauseText: "c", categoryCode: "LAB_TEST", ratePercent: "50" };

    expect(toInput(row)).not.toHaveProperty("rateBp");
  });

  it("puts each API error next to the field of the row it names", () => {
    const rows = [newRow("COVERAGE"), newRow("EXCLUSION")];

    const { byRow, general } = errorsByRow(rows, [
      { field: "rules[1].categoryCode", code: "REQUIRED", message: "Choose a benefit category." },
      { field: "rules", code: "MISSING_CATEGORY", message: "Procedure is neither covered nor excluded." },
    ]);

    expect(byRow.get(rows[1]!.key)).toEqual({ categoryCode: "Choose a benefit category." });
    expect(byRow.has(rows[0]!.key)).toBe(false);
    expect(general).toEqual(["Procedure is neither covered nor excluded."]);
  });

  it("names the categories that are neither covered nor excluded", () => {
    const rows = [{ ...newRow("COVERAGE"), categoryCode: "LAB_TEST" }, { ...newRow("WAITING_PERIOD"), categoryCode: "COSMETIC" }];

    const missing = unconfiguredCategories(rows, [
      { code: "LAB_TEST", name: "Laboratory tests", description: null },
      { code: "COSMETIC", name: "Cosmetic products", description: null },
    ]);

    expect(missing.map((category) => category.name)).toEqual(["Cosmetic products"]);
  });
});
