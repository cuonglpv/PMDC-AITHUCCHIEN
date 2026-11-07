import { Suspense } from "react";
import { IntakeHomePage } from "@/features/intake/IntakeHomePage";

export default function IntakeHome() {
  return (
    <Suspense>
      <IntakeHomePage />
    </Suspense>
  );
}
