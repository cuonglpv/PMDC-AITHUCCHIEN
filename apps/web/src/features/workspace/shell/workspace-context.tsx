"use client";

import { useQueryClient } from "@tanstack/react-query";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
  type RefObject,
} from "react";
import { isListBehind, withLiveState } from "@/features/intake/processing";
import { claimKeys, useDocuments } from "@/lib/api/claim-queries";
import type { Bbox, ClaimDetail, ClaimStatusSnapshot, Document, DocumentPage } from "@/lib/api/types";
import type { RegionTone } from "../viewer/RegionOverlay";
import { useWorkspaceStatus, type PageRegion } from "../queries";

/** What zone C draws over the page. */
export type SourceHighlight = { pageId: string; bbox: Bbox; label: string; tone: RegionTone };

/** A request from zone B to draw a region in zone C. */
export type DrawRequest = { fieldName: string; onDrawn: (region: PageRegion) => void };

type Workspace = {
  claimId: string;
  claim: ClaimDetail;
  /** Until a reviewer takes the claim the API opens nothing else on it (`START_REVIEW` is what `allowedActions` offers). */
  mustStartReview: boolean;
  canEdit: boolean;
  documents: Document[];
  documentsLoading: boolean;
  pages: DocumentPage[];
  snapshot: ClaimStatusSnapshot | undefined;
  viewedPage: DocumentPage | null;
  viewPage: (pageId: string) => void;
  highlight: SourceHighlight | null;
  /** The linking rule (docs/04-information-architecture.md): selecting a row moves zone C to its source. */
  showSource: (pageId: string | null, highlight: SourceHighlight | null) => void;
  drawing: DrawRequest | null;
  startDrawing: (request: DrawRequest) => void;
  stopDrawing: () => void;
  /** Set by the tab that has a "next item", for the primary action in the sticky bar. */
  verifyNextRef: RefObject<(() => void) | null>;
};

const WorkspaceContext = createContext<Workspace | null>(null);

export function useWorkspace(): Workspace {
  const workspace = useContext(WorkspaceContext);
  if (!workspace) {
    throw new Error("useWorkspace is used outside the Claim workspace");
  }
  return workspace;
}

/** Holds what the three zones share: the claim, its pages, and which page and region zone C shows. */
export function WorkspaceProvider({ claim, children }: { claim: ClaimDetail; children: ReactNode }) {
  const claimId = claim.id;
  const queryClient = useQueryClient();
  const mustStartReview = claim.allowedActions.includes("START_REVIEW");
  const list = useDocuments(claimId, { enabled: !mustStartReview });
  const status = useWorkspaceStatus(claimId, !mustStartReview);
  const snapshot = status.data;

  const [viewedPageId, setViewedPageId] = useState<string | null>(null);
  const [highlight, setHighlight] = useState<SourceHighlight | null>(null);
  const [drawing, setDrawing] = useState<DrawRequest | null>(null);
  const verifyNextRef = useRef<(() => void) | null>(null);

  const items = list.data?.items;
  const documents = useMemo(() => withLiveState(items ?? [], snapshot), [items, snapshot]);
  const pages = useMemo(() => documents.flatMap((document) => document.pages), [documents]);
  const viewedPage = pages.find((page) => page.id === viewedPageId) ?? pages[0] ?? null;

  // The poll carries no medical data, so it only says when the fuller reads are out of date.
  const polledStatus = snapshot?.status;
  const polledReturned = snapshot?.isReturned;
  useEffect(() => {
    if (polledStatus && (polledStatus !== claim.status || polledReturned !== claim.isReturned)) {
      void queryClient.invalidateQueries({ queryKey: claimKeys.claim(claimId) });
    }
  }, [polledStatus, polledReturned, claim.status, claim.isReturned, claimId, queryClient]);

  const listBehind = Boolean(items && snapshot && isListBehind(items, snapshot));
  useEffect(() => {
    if (listBehind) {
      void queryClient.invalidateQueries({ queryKey: claimKeys.documents(claimId) });
    }
  }, [listBehind, claimId, queryClient]);

  const viewPage = useCallback((pageId: string) => setViewedPageId(pageId), []);
  const showSource = useCallback((pageId: string | null, next: SourceHighlight | null) => {
    if (pageId) {
      setViewedPageId(pageId);
    }
    setHighlight(next);
  }, []);
  const startDrawing = useCallback((request: DrawRequest) => setDrawing(request), []);
  const stopDrawing = useCallback(() => setDrawing(null), []);

  const value: Workspace = {
    claimId,
    claim,
    mustStartReview,
    canEdit: claim.allowedActions.includes("EDIT_FIELDS"),
    documents,
    documentsLoading: list.isPending && !mustStartReview,
    pages,
    snapshot,
    viewedPage,
    viewPage,
    highlight,
    showSource,
    drawing,
    startDrawing,
    stopDrawing,
    verifyNextRef,
  };

  return <WorkspaceContext.Provider value={value}>{children}</WorkspaceContext.Provider>;
}
