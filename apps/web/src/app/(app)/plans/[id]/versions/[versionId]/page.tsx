import { RuleSetEditor } from "@/features/plans/editor/RuleSetEditor";

export default async function RuleSetVersion({ params }: { params: Promise<{ id: string; versionId: string }> }) {
  const { id, versionId } = await params;
  return <RuleSetEditor planId={id} versionId={versionId} />;
}
