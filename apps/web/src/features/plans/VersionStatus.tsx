import { StatusPill } from "@/components/ui/StatusPill";
import { RULE_SET_STATUS_LABEL, type RuleSetStatus } from "@/lib/labels/rule-sets";

const TONE = { DRAFT: "warning", PUBLISHED: "success", DISCARDED: "neutral" } as const;

export function VersionStatus({ status }: { status: RuleSetStatus }) {
  return <StatusPill tone={TONE[status]}>{RULE_SET_STATUS_LABEL[status]}</StatusPill>;
}
