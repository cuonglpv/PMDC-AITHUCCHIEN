import { describe, expect, it } from "vitest";
import { document as doc, page } from "@/test/intake";
import { autoAcceptedField, checkField, INVOICE_ID, missingField, REGION, resolved, SUMMARY_ID, PAGE_1 } from "@/test/workspace";
import {
  conflictValue,
  displayValue,
  groupByDocument,
  hasAiValue,
  hasSource,
  initialEditorText,
  nextFieldId,
  readEditedValue,
} from "./fields";

describe("what a field shows", () => {
  it("shows an AI value not yet accepted exactly as it was read", () => {
    expect(displayValue(checkField({ ai: { ...checkField().ai!, value: "1.400.000 đ" } }))).toBe("1.400.000 đ");
  });

  it("formats a value in use by its kind", () => {
    expect(displayValue(checkField({ valueAmount: 1_400_000, valueText: "1.400.000 đ" }))).toBe("1,400,000 ₫");
    expect(displayValue(autoAcceptedField())).toBe("12/10/2026");
  });

  it("never shows an AI value at level Missing, even if one were sent", () => {
    const sent = missingField({ ai: { ...missingField().ai!, value: "Viêm họng" } });
    expect(displayValue(sent)).toBeNull();
    expect(hasAiValue(sent)).toBe(false);
  });
});

describe("the source", () => {
  it("uses the AI's until a person draws one", () => {
    expect(hasSource(checkField())).toBe(true);
    expect(hasSource(checkField({ ai: { ...checkField().ai!, pageId: null, bbox: null } }))).toBe(false);
    expect(hasSource(missingField({ sourcePageId: PAGE_1, sourceBbox: REGION }))).toBe(true);
  });

  it("needs a box as well as a page", () => {
    expect(hasSource(checkField({ ai: { ...checkField().ai!, pageId: PAGE_1, bbox: null } }))).toBe(false);
  });
});

describe("the editor", () => {
  it("starts money from the AI's reading in a form it can read back", () => {
    expect(initialEditorText(checkField({ ai: { ...checkField().ai!, value: "1.400.000" } }))).toBe("1,400,000");
    expect(initialEditorText(checkField({ valueAmount: 800_000 }))).toBe("800,000");
  });

  it("leaves an unreadable AI reading as it was, for the person to fix", () => {
    expect(initialEditorText(checkField({ ai: { ...checkField().ai!, value: "1.4 trieu" } }))).toBe("1.4 trieu");
  });

  it("starts dates as dd/MM/yyyy", () => {
    expect(initialEditorText(autoAcceptedField())).toBe("12/10/2026");
    const iso = checkField({ valueKind: "DATE", ai: { ...checkField().ai!, value: "2026-10-12" } });
    expect(initialEditorText(iso)).toBe("12/10/2026");
  });

  it("never starts from a masked ID number, which would save the mask", () => {
    expect(initialEditorText(checkField({ fieldKey: "national_id", valueKind: "TEXT", valueText: "********4821" }))).toBe("");
  });

  it("reads money as whole VND", () => {
    expect(readEditedValue(checkField(), "1,400,000")).toEqual({ ok: true, change: { valueAmount: 1_400_000 } });
    expect(readEditedValue(checkField(), "1400000 ₫")).toEqual({ ok: true, change: { valueAmount: 1_400_000 } });
    expect(readEditedValue(checkField(), "1400000.5").ok).toBe(false);
  });

  it("reads a date as dd/MM/yyyy into the contract's date", () => {
    const field = autoAcceptedField();
    expect(readEditedValue(field, "12/10/2026")).toEqual({ ok: true, change: { valueDate: "2026-10-12" } });
    expect(readEditedValue(field, "31/02/2026").ok).toBe(false);
  });

  it("sends a number as text, as the contract says", () => {
    const field = checkField({ valueKind: "NUMBER", fieldKey: "line_quantity" });
    expect(readEditedValue(field, "1.5")).toEqual({ ok: true, change: { valueText: "1.5" } });
    expect(readEditedValue(field, "two").ok).toBe(false);
  });

  it("refuses an empty value", () => {
    expect(readEditedValue(missingField(), "   ").ok).toBe(false);
  });
});

describe("a conflict", () => {
  it("reads the current value from details by what it holds", () => {
    expect(conflictValue({ current: { valueAmount: 900_000 } })).toBe("900,000 ₫");
    expect(conflictValue({ current: { valueDate: "2026-10-12" } })).toBe("12/10/2026");
    expect(conflictValue({ current: { valueText: "Viêm họng" } })).toBe("Viêm họng");
    expect(conflictValue({ current: { status: "NOT_PRESENT" } })).toBe("Not present");
  });

  it("says nothing when details say nothing", () => {
    expect(conflictValue(undefined)).toBeNull();
    expect(conflictValue({ current: {} })).toBeNull();
  });
});

describe("the next field", () => {
  const a = checkField({ id: "a" });
  const b = autoAcceptedField({ id: "b" });
  const c = missingField({ id: "c" });
  const d = resolved(checkField({ id: "d" }));

  it("skips what is already resolved and wraps round", () => {
    expect(nextFieldId([a, b, c, d], "a")).toBe("c");
    expect(nextFieldId([a, b, c, d], "c")).toBe("a");
  });

  it("goes to the next row once nothing is left", () => {
    expect(nextFieldId([b, d], "b")).toBe("d");
    expect(nextFieldId([b, d], "d")).toBe("b");
  });

  it("has nowhere to go in an empty list", () => {
    expect(nextFieldId([], null)).toBeNull();
  });
});

describe("grouping", () => {
  const invoice = doc({ id: INVOICE_ID, pages: [page({ documentId: INVOICE_ID })] });
  const summary = doc({ id: SUMMARY_ID, pages: [page({ documentId: SUMMARY_ID })] });

  it("keeps the API's order: documents by their first field, fields as they came", () => {
    const first = missingField({ id: "1", documentId: SUMMARY_ID });
    const second = checkField({ id: "2", documentId: INVOICE_ID });
    const third = autoAcceptedField({ id: "3", documentId: SUMMARY_ID });

    const groups = groupByDocument([first, second, third], [invoice, summary]);

    expect(groups.map((group) => group.documentId)).toEqual([SUMMARY_ID, INVOICE_ID]);
    expect(groups[0]?.fields.map((field) => field.id)).toEqual(["1", "3"]);
  });

  it("lists a ready document with no fields after the others", () => {
    const groups = groupByDocument([checkField({ documentId: INVOICE_ID })], [summary, invoice]);

    expect(groups.map((group) => [group.documentId, group.fields.length])).toEqual([
      [INVOICE_ID, 1],
      [SUMMARY_ID, 0],
    ]);
  });
});
