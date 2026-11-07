import { Suspense } from "react";
import { NewClaimPage } from "@/features/intake/new-claim/NewClaimPage";

export default function NewClaim() {
  return (
    <Suspense>
      <NewClaimPage />
    </Suspense>
  );
}
