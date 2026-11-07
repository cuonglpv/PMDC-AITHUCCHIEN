import type { components } from "@/lib/api/schema";
import type { LimitScope, RuleType } from "@/lib/labels/rule-sets";

type BenefitRule = components["schemas"]["BenefitRule"];
type BenefitRuleInput = components["schemas"]["BenefitRuleInput"];
type BenefitCategory = components["schemas"]["BenefitCategory"];
type FieldError = components["schemas"]["FieldError"];

export type EditableType = Exclude<RuleType, "ELIGIBILITY">;

/** One rule as the editor holds it: text the person is typing, turned into numbers only when saved. */
export type RuleRow = {
  key: string;
  ruleType: EditableType;
  ruleCode: string;
  categoryCode: string;
  clauseText: string;
  clauseRef: string;
  applyOrder: string;
  ratePercent: string;
  limitAmount: string;
  limitScope: LimitScope | "";
  waitingDays: string;
};

/** The editor tabs of docs/04-information-architecture.md. They are views of one list of rules. */
export const TABS: { id: string; label: string; addLabel: string; type: EditableType; single?: boolean }[] = [
  { id: "categories", label: "Categories", addLabel: "Add covered category", type: "COVERAGE" },
  { id: "exclusions", label: "Exclusions", addLabel: "Add exclusion", type: "EXCLUSION" },
  { id: "waiting", label: "Waiting periods", addLabel: "Add waiting period", type: "WAITING_PERIOD" },
  { id: "coinsurance", label: "Coinsurance", addLabel: "Add reimbursement rate", type: "COINSURANCE", single: true },
  { id: "limits", label: "Limits", addLabel: "Add limit", type: "LIMIT" },
];

/** Where a new rule goes in the order of application, matching the seeded rule sets. */
const DEFAULT_ORDER: Record<EditableType, number> = {
  WAITING_PERIOD: 20,
  EXCLUSION: 30,
  COVERAGE: 40,
  COINSURANCE: 50,
  LIMIT: 60,
};

let counter = 0;
const nextKey = () => `rule-${(counter += 1)}`;

export function newRow(type: EditableType): RuleRow {
  return {
    key: nextKey(),
    ruleType: type,
    ruleCode: "",
    categoryCode: "",
    clauseText: "",
    clauseRef: "",
    applyOrder: String(DEFAULT_ORDER[type]),
    ratePercent: "",
    limitAmount: "",
    limitScope: "",
    waitingDays: "",
  };
}

/** The eligibility rule is added by the system and has no tab, so it is not a row (US19, claims-api.yaml). */
export function fromRules(rules: BenefitRule[]): RuleRow[] {
  return rules
    .filter((rule): rule is BenefitRule & { ruleType: EditableType } => rule.ruleType !== "ELIGIBILITY")
    .map((rule) => ({
      key: nextKey(),
      ruleType: rule.ruleType,
      ruleCode: rule.ruleCode,
      categoryCode: rule.categoryCode ?? "",
      clauseText: rule.clauseText,
      clauseRef: rule.clauseRef ?? "",
      applyOrder: String(rule.applyOrder),
      ratePercent: rule.rateBp === null ? "" : String(rule.rateBp / 100),
      limitAmount: rule.limitAmount === null ? "" : String(rule.limitAmount),
      limitScope: rule.limitScope ?? "",
      waitingDays: rule.waitingDays === null ? "" : String(rule.waitingDays),
    }));
}

const whole = (text: string) => (text.trim() === "" ? undefined : Number(text.replaceAll(",", "")));

/** Sends what the person typed. The API checks it and names each problem by its place in the list. */
export function toInput(row: RuleRow): BenefitRuleInput {
  const percent = whole(row.ratePercent);
  return {
    ruleCode: row.ruleCode.trim(),
    ruleType: row.ruleType,
    clauseText: row.clauseText,
    applyOrder: whole(row.applyOrder) as number,
    ...(row.categoryCode && { categoryCode: row.categoryCode }),
    ...(row.clauseRef.trim() && { clauseRef: row.clauseRef.trim() }),
    ...(row.ruleType === "COINSURANCE" && percent !== undefined && { rateBp: Math.round(percent * 100) }),
    ...(row.ruleType === "LIMIT" && whole(row.limitAmount) !== undefined && { limitAmount: whole(row.limitAmount) }),
    ...(row.ruleType === "LIMIT" && row.limitScope && { limitScope: row.limitScope }),
    ...(row.ruleType === "WAITING_PERIOD" && whole(row.waitingDays) !== undefined && { waitingDays: whole(row.waitingDays) }),
  };
}

/** `rules[3].clauseText` becomes the message for that field of the fourth row. Other errors stay general. */
export function errorsByRow(rows: RuleRow[], errors: FieldError[]) {
  const byRow = new Map<string, Record<string, string>>();
  const general: string[] = [];

  for (const error of errors) {
    const match = /^rules\[(\d+)\]\.(\w+)$/.exec(error.field);
    const row = match ? rows[Number(match[1])] : undefined;
    if (row && match) {
      byRow.set(row.key, { ...byRow.get(row.key), [match[2]!]: error.message });
    } else {
      general.push(error.message);
    }
  }

  return { byRow, general };
}

/** Active categories that are neither covered nor excluded yet. Publishing refuses while any remain. */
export function unconfiguredCategories(rows: RuleRow[], categories: BenefitCategory[]): BenefitCategory[] {
  const configured = new Set(
    rows.filter((row) => row.ruleType === "COVERAGE" || row.ruleType === "EXCLUSION").map((row) => row.categoryCode),
  );
  return categories.filter((category) => !configured.has(category.code));
}
