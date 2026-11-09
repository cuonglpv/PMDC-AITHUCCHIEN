"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useId, useState } from "react";
import { EmptyState } from "@/components/states/EmptyState";
import { LoadError, Loading } from "@/components/states/PageState";
import { Button } from "@/components/ui/Button";
import { StatusPill } from "@/components/ui/StatusPill";
import { Tab, TabList, TabPanel, Tabs } from "@/components/ui/Tabs";
import { useStartReview } from "@/lib/api/claim-queries";
import { ApiProblem } from "@/lib/api/problem";
import type { ClaimQueueItem, Priority, QueueView } from "@/lib/api/types";
import { formatAge, formatDate, formatMoney } from "@/lib/format";
import { claimStatusPill, PRIORITY_LABEL, START_REVIEW_FAILED, START_REVIEW_PROBLEM_LABEL } from "@/lib/labels/claims";
import { QUEUE_PAGE_SIZE, useOpenNextClaim, useReviewQueue } from "./queries";

type QueueTab = "available" | "assigned" | "verification" | "returned";

const VIEW: Record<QueueTab, QueueView> = {
  available: "AVAILABLE",
  assigned: "ASSIGNED_TO_ME",
  verification: "NEEDS_VERIFICATION",
  returned: "RETURNED",
};

const TABS: { tab: QueueTab; label: string }[] = [
  { tab: "available", label: "Available" },
  { tab: "assigned", label: "Assigned to me" },
  { tab: "verification", label: "Needs verification" },
  { tab: "returned", label: "Returned" },
];

const EMPTY: Record<QueueTab, { icon: string; title: string; description: string }> = {
  available: {
    icon: "inbox",
    title: "No claims available",
    description: "Claims ready for a reviewer appear here once the AI has read them.",
  },
  assigned: {
    icon: "assignment_ind",
    title: "No claims assigned to you",
    description: "Start a claim from Available and it stays here until you submit it.",
  },
  verification: {
    icon: "visibility",
    title: "No claims need verification",
    description: "Claims you have started with fields left to check appear here.",
  },
  returned: {
    icon: "undo",
    title: "No returned claims",
    description: "A claim an approver sends back to you appears here.",
  },
};

const isQueueTab = (value: string | null): value is QueueTab => TABS.some(({ tab }) => tab === value);

/** How long typing in the filter waits before it asks the API. */
const FILTER_DELAY_MS = 300;

/** SCR-20: the reviewer's queue, in the API's priority order (US23, BR-09). `?tab=` opens a tab. */
export function ReviewQueuePage() {
  const router = useRouter();
  const params = useSearchParams();
  const requested = params.get("tab");
  const [tab, setTab] = useState<QueueTab>(isQueueTab(requested) ? requested : "available");
  const [page, setPage] = useState(1);
  const [typed, setTyped] = useState("");
  const [claimNumber, setClaimNumber] = useState("");
  const [notice, setNotice] = useState<string | null>(null);
  const filterId = useId();

  useEffect(() => {
    const next = typed.trim();
    if (next === claimNumber) {
      return;
    }
    const timer = setTimeout(() => {
      setClaimNumber(next);
      setPage(1);
    }, FILTER_DELAY_MS);
    return () => clearTimeout(timer);
  }, [typed, claimNumber]);

  const queue = useReviewQueue(VIEW[tab], page, claimNumber);
  const start = useStartReview();
  const openNext = useOpenNextClaim();
  const counts = queue.data?.counts;

  const changeTab = (next: QueueTab) => {
    setTab(next);
    setPage(1);
    setNotice(null);
    router.replace(next === "available" ? "/queue" : `/queue?tab=${next}`, { scroll: false });
  };

  const startClaim = (claim: ClaimQueueItem) => {
    setNotice(null);
    start.mutate(claim.id, {
      onSuccess: () => router.push(`/claims/${claim.id}`),
      onError: (error) => {
        const label = (error instanceof ApiProblem && error.code && START_REVIEW_PROBLEM_LABEL[error.code]) || null;
        setNotice(label ? `${claim.claimNumber}: ${label} The list has been refreshed.` : START_REVIEW_FAILED);
      },
    });
  };

  const openNextClaim = () => {
    setNotice(null);
    openNext.mutate(undefined, {
      onSuccess: (claim) => {
        if (claim) {
          router.push(`/claims/${claim.id}`);
        } else {
          setNotice("No claims are available right now.");
        }
      },
      onError: () => setNotice(START_REVIEW_FAILED),
    });
  };

  const busy = start.isPending || openNext.isPending;

  return (
    <div className="p-6">
      <div className="flex items-center justify-between gap-4">
        <h1 className="font-display text-xl font-medium tracking-[-0.01em] uppercase">Review queue</h1>
        <Button variant="primary" disabled={busy} onClick={openNextClaim}>
          Open next claim
          <span className="material-symbols-outlined text-[18px]" aria-hidden="true">
            arrow_forward
          </span>
        </Button>
      </div>

      <Tabs value={tab} onValueChange={changeTab} className="mt-6">
        <TabList label="Review queue">
          {TABS.map((item) => (
            <Tab key={item.tab} value={item.tab} count={counts?.[VIEW[item.tab]]}>
              {item.label}
            </Tab>
          ))}
        </TabList>

        <div className="mt-4 flex items-center gap-3">
          <label htmlFor={filterId} className="sr-only">
            Filter by claim no.
          </label>
          <div className="relative w-80">
            <span
              className="material-symbols-outlined pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2 text-[18px] text-on-surface-muted"
              aria-hidden="true"
            >
              search
            </span>
            <input
              id={filterId}
              value={typed}
              placeholder="Filter by claim no."
              autoComplete="off"
              onChange={(event) => setTyped(event.target.value)}
              className="h-9 w-full rounded-sm border border-control-border bg-surface pr-3 pl-9 text-[13px] placeholder:text-on-surface-muted"
            />
          </div>
          {tab === "available" && (
            <p className="ml-auto text-xs text-on-surface-muted">Member details appear after you start review.</p>
          )}
        </div>

        {notice && (
          <p role="alert" className="mt-3 rounded-sm bg-warning-container px-3 py-2 text-[13px] text-warning">
            {notice}
          </p>
        )}

        {/* Base UI mounts only the active panel, so all panels can share the one query. */}
        {TABS.map((item) => (
          <TabPanel key={item.tab} value={item.tab} className="mt-4">
            <QueueContent
              tab={item.tab}
              queue={queue}
              page={page}
              filtered={claimNumber !== ""}
              busy={busy}
              onPage={setPage}
              onStart={startClaim}
              onOpen={(claimId) => router.push(`/claims/${claimId}`)}
            />
          </TabPanel>
        ))}
      </Tabs>
    </div>
  );
}

function QueueContent({
  tab,
  queue,
  page,
  filtered,
  busy,
  onPage,
  onStart,
  onOpen,
}: {
  tab: QueueTab;
  queue: ReturnType<typeof useReviewQueue>;
  page: number;
  filtered: boolean;
  busy: boolean;
  onPage: (page: number) => void;
  onStart: (claim: ClaimQueueItem) => void;
  onOpen: (claimId: string) => void;
}) {
  // While another tab or page loads, the previous rows are only placeholders: show the skeleton instead.
  if (queue.isPending || queue.isPlaceholderData) {
    return <Loading label="Loading claims" />;
  }
  if (queue.isError) {
    return <LoadError error={queue.error} />;
  }

  const { items, total, pageSize } = queue.data;
  if (items.length === 0) {
    return filtered ? (
      <EmptyState
        icon="filter_alt_off"
        title="No results for this filter"
        description="No claim on this tab has that number. Check it, or clear the filter."
      />
    ) : (
      <EmptyState {...EMPTY[tab]} />
    );
  }

  const size = pageSize || QUEUE_PAGE_SIZE;
  const available = tab === "available";

  return (
    <div className="border border-line bg-surface">
      <table className="w-full text-[13px]">
        <thead className="bg-background text-left font-mono text-[11px] tracking-[0.08em] text-on-surface-muted uppercase">
          <tr className="h-8">
            <th className="px-3 font-medium">Priority</th>
            <th className="px-3 font-medium">Claim no.</th>
            <th className="px-3 font-medium">{available ? "Plan" : "Member"}</th>
            <th className="px-3 font-medium">Service date</th>
            <th className="px-3 text-right font-medium">Claimed</th>
            <th className="px-3 font-medium">Status</th>
            {available ? (
              <>
                <th className="px-3 font-medium">Age</th>
                <th className="px-3 font-medium">
                  <span className="sr-only">Action</span>
                </th>
              </>
            ) : (
              <th className="px-3 font-medium">Needs attention</th>
            )}
          </tr>
        </thead>
        <tbody>
          {items.map((claim) =>
            available ? (
              <AvailableRow
                key={claim.id}
                claim={claim}
                now={queue.dataUpdatedAt}
                busy={busy}
                onStart={() => onStart(claim)}
              />
            ) : (
              <MyClaimRow key={claim.id} claim={claim} onOpen={() => onOpen(claim.id)} />
            ),
          )}
        </tbody>
      </table>

      <nav aria-label="Pages" className="flex h-11 items-center justify-between border-t border-line px-3">
        <span className="font-mono text-xs text-on-surface-muted">
          {(page - 1) * size + 1}–{Math.min(page * size, total)} of {total}
        </span>
        <span className="flex items-center gap-1">
          <PageButton icon="chevron_left" label="Previous page" disabled={page <= 1} onClick={() => onPage(page - 1)} />
          <PageButton
            icon="chevron_right"
            label="Next page"
            disabled={page * size >= total}
            onClick={() => onPage(page + 1)}
          />
        </span>
      </nav>
    </div>
  );
}

function PageButton({
  icon,
  label,
  disabled,
  onClick,
}: {
  icon: string;
  label: string;
  disabled: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      disabled={disabled}
      onClick={onClick}
      className="flex size-7 items-center justify-center rounded-sm text-on-surface-muted transition-colors duration-150 hover:bg-background hover:text-ink disabled:cursor-not-allowed disabled:opacity-30"
    >
      <span className="material-symbols-outlined text-[18px]" aria-hidden="true">
        {icon}
      </span>
    </button>
  );
}

/** Icon and word, never colour alone; High is crimson, because it means handle first, not an error of the brand. */
function PriorityMark({ priority }: { priority: Priority }) {
  const high = priority === "HIGH";
  return (
    <span
      className={`inline-flex items-center gap-1 text-xs ${high ? "font-semibold text-danger" : "font-medium text-on-surface-muted"}`}
    >
      <span className="material-symbols-outlined text-[15px]" aria-hidden="true">
        {high ? "arrow_upward" : "remove"}
      </span>
      {PRIORITY_LABEL[priority]}
    </span>
  );
}

function ClaimStatus({ claim }: { claim: ClaimQueueItem }) {
  const pill = claimStatusPill(claim);
  return <StatusPill tone={pill.tone}>{pill.label}</StatusPill>;
}

/**
 * BR-11: a claim nobody has taken shows only its number, plan, service date, claimed total, status and age. The member
 * column is not drawn from the row at all, so a member name could not appear here even if one were sent.
 */
function AvailableRow({
  claim,
  now,
  busy,
  onStart,
}: {
  claim: ClaimQueueItem;
  now: number;
  busy: boolean;
  onStart: () => void;
}) {
  return (
    <tr className="h-11 border-t border-line hover:bg-background">
      <td className="px-3 whitespace-nowrap">
        <PriorityMark priority={claim.priority} />
      </td>
      <td className="px-3 font-mono whitespace-nowrap">{claim.claimNumber}</td>
      <td className="px-3 font-mono whitespace-nowrap text-on-surface-muted">{claim.planCode}</td>
      <td className="px-3 font-mono whitespace-nowrap text-on-surface-muted">{formatDate(claim.serviceDate)}</td>
      <td className="px-3 text-right font-mono font-semibold whitespace-nowrap">{formatMoney(claim.claimedTotal)}</td>
      <td className="px-3 whitespace-nowrap">
        <ClaimStatus claim={claim} />
      </td>
      <td className="px-3 font-mono whitespace-nowrap text-on-surface-muted">{formatAge(claim.statusChangedAt, now)}</td>
      <td className="px-3 text-right whitespace-nowrap">
        <Button
          variant="ghost"
          className="h-8 px-2"
          aria-label={`Start review of ${claim.claimNumber}`}
          disabled={busy}
          onClick={onStart}
        >
          Start review
          <span className="material-symbols-outlined text-[15px]" aria-hidden="true">
            arrow_forward
          </span>
        </Button>
      </td>
    </tr>
  );
}

function MyClaimRow({ claim, onOpen }: { claim: ClaimQueueItem; onOpen: () => void }) {
  return (
    <tr className="h-11 cursor-pointer border-t border-line hover:bg-background" onClick={onOpen}>
      <td className="px-3 whitespace-nowrap">
        <PriorityMark priority={claim.priority} />
      </td>
      <td className="px-3 font-mono whitespace-nowrap">
        <Link
          href={`/claims/${claim.id}`}
          className="underline"
          // The row opens the claim too; the link is the keyboard path.
          onClick={(event) => event.stopPropagation()}
        >
          {claim.claimNumber}
        </Link>
      </td>
      <td className="px-3">{claim.memberName ?? "—"}</td>
      <td className="px-3 font-mono whitespace-nowrap text-on-surface-muted">{formatDate(claim.serviceDate)}</td>
      <td className="px-3 text-right font-mono font-semibold whitespace-nowrap">{formatMoney(claim.claimedTotal)}</td>
      <td className="px-3 whitespace-nowrap">
        <ClaimStatus claim={claim} />
      </td>
      <td className="px-3 whitespace-nowrap">
        <NeedsAttention claim={claim} />
      </td>
    </tr>
  );
}

function NeedsAttention({ claim }: { claim: ClaimQueueItem }) {
  const chips = [
    { count: claim.fieldsToVerify, icon: "auto_awesome", word: "fields to verify", look: "bg-ai-container text-ai-strong" },
    { count: claim.blockingAlerts, icon: "error", word: "blocking alerts", look: "bg-danger-container text-danger" },
    { count: claim.openFlags, icon: "flag", word: "open flags", look: "bg-risk-container text-risk" },
  ].filter((chip) => chip.count > 0);

  if (chips.length === 0) {
    return <span className="text-on-surface-muted">—</span>;
  }
  return (
    <span className="flex items-center gap-1.5">
      {chips.map((chip) => (
        <span
          key={chip.word}
          title={`${chip.count} ${chip.word}`}
          className={`inline-flex items-center gap-1 rounded-sm px-2 py-0.5 text-xs font-medium ${chip.look}`}
        >
          <span className="material-symbols-outlined text-[13px]" aria-hidden="true">
            {chip.icon}
          </span>
          <span className="font-mono">{chip.count}</span>
          <span className="sr-only"> {chip.word}</span>
        </span>
      ))}
    </span>
  );
}
