import { Suspense } from "react";
import { SignInPage } from "@/features/auth/SignInPage";

export default function SignIn() {
  return (
    <Suspense>
      <SignInPage />
    </Suspense>
  );
}
