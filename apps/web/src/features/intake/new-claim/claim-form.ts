import { z } from "zod";
import type {
  ClaimDetail,
  IntakeChannel,
  MemberSearchResult,
  PolicyStatus,
  VersionConflictDetails,
} from "@/lib/api/types";
import { formatDate, formatMoney, parseDate, parseMoney } from "@/lib/format";
import { CHANNEL_LABEL } from "@/lib/labels/claims";

/** The member and policy chosen in step 1, from a search result or from the draft being edited. */
export type Selection = {
  memberId: string;
  memberCode: string;
  fullName: string;
  nationalIdMasked: string;
  policyId: string;
  policyNumber: string;
  planName: string;
  policyStatus: PolicyStatus;
  coverageStart: string;
  coverageEnd: string;
};

export function selectionFromSearch(result: MemberSearchResult): Selection {
  return {
    memberId: result.memberId,
    memberCode: result.memberCode,
    fullName: result.fullName,
    nationalIdMasked: result.nationalIdMasked,
    policyId: result.policy.policyId,
    policyNumber: result.policy.policyNumber,
    planName: result.policy.planName,
    policyStatus: result.policy.status,
    coverageStart: result.policy.coverageStart,
    coverageEnd: result.policy.coverageEnd,
  };
}

export function selectionFromClaim(claim: ClaimDetail): Selection {
  return {
    memberId: claim.member.id,
    memberCode: claim.member.memberCode,
    fullName: claim.member.fullName,
    nationalIdMasked: claim.member.nationalIdMasked,
    policyId: claim.policy.id,
    policyNumber: claim.policy.policyNumber,
    planName: claim.policy.planName,
    policyStatus: claim.policy.status,
    coverageStart: claim.policy.coverageStart,
    coverageEnd: claim.policy.coverageEnd,
  };
}

const isChannel = (value: string): value is IntakeChannel => value in CHANNEL_LABEL;

/** Step 2 as typed. Required fields and formats only: the API owns every business rule and answers 422 with fields. */
export const detailsSchema = z.object({
  serviceDate: z
    .string()
    .trim()
    .min(1, "Enter the service date.")
    .refine((value) => parseDate(value) !== null, "Enter the date as dd/mm/yyyy, such as 12/10/2026."),
  providerName: z.string().trim().min(1, "Enter the provider's name.").max(200, "Use at most 200 characters."),
  claimedTotal: z
    .string()
    .trim()
    .min(1, "Enter the claimed total.")
    .refine((value) => (parseMoney(value) ?? 0) >= 1, "Enter a whole amount in VND, such as 1,400,000."),
  channel: z.string().refine(isChannel, "Choose how the claim arrived."),
});

/** The form as typed; `ValidDetails` is what passed the schema. */
export type DetailsValues = z.input<typeof detailsSchema>;
export type ValidDetails = z.output<typeof detailsSchema>;

export const DETAIL_FIELDS = ["serviceDate", "providerName", "claimedTotal", "channel"] as const satisfies readonly (keyof DetailsValues)[];

export const EMPTY_DETAILS: DetailsValues = { serviceDate: "", providerName: "", claimedTotal: "", channel: "" };

const typedMoney = new Intl.NumberFormat("en-US", { maximumFractionDigits: 0 });

export function detailsFromClaim(claim: ClaimDetail): DetailsValues {
  return {
    serviceDate: formatDate(claim.serviceDate),
    providerName: claim.providerName,
    claimedTotal: typedMoney.format(claim.claimedTotal),
    channel: claim.channel,
  };
}

/** The request values of a valid form. */
export function toRequest(values: DetailsValues) {
  return {
    serviceDate: parseDate(values.serviceDate) ?? "",
    providerName: values.providerName.trim(),
    claimedTotal: parseMoney(values.claimedTotal) ?? 0,
    channel: values.channel as IntakeChannel,
  };
}

/** What `updateClaim` sends: the row version and only what changed, so another person's edit of another field stands. */
export function toUpdate(claim: ClaimDetail, selection: Selection, values: DetailsValues) {
  const next = toRequest(values);
  return {
    rowVersion: claim.rowVersion,
    ...(selection.memberId !== claim.member.id && { memberId: selection.memberId }),
    ...(selection.policyId !== claim.policy.id && { policyId: selection.policyId }),
    ...(next.serviceDate !== claim.serviceDate && { serviceDate: next.serviceDate }),
    ...(next.providerName !== claim.providerName && { providerName: next.providerName }),
    ...(next.claimedTotal !== claim.claimedTotal && { claimedTotal: next.claimedTotal }),
    ...(next.channel !== claim.channel && { channel: next.channel }),
  };
}

const FIELD_LABEL: Record<(typeof DETAIL_FIELDS)[number], string> = {
  serviceDate: "Service date",
  providerName: "Provider",
  claimedTotal: "Claimed total",
  channel: "Channel",
};

/**
 * For the Conflict state: the fields another person changed to something other than what this officer typed, read
 * from the `details.current` of a 409 `VERSION_CONFLICT`.
 */
export function conflictChanges(details: unknown, values: DetailsValues): { label: string; value: string }[] {
  if (!details || typeof details !== "object" || !("current" in details)) {
    return [];
  }
  const current = (details as VersionConflictDetails).current as Partial<Record<string, unknown>>;
  const mine = toRequest(values);
  const changes: { label: string; value: string }[] = [];
  for (const field of DETAIL_FIELDS) {
    const theirs = current[field];
    if (theirs === undefined || theirs === null || theirs === mine[field]) {
      continue;
    }
    const shown =
      field === "claimedTotal"
        ? formatMoney(Number(theirs))
        : field === "serviceDate"
          ? formatDate(String(theirs))
          : field === "channel" && isChannel(String(theirs))
            ? CHANNEL_LABEL[theirs as IntakeChannel]
            : String(theirs);
    changes.push({ label: FIELD_LABEL[field], value: shown });
  }
  return changes;
}
