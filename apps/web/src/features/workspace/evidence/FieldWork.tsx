"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { ConflictNotice } from "@/components/states/ConflictNotice";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { ApiProblem } from "@/lib/api/problem";
import type { ClaimField } from "@/lib/api/types";
import { EVIDENCE_PROBLEM_LABEL, FIELD_NOT_SAVED, fieldLabel, LINE_NOT_SAVED } from "@/lib/labels/fields";
import { useWorkspace } from "../shell/workspace-context";
import { useAcceptField, useRemoveExpenseLine, useUpdateField, workspaceKeys } from "../queries";
import { FieldEditor } from "./FieldEditor";
import { conflictValue, hasAiValue, hasSource, initialEditorText, type FieldDraft, type FieldValueChange } from "./fields";
import { isShortcut } from "./shortcut-keys";

type Problem = { kind: "message"; text: string } | { kind: "conflict"; current: string | null };

const SOURCE_REQUIRED = EVIDENCE_PROBLEM_LABEL.SOURCE_REQUIRED!;
const NO_AI_VALUE = EVIDENCE_PROBLEM_LABEL.FIELD_HAS_NO_AI_VALUE!;

/**
 * The actions on the selected field, shown only when `allowedActions` has EDIT_FIELDS: Accept (A), Edit (E), Not present,
 * Remove line. A refusal stays next to the field. `SOURCE_REQUIRED` opens the drawing tool; the person draws, then uses
 * the same button again. A conflict keeps what was typed and shows what the server holds now.
 */
export function FieldWork({
  field,
  draft,
  onDraft,
  onClearDraft,
  onReturnFocus,
}: {
  field: ClaimField;
  draft: FieldDraft;
  onDraft: (patch: Partial<FieldDraft>) => void;
  onClearDraft: () => void;
  onReturnFocus: () => void;
}) {
  const { claimId, pages, startDrawing, drawing } = useWorkspace();
  const queryClient = useQueryClient();
  const accept = useAcceptField(claimId);
  const update = useUpdateField(claimId);
  const removeLine = useRemoveExpenseLine(claimId);
  const [problem, setProblem] = useState<Problem | null>(null);
  const [confirmRemove, setConfirmRemove] = useState(false);
  const name = fieldLabel(field);
  const busy = accept.isPending || update.isPending;
  const sourced = hasSource(field) || draft.drawn !== null;

  const askToDraw = () => {
    startDrawing({
      fieldName: name,
      onDrawn: (region) => {
        setProblem(null);
        onDraft({ drawn: region });
      },
    });
  };

  const needsDrawing = () => {
    setProblem({ kind: "message", text: SOURCE_REQUIRED });
    askToDraw();
  };

  const onRefused = (error: unknown) => {
    if (!(error instanceof ApiProblem)) {
      setProblem({ kind: "message", text: FIELD_NOT_SAVED });
    } else if (error.code === "VERSION_CONFLICT") {
      setProblem({ kind: "conflict", current: conflictValue(error.problem?.details) });
    } else if (error.code === "SOURCE_REQUIRED") {
      needsDrawing();
    } else if (error.code === "VALIDATION_FAILED") {
      setProblem({ kind: "message", text: error.errors[0]?.message ?? FIELD_NOT_SAVED });
    } else {
      setProblem({ kind: "message", text: (error.code && EVIDENCE_PROBLEM_LABEL[error.code]) || FIELD_NOT_SAVED });
    }
  };

  const onSaved = () => {
    setProblem(null);
    onClearDraft();
    onReturnFocus();
  };

  const doAccept = () => {
    if (busy) {
      return;
    }
    if (!hasAiValue(field)) {
      setProblem({ kind: "message", text: NO_AI_VALUE });
    } else if (!sourced) {
      needsDrawing();
    } else {
      setProblem(null);
      accept.mutate({ field, region: draft.drawn }, { onSuccess: onSaved, onError: onRefused });
    }
  };

  const doEdit = () => {
    setProblem(null);
    onDraft({ editing: true, text: draft.editing ? draft.text : initialEditorText(field) });
  };

  const doSave = (change: FieldValueChange) => {
    if (!sourced) {
      needsDrawing();
      return;
    }
    setProblem(null);
    update.mutate(
      { field, change: { action: "SET_VALUE", ...change }, region: draft.drawn },
      { onSuccess: onSaved, onError: onRefused },
    );
  };

  const doNotPresent = () => {
    setProblem(null);
    update.mutate({ field, change: { action: "NOT_PRESENT" }, region: null }, { onSuccess: onSaved, onError: onRefused });
  };

  const reload = () => {
    setProblem(null);
    void queryClient.invalidateQueries({ queryKey: workspaceKeys.fields(claimId) });
  };

  // Re-subscribes each render, so a key always acts on the field and state on screen.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (!isShortcut(event) || drawing || draft.editing) {
        return;
      }
      if (event.key === "a" || event.key === "A") {
        event.preventDefault();
        doAccept();
      } else if (event.key === "e" || event.key === "E") {
        event.preventDefault();
        doEdit();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  });

  const drawnPageNo = draft.drawn ? pages.find((page) => page.id === draft.drawn?.pageId)?.claimPageNo : undefined;
  // An ID number is masked in the list, so its editor starts empty even though a value exists.
  const hasValueToEdit = field.fieldKey === "national_id" ? field.valueText !== null : initialEditorText(field) !== "";

  return (
    <div className="flex flex-col gap-2 px-3 pb-3">
      {draft.editing ? (
        <FieldEditor
          field={field}
          text={draft.text}
          saving={update.isPending}
          onText={(text) => onDraft({ text })}
          onSave={doSave}
          onCancel={() => {
            setProblem(null);
            onDraft({ editing: false });
            onReturnFocus();
          }}
        />
      ) : (
        <div className="flex flex-wrap items-center gap-2">
          {field.resolvedBy === null && hasAiValue(field) && (
            <Button className="h-8" title="Accept (A)" aria-keyshortcuts="A" disabled={busy} onClick={doAccept}>
              Accept
            </Button>
          )}
          <Button className="h-8" title="Edit (E)" aria-keyshortcuts="E" disabled={busy} onClick={doEdit}>
            {hasValueToEdit ? "Edit" : "Enter value"}
          </Button>
          <Button variant="ghost" className="h-8" disabled={busy} onClick={doNotPresent}>
            Not present
          </Button>
          {field.expenseLineId && (
            <Button variant="ghost" className="ml-auto h-8" onClick={() => setConfirmRemove(true)}>
              Remove line
            </Button>
          )}
        </div>
      )}

      {(draft.editing || !hasSource(field)) && (
        <div className="flex flex-wrap items-center gap-2 text-xs text-on-surface-muted">
          <span>
            {draft.drawn
              ? `Source: region drawn on p${drawnPageNo ?? "?"}`
              : hasSource(field)
                ? "Source: kept from the page"
                : "No source yet. A value needs one."}
          </span>
          <Button variant="ghost" className="h-7 px-2 text-xs" disabled={drawing !== null} onClick={askToDraw}>
            {draft.drawn || hasSource(field) ? "Redraw region" : "Draw region"}
          </Button>
        </div>
      )}

      {problem?.kind === "message" && (
        <p role="alert" className="text-[13px] text-danger">
          {problem.text}
        </p>
      )}
      {problem?.kind === "conflict" && <ConflictNotice current={problem.current} onReload={reload} />}

      {field.expenseLineId && (
        <RemoveLineDialog
          open={confirmRemove}
          lineNo={field.itemNo}
          pending={removeLine.isPending}
          error={
            removeLine.isError
              ? (removeLine.error instanceof ApiProblem &&
                  removeLine.error.code &&
                  EVIDENCE_PROBLEM_LABEL[removeLine.error.code]) ||
                LINE_NOT_SAVED
              : null
          }
          onCancel={() => setConfirmRemove(false)}
          onConfirm={() => removeLine.mutate(field.expenseLineId!, { onSuccess: () => setConfirmRemove(false) })}
        />
      )}
    </div>
  );
}

function RemoveLineDialog({
  open,
  lineNo,
  pending,
  error,
  onCancel,
  onConfirm,
}: {
  open: boolean;
  lineNo: number;
  pending: boolean;
  error: string | null;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) {
          onCancel();
        }
      }}
      title={`Remove line ${lineNo}?`}
      description="Its fields leave this claim and the removal is recorded with your name."
    >
      {error && (
        <p role="alert" className="mb-3 text-[13px] text-danger">
          {error}
        </p>
      )}
      <div className="flex justify-end gap-2">
        <Button onClick={onCancel}>Keep line</Button>
        <Button variant="danger" disabled={pending} onClick={onConfirm}>
          Remove line
        </Button>
      </div>
    </Dialog>
  );
}
