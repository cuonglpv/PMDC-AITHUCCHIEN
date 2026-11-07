"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { LoadError, Loading } from "@/components/states/PageState";
import type { ClaimDetail } from "@/lib/api/types";
import { IntakeStepper } from "../IntakeStepper";
import { useClaim } from "../queries";
import { detailsFromClaim, EMPTY_DETAILS, selectionFromClaim, type DetailsValues, type Selection } from "./claim-form";
import { DetailsStep } from "./DetailsStep";
import { MemberStep } from "./MemberStep";

/**
 * SCR-11 and SCR-12, the first two steps of New claim. `?claimId=` reopens a draft to change what step 1 or 2 set;
 * the claim id is not personal data, so it may sit in the URL.
 */
export function NewClaimPage() {
  const claimId = useSearchParams().get("claimId");
  return claimId ? <EditDraft claimId={claimId} /> : <ClaimSteps />;
}

function EditDraft({ claimId }: { claimId: string }) {
  const claim = useClaim(claimId);

  if (claim.isPending) {
    return <Loading label="Loading the claim" />;
  }
  if (claim.isError) {
    return <LoadError error={claim.error} />;
  }
  if (claim.data.status !== "DRAFT" || !claim.data.allowedActions.includes("EDIT_CLAIM")) {
    return (
      <div role="alert" className="m-6 rounded-sm border border-line bg-surface p-6">
        <p className="font-semibold">This claim can no longer be edited</p>
        <p className="mt-1 text-[13px] text-on-surface-muted">It has been submitted.</p>
        <Link href={`/intake/${claimId}`} className="mt-3 inline-block text-[13px] underline">
          Open the claim
        </Link>
      </div>
    );
  }

  // Keyed by id only: reloading after a conflict brings a new row version but keeps what the officer typed.
  return <ClaimSteps key={claim.data.id} claim={claim.data} onReload={() => claim.refetch()} />;
}

function ClaimSteps({ claim, onReload }: { claim?: ClaimDetail; onReload?: () => Promise<unknown> }) {
  const router = useRouter();
  const [step, setStep] = useState<1 | 2>(claim ? 2 : 1);
  const [selection, setSelection] = useState<Selection | null>(claim ? selectionFromClaim(claim) : null);
  const [details, setDetails] = useState<DetailsValues>(claim ? detailsFromClaim(claim) : EMPTY_DETAILS);

  return (
    <div className="p-6">
      <div className="flex flex-wrap items-baseline gap-x-4 gap-y-1">
        <h1 className="font-display text-xl font-medium tracking-[0.08em] uppercase">New claim</h1>
        {claim && (
          <span className="font-mono text-[13px] text-on-surface-muted">{claim.claimNumber} · Draft</span>
        )}
      </div>

      <IntakeStepper
        current={step}
        done={claim ? [1, 2] : step === 2 ? [1] : []}
        onStep={(next) => (next === 3 ? claim && router.push(`/intake/${claim.id}`) : setStep(next))}
      />

      {step === 1 || !selection ? (
        <MemberStep
          selection={selection}
          onCancel={() => router.push(claim ? `/intake/${claim.id}` : "/intake")}
          onContinue={(chosen) => {
            setSelection(chosen);
            setStep(2);
          }}
        />
      ) : (
        <DetailsStep
          selection={selection}
          details={details}
          claim={claim}
          onReload={onReload}
          onBack={(values) => {
            setDetails(values);
            setStep(1);
          }}
        />
      )}
    </div>
  );
}
