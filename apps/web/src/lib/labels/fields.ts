import type { ClaimField, ErrorCode, FieldKey } from "@/lib/api/types";

/** The US05 field catalogue, as the Evidence tab names it. */
export const FIELD_KEY_LABEL: Record<FieldKey, string> = {
  insured_name: "Insured name",
  national_id: "ID number",
  policy_number: "Policy number",
  provider_name: "Provider",
  provider_tax_code: "Provider tax code",
  service_date: "Service date",
  invoice_number: "Invoice number",
  invoice_total: "Invoice total",
  line_description: "description",
  line_quantity: "quantity",
  line_unit_price: "unit price",
  line_amount: "amount",
  diagnosis: "Diagnosis",
  medication: "Medication",
};

/** "Invoice total", "Line 4 description", "Medication 2". Item 0 is the document header. */
export function fieldLabel(field: Pick<ClaimField, "fieldKey" | "itemNo">): string {
  if (field.fieldKey.startsWith("line_")) {
    return `Line ${field.itemNo} ${FIELD_KEY_LABEL[field.fieldKey]}`;
  }
  return field.itemNo > 0 ? `${FIELD_KEY_LABEL[field.fieldKey]} ${field.itemNo}` : FIELD_KEY_LABEL[field.fieldKey];
}

/** Why a change to a field or page was refused, by the code the API sends, shown next to it. */
export const EVIDENCE_PROBLEM_LABEL: Partial<Record<ErrorCode, string>> = {
  SOURCE_REQUIRED: "This value has no source on the page. Draw the region it came from, then try again.",
  FIELD_HAS_NO_AI_VALUE: "AI has no value to accept for this field. Enter it by hand.",
  CLAIM_STATE_CONFLICT: "This claim has moved on and can no longer change here.",
  NOT_FOUND: "This item no longer exists. It may have been removed already.",
  CLAIM_OUT_OF_SCOPE: "This claim is no longer assigned to you.",
};

export const FIELD_NOT_SAVED = "The change could not be saved. Please try again.";
export const LINE_NOT_SAVED = "The expense line could not be changed. Please try again.";
export const RERUN_NOT_STARTED = "AI could not be started on this page. Please try again.";

export const VALUE_REQUIRED = "Enter a value.";
export const MONEY_INVALID = "Enter a whole amount in VND, such as 1,400,000.";
export const DATE_INVALID = "Enter the date as dd/mm/yyyy, such as 12/10/2026.";
export const NUMBER_INVALID = "Enter a number, such as 2 or 1.5.";
