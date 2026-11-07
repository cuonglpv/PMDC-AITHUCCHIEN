"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { Button } from "@/components/ui/Button";
import { api } from "@/lib/api/client";
import { ApiProblem, unwrap } from "@/lib/api/problem";
import type { ClaimDetail, ClaimSaved } from "@/lib/api/types";
import { parseDate } from "@/lib/format";
import { CHANNEL_LABEL, CHANNELS } from "@/lib/labels/claims";
import { PolicyCard } from "../PolicyCard";
import { intakeKeys } from "../queries";
import {
  conflictChanges,
  DETAIL_FIELDS,
  detailsSchema,
  toRequest,
  toUpdate,
  type DetailsValues,
  type Selection,
  type ValidDetails,
} from "./claim-form";

const inputClass =
  "mt-1 block h-9 w-full rounded-sm border border-control-border bg-surface px-3 text-[13px] text-ink aria-invalid:border-danger";

/**
 * SCR-12. A new claim is created here (`createClaim`); a draft opened again is saved with its row version
 * (`updateClaim`). Either way the officer lands on the documents step, where any warnings are shown.
 */
export function DetailsStep({
  selection,
  details,
  claim,
  onBack,
  onReload,
}: {
  selection: Selection;
  details: DetailsValues;
  /** The draft being edited; absent for a new claim. */
  claim?: ClaimDetail;
  onBack: (values: DetailsValues) => void;
  onReload?: () => Promise<unknown>;
}) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [problem, setProblem] = useState<ApiProblem | null>(null);
  const [reloading, setReloading] = useState(false);
  const {
    register,
    handleSubmit,
    getValues,
    setError,
    control,
    formState: { errors },
  } = useForm<DetailsValues, unknown, ValidDetails>({ resolver: zodResolver(detailsSchema), defaultValues: details });

  const serviceDate = useWatch({ control, name: "serviceDate" });
  const balanceDate = parseDate(serviceDate) ?? undefined;

  const save = useMutation({
    mutationFn: async (values: ValidDetails): Promise<ClaimSaved | null> => {
      if (!claim) {
        return unwrap(
          api.POST("/claims", {
            body: { memberId: selection.memberId, policyId: selection.policyId, ...toRequest(values) },
          }),
        );
      }
      const body = toUpdate(claim, selection, values);
      if (Object.keys(body).length === 1) {
        return null; // Nothing changed: there is nothing to save.
      }
      return unwrap(api.PATCH("/claims/{claimId}", { params: { path: { claimId: claim.id } }, body }));
    },
    onMutate: () => setProblem(null),
    onSuccess: (saved) => {
      const claimId = saved?.claim.id ?? claim!.id;
      if (saved) {
        queryClient.setQueryData(intakeKeys.claim(claimId), saved.claim);
        queryClient.setQueryData(intakeKeys.warnings(claimId), saved.warnings);
      }
      void queryClient.invalidateQueries({ queryKey: intakeKeys.queues });
      router.push(`/intake/${claimId}`);
    },
    onError: (error) => {
      const refusal = error instanceof ApiProblem ? error : new ApiProblem(0, null);
      // Field problems go next to their field, and focus moves to the first one.
      const fieldErrors = refusal.errors.filter((item) =>
        (DETAIL_FIELDS as readonly string[]).includes(item.field),
      );
      fieldErrors.forEach((item, index) =>
        setError(item.field as keyof DetailsValues, { message: item.message }, { shouldFocus: index === 0 }),
      );
      setProblem(refusal);
    },
  });

  const otherErrors = (problem?.errors ?? []).filter(
    (item) => !(DETAIL_FIELDS as readonly string[]).includes(item.field),
  );
  const changes = problem?.code === "VERSION_CONFLICT" ? conflictChanges(problem.problem?.details, getValues()) : [];

  const reload = async () => {
    setReloading(true);
    await onReload?.();
    setReloading(false);
    setProblem(null);
  };

  return (
    <form
      noValidate
      onSubmit={handleSubmit((values) => save.mutate(values))}
      className="mt-6 grid grid-cols-1 gap-6 xl:grid-cols-[minmax(0,1fr)_320px]"
    >
      <div className="max-w-xl min-w-0 space-y-5">
        {problem?.code === "VERSION_CONFLICT" ? (
          <div role="alert" className="rounded-sm bg-warning-container px-4 py-3 text-[13px] text-warning">
            <p className="font-medium">Someone else changed this claim while you were editing it.</p>
            <p className="mt-1">Your input is kept. Load the latest version, check your values, then save again.</p>
            {changes.length > 0 && (
              <ul className="mt-2 list-disc pl-5">
                {changes.map((change) => (
                  <li key={change.label}>
                    {change.label} is now <span className="font-mono">{change.value}</span>
                  </li>
                ))}
              </ul>
            )}
            <Button className="mt-3" disabled={reloading} onClick={() => void reload()}>
              Load the latest version
            </Button>
          </div>
        ) : problem?.code === "CLAIM_STATE_CONFLICT" ? (
          <div role="alert" className="rounded-sm bg-danger-container px-4 py-3 text-[13px] text-danger">
            <p className="font-medium">This claim has already been submitted, so it can no longer change.</p>
            {claim && (
              <Link href={`/intake/${claim.id}`} className="mt-1 inline-block underline">
                Open the claim
              </Link>
            )}
          </div>
        ) : (
          problem && (
            <div role="alert" className="rounded-sm bg-danger-container px-4 py-3 text-[13px] text-danger">
              <p className="font-medium">
                {problem.code === "POLICY_MEMBER_MISMATCH"
                  ? "This policy belongs to another member. Go back and choose the member again."
                  : problem.code === "VALIDATION_FAILED"
                    ? "Check the highlighted fields."
                    : "The claim could not be saved. Please try again."}
              </p>
              {otherErrors.map((item) => (
                <p key={item.field + item.code}>{item.message}</p>
              ))}
            </div>
          )
        )}

        <div>
          <label htmlFor="serviceDate" className="block text-[13px] font-medium">
            Service date
          </label>
          <input
            id="serviceDate"
            placeholder="dd/mm/yyyy"
            autoComplete="off"
            className={`${inputClass} max-w-48 font-mono`}
            aria-invalid={errors.serviceDate ? true : undefined}
            aria-describedby={`serviceDate-hint${errors.serviceDate ? " serviceDate-error" : ""}`}
            {...register("serviceDate")}
          />
          <p id="serviceDate-hint" className="mt-1 text-xs text-on-surface-muted">
            Day, month, year: 12/10/2026 is 12 October.
          </p>
          <FieldError id="serviceDate-error" message={errors.serviceDate?.message} />
        </div>

        <div>
          <label htmlFor="providerName" className="block text-[13px] font-medium">
            Provider
          </label>
          <input
            id="providerName"
            className={inputClass}
            autoComplete="off"
            aria-invalid={errors.providerName ? true : undefined}
            aria-describedby={errors.providerName ? "providerName-error" : undefined}
            {...register("providerName")}
          />
          <FieldError id="providerName-error" message={errors.providerName?.message} />
        </div>

        <div>
          <label htmlFor="claimedTotal" className="block text-[13px] font-medium">
            Claimed total (₫)
          </label>
          <input
            id="claimedTotal"
            inputMode="numeric"
            autoComplete="off"
            className={`${inputClass} max-w-60 text-right font-mono`}
            aria-invalid={errors.claimedTotal ? true : undefined}
            aria-describedby={`claimedTotal-hint${errors.claimedTotal ? " claimedTotal-error" : ""}`}
            {...register("claimedTotal")}
          />
          <p id="claimedTotal-hint" className="mt-1 text-xs text-on-surface-muted">
            Whole VND, as written on the claim form.
          </p>
          <FieldError id="claimedTotal-error" message={errors.claimedTotal?.message} />
        </div>

        <fieldset aria-describedby={errors.channel ? "channel-error" : undefined}>
          <legend className="text-[13px] font-medium">Channel</legend>
          <div className="mt-1 flex flex-wrap gap-4">
            {CHANNELS.map((channel) => (
              <label key={channel} className="flex h-9 items-center gap-2 text-[13px]">
                <input type="radio" value={channel} {...register("channel")} />
                {CHANNEL_LABEL[channel]}
              </label>
            ))}
          </div>
          <FieldError id="channel-error" message={errors.channel?.message} />
        </fieldset>
      </div>

      <aside>
        <PolicyCard selection={selection} serviceDate={balanceDate} />
      </aside>

      <div className="flex justify-end gap-2 xl:col-span-2">
        <Button onClick={() => onBack(getValues())}>Back</Button>
        <Button type="submit" variant="primary" disabled={save.isPending}>
          {save.isPending ? "Saving…" : claim ? "Save and continue" : "Create claim"}
        </Button>
      </div>
    </form>
  );
}

function FieldError({ id, message }: { id: string; message: string | undefined }) {
  return message ? (
    <p id={id} className="mt-1 flex items-center gap-1 text-xs text-danger">
      <span className="material-symbols-outlined text-[14px]" aria-hidden="true">
        error
      </span>
      {message}
    </p>
  ) : null;
}
