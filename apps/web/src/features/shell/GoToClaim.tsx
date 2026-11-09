"use client";

import { useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useId, useState, type FormEvent } from "react";
import { api } from "@/lib/api/client";
import { ApiProblem, unwrap } from "@/lib/api/problem";
import type { StaffRole } from "@/lib/api/types";
import { claimPathFor } from "@/lib/auth/roles";
import { GO_TO_CLAIM_FAILED, GO_TO_CLAIM_NOT_FOUND } from "@/lib/labels/claims";

/**
 * The top bar lookup (US23): opens a claim by its number. The API answers 404 for a claim out of the caller's scope, and
 * a 403 here reads the same, so a number reveals nothing about other people's claims.
 */
export function GoToClaim({ role }: { role: StaffRole }) {
  const router = useRouter();
  const id = useId();
  const [text, setText] = useState("");
  const lookup = useMutation({
    mutationFn: (claimNumber: string) =>
      unwrap(api.GET("/claim-numbers/{claimNumber}", { params: { path: { claimNumber } } })),
    onSuccess: (claim) => {
      const path = claimPathFor(role, claim.id);
      if (path) {
        setText("");
        router.push(path);
      }
    },
  });

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const claimNumber = text.trim().toUpperCase();
    if (claimNumber) {
      lookup.mutate(claimNumber);
    }
  };

  const message = !lookup.isError
    ? null
    : lookup.error instanceof ApiProblem && (lookup.error.status === 404 || lookup.error.status === 403)
      ? GO_TO_CLAIM_NOT_FOUND
      : GO_TO_CLAIM_FAILED;

  return (
    <form role="search" aria-label="Go to claim" onSubmit={submit} className="relative w-72">
      <label htmlFor={id} className="sr-only">
        Go to claim no.
      </label>
      <span
        className="material-symbols-outlined pointer-events-none absolute top-1/2 left-2 -translate-y-1/2 text-[18px] text-on-surface-muted"
        aria-hidden="true"
      >
        search
      </span>
      <input
        id={id}
        value={text}
        placeholder="Go to claim no."
        autoComplete="off"
        aria-describedby={message ? `${id}-error` : undefined}
        aria-invalid={message ? true : undefined}
        onChange={(event) => {
          setText(event.target.value);
          lookup.reset();
        }}
        className="h-8 w-full rounded-sm border border-control-border bg-surface pr-2 pl-8 text-[13px] placeholder:text-on-surface-muted"
      />
      {message && (
        <p
          id={`${id}-error`}
          role="alert"
          className="absolute top-full left-0 z-30 mt-1 w-full border border-line bg-surface px-3 py-2 text-[13px] text-danger shadow-[0_4px_12px_rgba(9,9,9,0.08)]"
        >
          {message}
        </p>
      )}
    </form>
  );
}
