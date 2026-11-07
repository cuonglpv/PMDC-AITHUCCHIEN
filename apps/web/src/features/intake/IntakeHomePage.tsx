"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { EmptyState } from "@/components/states/EmptyState";
import { LoadError, Loading } from "@/components/states/PageState";
import { Button, buttonClassName } from "@/components/ui/Button";
import { StatusPill } from "@/components/ui/StatusPill";
import { Tab, TabList, TabPanel, Tabs } from "@/components/ui/Tabs";
import type { ClaimQueueItem, QueueView } from "@/lib/api/types";
import { formatDate, formatDateTime, formatMoney } from "@/lib/format";
import { CLAIM_STATUS_LABEL, CLAIM_STATUS_TONE } from "@/lib/labels/claims";
import { QUEUE_PAGE_SIZE, useQueue } from "./queries";

type IntakeTab = "drafts" | "submitted";

const VIEW: Record<IntakeTab, QueueView> = { drafts: "MY_DRAFTS", submitted: "MY_SUBMISSIONS" };

/** SCR-10: the officer's drafts and submissions (US02, US03). `?tab=submitted` opens the second tab. */
export function IntakeHomePage() {
  const router = useRouter();
  const params = useSearchParams();
  const [tab, setTab] = useState<IntakeTab>(params.get("tab") === "submitted" ? "submitted" : "drafts");
  const [page, setPage] = useState(1);
  const queue = useQueue(VIEW[tab], page);
  const counts = queue.data?.counts;

  const changeTab = (next: IntakeTab) => {
    setTab(next);
    setPage(1);
    router.replace(next === "submitted" ? "/intake?tab=submitted" : "/intake", { scroll: false });
  };

  return (
    <div className="p-6">
      <div className="flex items-end justify-between gap-4">
        <div>
          <p className="font-mono text-[11px] font-medium tracking-[0.08em] text-primary-text uppercase">Intake</p>
          <h1 className="mt-1 font-display text-xl font-medium tracking-[-0.01em] uppercase">Claims</h1>
        </div>
        <Link href="/intake/new" className={buttonClassName("primary")}>
          <span className="material-symbols-outlined text-[18px]" aria-hidden="true">
            add
          </span>
          New claim
        </Link>
      </div>

      <Tabs value={tab} onValueChange={changeTab} className="mt-6">
        <TabList label="Intake claims">
          <Tab value="drafts" count={counts?.MY_DRAFTS}>
            Drafts
          </Tab>
          <Tab value="submitted" count={counts?.MY_SUBMISSIONS}>
            My submissions
          </Tab>
        </TabList>
        {/* Base UI mounts only the active panel, so both panels can share the one query. */}
        {(["drafts", "submitted"] as const).map((value) => (
          <TabPanel key={value} value={value} className="mt-4">
            <QueueContent
              tab={value}
              queue={queue}
              page={page}
              onPage={setPage}
              onOpen={(claimId) => router.push(`/intake/${claimId}`)}
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
  onPage,
  onOpen,
}: {
  tab: IntakeTab;
  queue: ReturnType<typeof useQueue>;
  page: number;
  onPage: (page: number) => void;
  onOpen: (claimId: string) => void;
}) {
  // While another tab or page loads, the previous rows are only placeholders: show the skeleton instead.
  if (queue.isPending || queue.isPlaceholderData) {
    return <Loading label={tab === "drafts" ? "Loading drafts" : "Loading submissions"} />;
  }
  if (queue.isError) {
    return <LoadError error={queue.error} />;
  }

  const { items, total, pageSize } = queue.data;
  if (items.length === 0) {
    return tab === "drafts" ? (
      <EmptyState
        icon="draft"
        title="No drafts"
        description="A claim you register stays here until you submit it."
        action={
          <Link href="/intake/new" className={buttonClassName("secondary")}>
            New claim
          </Link>
        }
      />
    ) : (
      <EmptyState
        icon="outbox"
        title="No submitted claims yet"
        description="Claims you submit appear here, with the status they have reached."
      />
    );
  }

  const pages = Math.max(1, Math.ceil(total / (pageSize || QUEUE_PAGE_SIZE)));

  return (
    <>
      <table className="w-full border border-line bg-surface text-[13px]">
        <thead className="bg-background text-left font-mono text-[11px] tracking-[0.08em] text-on-surface-muted uppercase">
          <tr className="h-8">
            <th className="px-3 font-medium">Claim no.</th>
            <th className="px-3 font-medium">Member</th>
            <th className="px-3 font-medium">Plan</th>
            <th className="px-3 font-medium">Service date</th>
            <th className="px-3 text-right font-medium">Claimed</th>
            <th className="px-3 font-medium">{tab === "drafts" ? "Last changed" : "Status"}</th>
          </tr>
        </thead>
        <tbody>
          {items.map((claim) => (
            <QueueRow key={claim.id} claim={claim} tab={tab} onOpen={onOpen} />
          ))}
        </tbody>
      </table>

      {pages > 1 && (
        <nav aria-label="Pages" className="mt-3 flex items-center justify-end gap-3 text-[13px]">
          <span className="text-on-surface-muted">
            Page {page} of {pages}
          </span>
          <Button disabled={page <= 1} onClick={() => onPage(page - 1)}>
            Previous
          </Button>
          <Button disabled={page >= pages} onClick={() => onPage(page + 1)}>
            Next
          </Button>
        </nav>
      )}
    </>
  );
}

function QueueRow({ claim, tab, onOpen }: { claim: ClaimQueueItem; tab: IntakeTab; onOpen: (claimId: string) => void }) {
  return (
    <tr className="h-9 cursor-pointer border-t border-line hover:bg-background" onClick={() => onOpen(claim.id)}>
      <td className="px-3 font-mono">
        <Link
          href={`/intake/${claim.id}`}
          className="underline"
          // The row opens the claim too; the link is the keyboard path.
          onClick={(event) => event.stopPropagation()}
        >
          {claim.claimNumber}
        </Link>
      </td>
      <td className="px-3">{claim.memberName ?? "—"}</td>
      <td className="px-3 font-mono">{claim.planCode}</td>
      <td className="px-3 font-mono">{formatDate(claim.serviceDate)}</td>
      <td className="px-3 text-right font-mono">{formatMoney(claim.claimedTotal)}</td>
      <td className="px-3">
        {tab === "drafts" ? (
          <span className="font-mono">{formatDateTime(claim.statusChangedAt)}</span>
        ) : (
          <StatusPill tone={CLAIM_STATUS_TONE[claim.status]}>{CLAIM_STATUS_LABEL[claim.status]}</StatusPill>
        )}
      </td>
    </tr>
  );
}
