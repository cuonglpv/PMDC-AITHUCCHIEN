"use client";

import { useEffect, useId, useRef, useState, type FormEvent, type KeyboardEvent } from "react";
import { Button } from "@/components/ui/Button";
import type { ClaimField } from "@/lib/api/types";
import { fieldLabel } from "@/lib/labels/fields";
import { readEditedValue, type FieldValueChange } from "./fields";

const UNIT: Partial<Record<ClaimField["valueKind"], string>> = { MONEY: " (₫)" };
const PLACEHOLDER: Partial<Record<ClaimField["valueKind"], string>> = { MONEY: "1,400,000", DATE: "dd/mm/yyyy" };

/**
 * The value input of one field, by its kind: money as whole VND and dates typed dd/MM/yyyy, as on New claim. A format
 * problem stays next to the field and focus returns to the input. The text lives with the caller, so it survives a
 * conflict or moving to another row.
 */
export function FieldEditor({
  field,
  text,
  saving,
  onText,
  onSave,
  onCancel,
}: {
  field: ClaimField;
  text: string;
  saving: boolean;
  onText: (text: string) => void;
  onSave: (change: FieldValueChange) => void;
  onCancel: () => void;
}) {
  const id = useId();
  const input = useRef<HTMLInputElement>(null);
  const [formatError, setFormatError] = useState<string | null>(null);

  useEffect(() => {
    input.current?.focus();
  }, []);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const value = readEditedValue(field, text);
    if (value.ok) {
      setFormatError(null);
      onSave(value.change);
    } else {
      setFormatError(value.message);
      input.current?.focus();
    }
  };

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === "Escape") {
      event.preventDefault();
      onCancel();
    }
  };

  return (
    <form onSubmit={submit} onKeyDown={onKeyDown} noValidate className="flex flex-col gap-1">
      <label htmlFor={id} className="text-[13px] font-medium">
        {fieldLabel(field)}
        {UNIT[field.valueKind]}
      </label>
      <div className="flex items-center gap-2">
        <input
          ref={input}
          id={id}
          value={text}
          placeholder={PLACEHOLDER[field.valueKind]}
          inputMode={field.valueKind === "MONEY" || field.valueKind === "NUMBER" ? "decimal" : undefined}
          autoComplete="off"
          aria-invalid={formatError ? true : undefined}
          aria-describedby={formatError ? `${id}-error` : undefined}
          onChange={(event) => {
            onText(event.target.value);
            setFormatError(null);
          }}
          className={`h-9 min-w-0 flex-1 rounded-sm border bg-surface px-3 text-sm ${
            field.valueKind === "MONEY" || field.valueKind === "DATE" ? "font-mono" : ""
          } ${formatError ? "border-danger" : "border-control-border"}`}
        />
        <Button type="submit" disabled={saving}>
          Save
        </Button>
        <Button variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
      </div>
      {formatError && (
        <p id={`${id}-error`} className="text-xs text-danger">
          {formatError}
        </p>
      )}
    </form>
  );
}
