"use client";

import type { components } from "@/lib/api/schema";
import { formatMoney } from "@/lib/format";
import { LIMIT_SCOPE_LABEL } from "@/lib/labels/rule-sets";
import type { EditableType, RuleRow } from "./rule-rows";

type BenefitCategory = components["schemas"]["BenefitCategory"];

type Column = {
  field: keyof RuleRow;
  /** The API's field name, for its error messages. */
  apiField: string;
  label: string;
  width: string;
  kind: "text" | "number" | "category" | "scope" | "textarea";
  optionalCategory?: boolean;
};

const CODE: Column = { field: "ruleCode", apiField: "ruleCode", label: "Rule code", width: "w-[120px]", kind: "text" };
const CLAUSE: Column = { field: "clauseText", apiField: "clauseText", label: "Clause text", width: "", kind: "textarea" };
const CLAUSE_REF: Column = { field: "clauseRef", apiField: "clauseRef", label: "Clause no.", width: "w-[96px]", kind: "text" };
const ORDER: Column = { field: "applyOrder", apiField: "applyOrder", label: "Order", width: "w-[72px]", kind: "number" };
const CATEGORY: Column = { field: "categoryCode", apiField: "categoryCode", label: "Category", width: "w-[200px]", kind: "category" };

const COLUMNS: Record<EditableType, Column[]> = {
  COVERAGE: [CODE, CATEGORY, CLAUSE, CLAUSE_REF, ORDER],
  EXCLUSION: [CODE, CATEGORY, CLAUSE, CLAUSE_REF, ORDER],
  WAITING_PERIOD: [
    CODE,
    { ...CATEGORY, optionalCategory: true },
    { field: "waitingDays", apiField: "waitingDays", label: "Days", width: "w-[80px]", kind: "number" },
    CLAUSE,
    CLAUSE_REF,
    ORDER,
  ],
  COINSURANCE: [
    CODE,
    { field: "ratePercent", apiField: "rateBp", label: "Insurer pays (%)", width: "w-[120px]", kind: "number" },
    CLAUSE,
    CLAUSE_REF,
    ORDER,
  ],
  LIMIT: [
    CODE,
    { field: "limitScope", apiField: "limitScope", label: "Scope", width: "w-[120px]", kind: "scope" },
    { field: "limitAmount", apiField: "limitAmount", label: "Limit (₫)", width: "w-[140px]", kind: "number" },
    CLAUSE,
    CLAUSE_REF,
    ORDER,
  ],
};

const inputClass =
  "h-8 w-full rounded-sm border border-control-border bg-surface px-2 text-[13px] aria-invalid:border-danger";

function display(row: RuleRow, column: Column, categories: BenefitCategory[]): string {
  const value = row[column.field];
  if (column.kind === "category") {
    return categories.find((category) => category.code === value)?.name ?? (value || "All categories");
  }
  if (column.kind === "scope") {
    return value ? LIMIT_SCOPE_LABEL[value as keyof typeof LIMIT_SCOPE_LABEL] : "—";
  }
  if (column.field === "limitAmount") {
    return value ? formatMoney(Number(value)) : "—";
  }
  return value || "—";
}

export function RuleTable({
  type,
  rows,
  categories,
  errors,
  editable,
  onChange,
  onRemove,
}: {
  type: EditableType;
  rows: RuleRow[];
  categories: BenefitCategory[];
  errors: Map<string, Record<string, string>>;
  editable: boolean;
  onChange: (key: string, field: keyof RuleRow, value: string) => void;
  onRemove: (key: string) => void;
}) {
  const columns = COLUMNS[type];

  if (rows.length === 0) {
    return <p className="rounded-sm border border-line bg-surface p-6 text-[13px]">No rules of this kind.</p>;
  }

  return (
    <table className="w-full border border-line bg-surface text-[13px]">
      <thead className="bg-background text-left font-mono text-[11px] tracking-[0.08em] text-on-surface-muted uppercase">
        <tr className="h-8">
          {columns.map((column) => (
            <th key={column.field} className={`px-3 font-medium ${column.width}`}>
              {column.label}
            </th>
          ))}
          {editable && <th className="w-[80px] px-3" aria-label="Actions" />}
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => {
          const rowErrors = errors.get(row.key) ?? {};
          return (
            <tr key={row.key} className="border-t border-line align-top" data-rule-code={row.ruleCode}>
              {columns.map((column) => {
                const error = rowErrors[column.apiField];
                const id = `${row.key}-${column.field}`;
                const label = `${column.label}, ${row.ruleCode || "new rule"}`;
                return (
                  <td key={column.field} className="px-3 py-2">
                    {!editable ? (
                      <span className={column.kind === "text" || column.kind === "number" ? "font-mono" : ""}>
                        {display(row, column, categories)}
                      </span>
                    ) : column.kind === "category" ? (
                      <select
                        id={id}
                        aria-label={label}
                        className={inputClass}
                        value={row.categoryCode}
                        aria-invalid={error ? true : undefined}
                        aria-describedby={error ? `${id}-error` : undefined}
                        onChange={(event) => onChange(row.key, column.field, event.target.value)}
                      >
                        <option value="">{column.optionalCategory ? "All categories" : "Choose a category"}</option>
                        {categories.map((category) => (
                          <option key={category.code} value={category.code}>
                            {category.name}
                          </option>
                        ))}
                      </select>
                    ) : column.kind === "scope" ? (
                      <select
                        id={id}
                        aria-label={label}
                        className={inputClass}
                        value={row.limitScope}
                        aria-invalid={error ? true : undefined}
                        aria-describedby={error ? `${id}-error` : undefined}
                        onChange={(event) => onChange(row.key, column.field, event.target.value)}
                      >
                        <option value="">Choose a scope</option>
                        <option value="PER_VISIT">{LIMIT_SCOPE_LABEL.PER_VISIT}</option>
                        <option value="ANNUAL">{LIMIT_SCOPE_LABEL.ANNUAL}</option>
                      </select>
                    ) : column.kind === "textarea" ? (
                      <textarea
                        id={id}
                        aria-label={label}
                        rows={2}
                        className={`${inputClass} h-auto py-1`}
                        value={row.clauseText}
                        aria-invalid={error ? true : undefined}
                        aria-describedby={error ? `${id}-error` : undefined}
                        onChange={(event) => onChange(row.key, column.field, event.target.value)}
                      />
                    ) : (
                      <input
                        id={id}
                        aria-label={label}
                        inputMode={column.kind === "number" ? "decimal" : undefined}
                        className={`${inputClass} ${column.kind === "number" || column.field === "ruleCode" ? "font-mono" : ""}`}
                        value={row[column.field]}
                        aria-invalid={error ? true : undefined}
                        aria-describedby={error ? `${id}-error` : undefined}
                        onChange={(event) => onChange(row.key, column.field, event.target.value)}
                      />
                    )}
                    {error && (
                      <p id={`${id}-error`} className="mt-1 text-xs text-danger">
                        {error}
                      </p>
                    )}
                  </td>
                );
              })}
              {editable && (
                <td className="px-3 py-2">
                  <button
                    type="button"
                    className="h-8 text-[13px] underline"
                    aria-label={`Remove ${row.ruleCode || "new rule"}`}
                    onClick={() => onRemove(row.key)}
                  >
                    Remove
                  </button>
                </td>
              )}
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
