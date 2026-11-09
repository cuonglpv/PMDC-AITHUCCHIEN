"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { AiMarker } from "@/components/ai/AiMarker";
import { ConfidenceBadge } from "@/components/ai/ConfidenceBadge";
import { PersonMarker } from "@/components/ai/PersonMarker";
import { SourcePin } from "@/components/ai/SourcePin";
import { EmptyState } from "@/components/states/EmptyState";
import { LoadError, Loading } from "@/components/states/PageState";
import { Button } from "@/components/ui/Button";
import { ApiProblem } from "@/lib/api/problem";
import type { ClaimField, FieldList } from "@/lib/api/types";
import { fieldsToVerifyLabel } from "@/lib/labels/claims";
import { PAGE_TYPE_LABEL } from "@/lib/labels/documents";
import { EVIDENCE_PROBLEM_LABEL, fieldLabel, LINE_NOT_SAVED } from "@/lib/labels/fields";
import { documentType } from "../shell/DocumentRail";
import { useWorkspace, type SourceHighlight } from "../shell/workspace-context";
import { useAddExpenseLine, useFields, workspaceKeys } from "../queries";
import {
  displayValue,
  EMPTY_DRAFT,
  fieldSource,
  groupByDocument,
  nextFieldId,
  type FieldDraft,
  type FieldGroup,
} from "./fields";
import { FieldWork } from "./FieldWork";
import { PageStrip } from "./PageStrip";
import { isShortcut } from "./shortcut-keys";

/** What zone C shows for a field: where its source is, or else the first page of its document. */
type SourceTarget = { pageId: string | null; highlight: SourceHighlight | null };

/**
 * SCR-31 (US06): fields by document in the API's order, lowest confidence first, each beside its source. The web renders
 * `level`, `aiMarker` and `allowedActions`, and never compares a confidence with a threshold.
 */
export function EvidencePage() {
  const { claimId, mustStartReview } = useWorkspace();
  const fields = useFields(claimId, !mustStartReview);

  if (mustStartReview) {
    return (
      <div className="p-4">
        <EmptyState
          icon="assignment_ind"
          title="This claim is not assigned yet"
          description="Start review to take the claim and see its fields next to the pages."
        />
      </div>
    );
  }
  if (fields.isPending) {
    return <Loading label="Loading the fields" />;
  }
  if (fields.isError) {
    return <LoadError error={fields.error} />;
  }
  return <FieldsPanel list={fields.data} />;
}

function FieldsPanel({ list }: { list: FieldList }) {
  const { claimId, canEdit, documents, viewedPage, showSource, stopDrawing, drawing, verifyNextRef } = useWorkspace();
  const queryClient = useQueryClient();
  const addLine = useAddExpenseLine(claimId);
  const { items, remainingToVerify } = list;
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [drafts, setDrafts] = useState<Record<string, FieldDraft>>({});
  const rows = useRef(new Map<string, HTMLButtonElement>());

  const selected = items.find((item) => item.id === selectedId) ?? items[0] ?? null;
  const groups = useMemo(() => groupByDocument(items, documents), [items, documents]);
  // What the reviewer sees, top to bottom: N and the arrow keys walk this, not the list as it came.
  const shown = useMemo(() => groups.flatMap((group) => group.fields), [groups]);
  const pageNoOf = useMemo(
    () => new Map(documents.flatMap((document) => document.pages.map((page) => [page.id, page.claimPageNo]))),
    [documents],
  );

  const targetFor = useCallback(
    (field: ClaimField, drawn: FieldDraft["drawn"]): SourceTarget => {
      if (drawn) {
        return { pageId: drawn.pageId, highlight: { ...drawn, label: "Drawn region", tone: "person" } };
      }
      const { pageId, bbox } = fieldSource(field);
      if (pageId && bbox) {
        const fromAi = field.aiMarker && field.ai;
        // DESIGN.md source highlight tag: the field and its score, as the API sent it.
        const label = fromAi ? `${field.fieldKey} · ${field.ai!.confidence.toFixed(2)}` : fieldLabel(field);
        return { pageId, highlight: { pageId, bbox, label, tone: fromAi ? "ai" : "person" } };
      }
      const document = documents.find((item) => item.id === field.documentId);
      return { pageId: pageId ?? document?.pages[0]?.id ?? null, highlight: null };
    },
    [documents],
  );

  // The linking rule: zone C follows the selected field, its source and anything drawn for it. Serialised, so it moves
  // only when the target really changes, not each time the page reference does.
  const target = selected ? JSON.stringify(targetFor(selected, drafts[selected.id]?.drawn ?? null)) : null;
  useEffect(() => {
    const next: SourceTarget | null = target ? JSON.parse(target) : null;
    showSource(next?.pageId ?? null, next?.highlight ?? null);
  }, [target, showSource]);

  const select = (field: ClaimField) => {
    stopDrawing();
    setSelectedId(field.id);
    // The reviewer may have paged away in the viewer: selecting the same field again goes back to its source.
    const next = targetFor(field, drafts[field.id]?.drawn ?? null);
    showSource(next.pageId, next.highlight);
  };

  const selectAndFocus = (field: ClaimField | undefined) => {
    if (field) {
      select(field);
      rows.current.get(field.id)?.focus();
    }
  };

  const goNext = () => selectAndFocus(shown.find((item) => item.id === nextFieldId(shown, selected?.id ?? null)));

  useEffect(() => {
    verifyNextRef.current = goNext;
    return () => {
      verifyNextRef.current = null;
    };
  });

  // Re-subscribes each render, so a key always acts on the selection on screen.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (!isShortcut(event) || drawing) {
        return;
      }
      const at = shown.findIndex((item) => item.id === selected?.id);
      if (event.key === "n" || event.key === "N") {
        event.preventDefault();
        goNext();
      } else if (event.key === "ArrowDown") {
        event.preventDefault();
        selectAndFocus(shown[at + 1]);
      } else if (event.key === "ArrowUp") {
        event.preventDefault();
        selectAndFocus(shown[at - 1]);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  });

  const updateDraft = (fieldId: string, patch: Partial<FieldDraft>) =>
    setDrafts((current) => ({ ...current, [fieldId]: { ...(current[fieldId] ?? EMPTY_DRAFT), ...patch } }));

  const clearDraft = (fieldId: string) =>
    setDrafts((current) => {
      const next = { ...current };
      delete next[fieldId];
      return next;
    });

  const addLineTo = (documentId: string) =>
    addLine.mutate(documentId, {
      onSuccess: ({ line }) => {
        // The fields have reloaded by now, so the first empty slot of the new line can be selected for entry.
        const reloaded = queryClient.getQueryData<FieldList>(workspaceKeys.fields(claimId));
        const first = reloaded?.items.find((item) => item.expenseLineId === line.id);
        if (first) {
          setSelectedId(first.id);
        }
      },
    });

  const addLineProblem =
    addLine.error instanceof ApiProblem && addLine.error.code ? EVIDENCE_PROBLEM_LABEL[addLine.error.code] : undefined;

  return (
    <div>
      <div className="flex items-baseline justify-between gap-3 border-b border-line bg-surface px-4 py-3">
        <div>
          <p className="font-mono text-[11px] font-medium tracking-[0.08em] text-primary-text uppercase">01 / Evidence</p>
          <h2 className="mt-0.5 font-display text-sm font-medium uppercase">Fields</h2>
        </div>
        <p className="text-[13px] font-semibold" data-testid="fields-counter">
          {fieldsToVerifyLabel(remainingToVerify)}
        </p>
      </div>

      {viewedPage && <PageStrip key={viewedPage.id} page={viewedPage} />}

      {addLine.isError && (
        <p role="alert" className="border-b border-line bg-surface px-4 py-2 text-[13px] text-danger">
          {addLineProblem ?? LINE_NOT_SAVED}
        </p>
      )}

      <div className="space-y-4 p-4">
        {groups.length === 0 && (
          <EmptyState
            icon="description"
            title="No fields yet"
            description="Fields appear here once a page has a type. Choose one in the document rail."
          />
        )}
        {groups.map((group) => (
          <section key={group.documentId} aria-label={groupName(group)} className="border border-line bg-surface">
            <div className="flex min-h-10 items-center justify-between gap-3 border-b border-line bg-background px-3 py-1">
              <h3 className="min-w-0 truncate text-[13px] font-semibold">
                {groupName(group)}
                {group.document && (
                  <span className="ml-2 font-normal text-on-surface-muted">{group.document.originalFilename}</span>
                )}
              </h3>
              {canEdit && group.document && documentType(group.document) === "INVOICE" && (
                <Button
                  variant="ghost"
                  className="h-7 shrink-0 px-2 text-xs"
                  disabled={addLine.isPending}
                  onClick={() => addLineTo(group.documentId)}
                >
                  <span className="material-symbols-outlined text-[15px]" aria-hidden="true">
                    add
                  </span>
                  Add expense line
                </Button>
              )}
            </div>
            {group.fields.length === 0 ? (
              <p className="px-3 py-3 text-[13px] text-on-surface-muted">
                {group.document?.pages.some((page) => page.pageType === "UNCLASSIFIED")
                  ? "No fields yet. Choose a type for its pages in the document rail."
                  : "No fields on this document."}
              </p>
            ) : (
              <ul>
                {group.fields.map((field) => (
                  <FieldRow
                    key={field.id}
                    field={field}
                    selected={field.id === selected?.id}
                    draft={drafts[field.id] ?? EMPTY_DRAFT}
                    pageNo={pageNoFor(field, pageNoOf)}
                    canEdit={canEdit}
                    buttonRef={(element) => {
                      if (element) {
                        rows.current.set(field.id, element);
                      } else {
                        rows.current.delete(field.id);
                      }
                    }}
                    onSelect={() => select(field)}
                    onDraft={(patch) => updateDraft(field.id, patch)}
                    onClearDraft={() => clearDraft(field.id)}
                    onReturnFocus={() => rows.current.get(field.id)?.focus()}
                  />
                ))}
              </ul>
            )}
          </section>
        ))}
      </div>
    </div>
  );
}

function groupName(group: FieldGroup): string {
  return group.document ? PAGE_TYPE_LABEL[documentType(group.document)] : "Document";
}

function pageNoFor(field: ClaimField, pageNoOf: Map<string, number>): number | null {
  const { pageId, bbox } = fieldSource(field);
  return pageId && bbox ? (pageNoOf.get(pageId) ?? null) : null;
}

function FieldRow({
  field,
  selected,
  draft,
  pageNo,
  canEdit,
  buttonRef,
  onSelect,
  onDraft,
  onClearDraft,
  onReturnFocus,
}: {
  field: ClaimField;
  selected: boolean;
  draft: FieldDraft;
  pageNo: number | null;
  canEdit: boolean;
  buttonRef: (element: HTMLButtonElement | null) => void;
  onSelect: () => void;
  onDraft: (patch: Partial<FieldDraft>) => void;
  onClearDraft: () => void;
  onReturnFocus: () => void;
}) {
  const name = fieldLabel(field);
  const value = displayValue(field);
  const mono = field.valueKind === "MONEY" || field.valueKind === "DATE";
  const item = useRef<HTMLLIElement>(null);

  // A row selected by a key, or a new expense line, may be below the fold.
  useEffect(() => {
    if (selected) {
      item.current?.scrollIntoView?.({ block: "nearest" });
    }
  }, [selected]);

  return (
    <li
      ref={item}
      aria-label={name}
      className={`border-t border-l-2 border-t-line first:border-t-0 ${
        selected ? "border-l-primary bg-row-selected" : "border-l-transparent"
      }`}
    >
      <div className="grid grid-cols-[minmax(0,2fr)_minmax(0,3fr)_232px] items-center gap-x-3">
        <button
          ref={buttonRef}
          type="button"
          aria-current={selected ? "true" : undefined}
          onClick={onSelect}
          className="col-span-2 grid min-h-11 grid-cols-subgrid items-center py-2 pl-3 text-left text-[13px] transition-colors duration-150 hover:bg-background"
        >
          <span className="font-medium">
            {name}
            {field.required && (
              <>
                <span aria-hidden="true"> *</span>
                <span className="sr-only"> (required)</span>
              </>
            )}
          </span>
          <span className={`min-w-0 break-words ${mono ? "font-mono" : ""}`}>
            {field.status === "NOT_PRESENT" ? (
              <span className="text-on-surface-muted">Not present</span>
            ) : value !== null ? (
              value
            ) : (
              <span className="rounded-sm bg-danger-container px-1.5 py-0.5 text-xs text-danger">Needs entry</span>
            )}
          </span>
        </button>
        <span className="flex flex-wrap items-center justify-end gap-1.5 pr-3">
          {!selected && draft.editing && <span className="text-xs text-warning">Unsaved</span>}
          {field.aiMarker && <AiMarker />}
          {field.aiMarker && field.ai && <ConfidenceBadge level={field.ai.level} confidence={field.ai.confidence} />}
          {!field.aiMarker && field.resolvedBy && <PersonMarker person={field.resolvedBy} />}
          <SourcePin pageNo={pageNo} fieldName={name} onClick={onSelect} />
        </span>
      </div>
      {selected && canEdit && (
        <FieldWork
          key={field.id}
          field={field}
          draft={draft}
          onDraft={onDraft}
          onClearDraft={onClearDraft}
          onReturnFocus={onReturnFocus}
        />
      )}
    </li>
  );
}
