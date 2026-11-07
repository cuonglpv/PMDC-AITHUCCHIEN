import { ClaimDocumentsPage } from "@/features/intake/documents/ClaimDocumentsPage";

export default async function IntakeClaim({ params }: { params: Promise<{ claimId: string }> }) {
  const { claimId } = await params;
  return <ClaimDocumentsPage claimId={claimId} />;
}
