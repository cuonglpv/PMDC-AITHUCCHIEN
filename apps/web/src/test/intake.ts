import type {
  ClaimDetail,
  ClaimQueueItem,
  ClaimStatusSnapshot,
  Document,
  DocumentPage,
  MemberSearchResult,
  PolicySummary,
} from "@/lib/api/types";

/** Intake test data, typed from schema.d.ts so a contract change breaks the tests that rely on it. */
export const CLAIM_ID = "30000000-0000-4000-8000-000000000184";
export const MEMBER_ID = "31000000-0000-4000-8000-000000007731";
export const POLICY_ID = "32000000-0000-4000-8000-000000000731";

const officer = { id: "10000000-0000-4000-8000-000000000001", fullName: "Phạm Gia Bảo", initials: "PB" };

export function policySummary(overrides: Partial<PolicySummary> = {}): PolicySummary {
  return {
    policyId: POLICY_ID,
    policyNumber: "PL-2026-00731",
    memberId: MEMBER_ID,
    planCode: "GOLD-OP",
    planName: "Gold Outpatient",
    status: "ACTIVE",
    coverageStart: "2026-01-01",
    coverageEnd: "2026-12-31",
    firstEnrolledOn: "2024-01-01",
    consumedAmount: 4_200_000,
    annualLimit: 5_000_000,
    remainingBalance: 800_000,
    asOfDate: "2026-11-07",
    ...overrides,
  };
}

export function searchResult(overrides: Partial<MemberSearchResult> = {}): MemberSearchResult {
  return {
    memberId: MEMBER_ID,
    memberCode: "MB-0007731",
    fullName: "Nguyễn Văn An",
    dateOfBirth: "1988-03-14",
    nationalIdMasked: "********4821",
    policy: policySummary(),
    ...overrides,
  };
}

export function claimDetail(overrides: Partial<ClaimDetail> = {}): ClaimDetail {
  return {
    id: CLAIM_ID,
    claimNumber: "CLM-2026-000184",
    status: "DRAFT",
    isReturned: false,
    returnComment: null,
    member: {
      id: MEMBER_ID,
      memberCode: "MB-0007731",
      fullName: "Nguyễn Văn An",
      dateOfBirth: "1988-03-14",
      nationalIdMasked: "********4821",
    },
    policy: {
      id: POLICY_ID,
      policyNumber: "PL-2026-00731",
      status: "ACTIVE",
      coverageStart: "2026-01-01",
      coverageEnd: "2026-12-31",
      planCode: "GOLD-OP",
      planName: "Gold Outpatient",
    },
    providerName: "Phòng khám Đa khoa Hồng Ngọc",
    serviceDate: "2026-10-12",
    claimedTotal: 1_400_000,
    channel: "COUNTER",
    priority: "NORMAL",
    registeredBy: officer,
    assignee: null,
    assignedAt: null,
    submittedAt: null,
    statusChangedAt: "2026-10-13T08:50:00Z",
    createdAt: "2026-10-13T08:50:00Z",
    updatedAt: "2026-10-13T08:50:00Z",
    rowVersion: 3,
    ruleSetVersion: null,
    currentRevision: null,
    balance: null,
    counts: null,
    ai: { fallbackActive: false, clinicalCheck: "NOT_RUN" },
    allowedActions: ["EDIT_CLAIM", "UPLOAD_DOCUMENT", "SUBMIT_CLAIM"],
    blockers: [],
    ...overrides,
  };
}

export function queueItem(overrides: Partial<ClaimQueueItem> = {}): ClaimQueueItem {
  return {
    id: CLAIM_ID,
    claimNumber: "CLM-2026-000184",
    status: "DRAFT",
    isReturned: false,
    memberName: "Nguyễn Văn An",
    planCode: "GOLD-OP",
    serviceDate: "2026-10-12",
    claimedTotal: 1_400_000,
    priority: "NORMAL",
    statusChangedAt: "2026-10-13T08:50:00Z",
    assigneeInitials: null,
    fieldsToVerify: 0,
    blockingAlerts: 0,
    warningAlerts: 0,
    openFlags: 0,
    payableAmount: null,
    recommendedOutcome: null,
    decisionAction: null,
    decidedAt: null,
    ...overrides,
  };
}

export function page(overrides: Partial<DocumentPage> = {}): DocumentPage {
  return {
    id: "41000000-0000-4000-8000-000000000001",
    documentId: "40000000-0000-4000-8000-000000000001",
    pageNo: 1,
    claimPageNo: 1,
    widthPx: 1240,
    heightPx: 1754,
    pageType: "UNCLASSIFIED",
    pageTypeSource: null,
    pageTypeSetBy: null,
    quality: "NOT_CHECKED",
    aiStatus: "QUEUED",
    ai: null,
    ...overrides,
  };
}

export function document(overrides: Partial<Document> = {}): Document {
  return {
    id: "40000000-0000-4000-8000-000000000001",
    originalFilename: "hoa-don.pdf",
    fileFormat: "PDF",
    sizeBytes: 482_000,
    pageCount: 1,
    status: "READY",
    rejectionReason: null,
    uploadedBy: officer,
    uploadedAt: "2026-10-13T09:00:00Z",
    pages: [page()],
    ...overrides,
  };
}

/** The poll answer that matches these documents. */
export function snapshotOf(documents: Document[], overrides: Partial<ClaimStatusSnapshot> = {}): ClaimStatusSnapshot {
  return {
    claimId: CLAIM_ID,
    status: "DRAFT",
    isReturned: false,
    rowVersion: 3,
    fallbackActive: false,
    clinicalCheck: "NOT_RUN",
    currentRevisionNo: null,
    isRevisionStale: false,
    documents: documents.map((item) => ({
      id: item.id,
      status: item.status,
      rejectionReason: item.rejectionReason,
      pages: item.pages.map(({ id, claimPageNo, pageType, quality, aiStatus }) => ({
        id,
        claimPageNo,
        pageType,
        quality,
        aiStatus,
      })),
    })),
    aiRuns: [],
    ...overrides,
  };
}
