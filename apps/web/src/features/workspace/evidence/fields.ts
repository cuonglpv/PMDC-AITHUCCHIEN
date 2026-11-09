import type { ClaimField, Document, VersionConflictDetails } from "@/lib/api/types";
import { formatDate, formatMoney, parseDate, parseMoney } from "@/lib/format";
import { DATE_INVALID, MONEY_INVALID, NUMBER_INVALID, VALUE_REQUIRED } from "@/lib/labels/fields";
import type { PageRegion } from "../queries";

const WHOLE_VND = new Intl.NumberFormat("en-US", { maximumFractionDigits: 0 });

/** The source in use: the one a person drew, or the AI's while nobody has changed it. */
export function fieldSource(field: ClaimField): { pageId: string | null; bbox: ClaimField["sourceBbox"] } {
  if (field.sourcePageId) {
    return { pageId: field.sourcePageId, bbox: field.sourceBbox };
  }
  return { pageId: field.ai?.pageId ?? null, bbox: field.ai?.bbox ?? null };
}

/** Whether the value can stand as it is (BR-02). The API's 422 `SOURCE_REQUIRED` stays the real check. */
export function hasSource(field: ClaimField): boolean {
  const { pageId, bbox } = fieldSource(field);
  return pageId !== null && bbox !== null;
}

/** AI has a value to accept. A level MISSING field never shows one (US05). */
export function hasAiValue(field: ClaimField): boolean {
  return field.ai !== null && field.ai.level !== "MISSING" && field.ai.value !== null;
}

/** What the value columns hold, as a person reads it; null when none is set. */
function typedValue(field: Pick<ClaimField, "valueAmount" | "valueDate" | "valueText">): string | null {
  if (field.valueAmount !== null && field.valueAmount !== undefined) {
    return formatMoney(field.valueAmount);
  }
  if (field.valueDate) {
    return formatDate(field.valueDate);
  }
  return field.valueText ?? null;
}

/**
 * The value on the row. A value in use is shown formatted; an AI value not yet accepted lives only in `ai.value` and is
 * shown exactly as it was read, because the person checks it against the page. Null means there is no value to show.
 */
export function displayValue(field: ClaimField): string | null {
  return typedValue(field) ?? (hasAiValue(field) ? field.ai!.value : null);
}

/** A money or date value in the form the editor reads, or the raw text when it cannot be read as one. */
function editableAiValue(field: ClaimField, value: string): string {
  if (field.valueKind === "MONEY") {
    const amount = parseMoney(value);
    return amount === null ? value : WHOLE_VND.format(amount);
  }
  if (field.valueKind === "DATE" && /^\d{4}-\d{2}-\d{2}/.test(value)) {
    return formatDate(value);
  }
  return value;
}

/** What the editor starts with. */
export function initialEditorText(field: ClaimField): string {
  // The ID number comes back masked (docs/07-api-contract.md), so starting from it would save the mask as the value.
  if (field.fieldKey === "national_id") {
    return "";
  }
  if (field.valueAmount !== null) {
    return WHOLE_VND.format(field.valueAmount);
  }
  if (field.valueDate) {
    return formatDate(field.valueDate);
  }
  if (field.valueText) {
    return field.valueText;
  }
  return hasAiValue(field) ? editableAiValue(field, field.ai!.value!) : "";
}

export type FieldValueChange = { valueText?: string; valueAmount?: number; valueDate?: string };
export type EditedValue = { ok: true; change: FieldValueChange } | { ok: false; message: string };

/** Reads the editor's text by the field's kind. Format only: the API owns every business rule. */
export function readEditedValue(field: ClaimField, text: string): EditedValue {
  const typed = text.trim();
  if (!typed) {
    return { ok: false, message: VALUE_REQUIRED };
  }
  switch (field.valueKind) {
    case "MONEY": {
      const amount = parseMoney(typed);
      return amount === null ? { ok: false, message: MONEY_INVALID } : { ok: true, change: { valueAmount: amount } };
    }
    case "DATE": {
      const date = parseDate(typed);
      return date === null ? { ok: false, message: DATE_INVALID } : { ok: true, change: { valueDate: date } };
    }
    case "NUMBER":
      return /^\d+(\.\d+)?$/.test(typed) ? { ok: true, change: { valueText: typed } } : { ok: false, message: NUMBER_INVALID };
    default:
      return { ok: true, change: { valueText: typed } };
  }
}

/** What `details.current` of a 409 `VERSION_CONFLICT` says the field holds now, ready to show; null when it says nothing. */
export function conflictValue(details: unknown): string | null {
  if (!details || typeof details !== "object" || !("current" in details)) {
    return null;
  }
  const current = (details as VersionConflictDetails).current as Partial<ClaimField> | null;
  if (!current) {
    return null;
  }
  if (current.status === "NOT_PRESENT") {
    return "Not present";
  }
  return typedValue({
    valueAmount: current.valueAmount ?? null,
    valueDate: current.valueDate ?? null,
    valueText: current.valueText ?? null,
  });
}

/** A field a person still has to look at; `AUTO_ACCEPTED` went through on its level and stays in the list with its marker. */
export function isUnresolved(field: ClaimField): boolean {
  return field.status === "NEEDS_VERIFICATION" || field.status === "NEEDS_ENTRY";
}

/** N: the next unresolved field after this one, wrapping round; once none is left, simply the next row. */
export function nextFieldId(items: ClaimField[], currentId: string | null): string | null {
  if (items.length === 0) {
    return null;
  }
  const from = items.findIndex((item) => item.id === currentId);
  const rotated = [...items.slice(from + 1), ...items.slice(0, from + 1)];
  return (rotated.find(isUnresolved) ?? rotated[0])?.id ?? null;
}

export type FieldGroup = { documentId: string; document: Document | undefined; fields: ClaimField[] };

/**
 * Fields by document. The order is the API's, lowest confidence first: documents by their first field, fields as they
 * came. The web never sorts by confidence. Documents with no field yet follow, so a page chosen by hand can start them.
 */
export function groupByDocument(items: ClaimField[], documents: Document[]): FieldGroup[] {
  const groups = new Map<string, FieldGroup>();
  const documentOf = new Map(documents.map((document) => [document.id, document]));
  for (const field of items) {
    const group = groups.get(field.documentId);
    if (group) {
      group.fields.push(field);
    } else {
      groups.set(field.documentId, { documentId: field.documentId, document: documentOf.get(field.documentId), fields: [field] });
    }
  }
  for (const document of documents) {
    if (!groups.has(document.id) && document.status === "READY") {
      groups.set(document.id, { documentId: document.id, document, fields: [] });
    }
  }
  return [...groups.values()];
}

/** The work on one field that survives moving to another row and back: what was typed and what was drawn. */
export type FieldDraft = { editing: boolean; text: string; drawn: PageRegion | null };

export const EMPTY_DRAFT: FieldDraft = { editing: false, text: "", drawn: null };
