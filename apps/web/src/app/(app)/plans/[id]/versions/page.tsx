import { VersionsPage } from "@/features/plans/VersionsPage";

export default async function PlanVersions({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <VersionsPage planId={id} />;
}
