"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { api } from "@/lib/api/client";
import { session } from "@/lib/api/session";
import { ROLE_HOME, safeReturnPath } from "@/lib/auth/roles";

const schema = z.object({
  email: z.string().trim().min(1, "Enter your work email.").max(254),
  password: z.string().min(1, "Enter your password.").max(200),
});

type Form = z.infer<typeof schema>;

const inputClass =
  "h-10 w-full rounded-sm border border-control-border bg-surface px-3.5 text-sm text-ink placeholder:text-on-shell-muted aria-invalid:border-danger";

export function SignInPage() {
  const router = useRouter();
  const next = safeReturnPath(useSearchParams().get("next"));
  const [failure, setFailure] = useState<string | null>(null);
  const [showPassword, setShowPassword] = useState(false);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<Form>({ resolver: zodResolver(schema) });

  const onSubmit = handleSubmit(async (values) => {
    setFailure(null);
    const { data, response } = await api.POST("/auth/login", { body: values }).catch(() => ({
      data: undefined,
      response: undefined,
    }));

    if (data) {
      session.start(data);
      router.replace(next ?? ROLE_HOME[data.user.role]);
      return;
    }

    // The same message for a wrong password, an unknown email and a disabled account (US01).
    setFailure(
      response?.status === 429
        ? "Too many sign-in attempts. Please wait a few minutes and try again."
        : response?.status === 401
          ? "Wrong email or password."
          : "Sign-in is not available right now. Please try again.",
    );
  });

  return (
    <div className="sign-in-grid flex min-h-screen flex-col text-on-shell">
      <div className="mx-auto flex min-h-screen w-full max-w-[1440px] flex-col justify-between p-8 md:p-14 lg:p-20">
        <header className="flex items-center gap-2.5">
          <span className="inline-block size-2.5 rounded-[1px] bg-primary" aria-hidden="true" />
          <span className="text-xl font-bold tracking-tight">ClaimLens</span>
        </header>

        <main className="my-auto grid grid-cols-1 items-center gap-12 py-12 lg:grid-cols-12 lg:gap-16">
          <section className="flex flex-col justify-center lg:col-span-7 lg:pr-8">
            <div className="mb-6">
              <span className="inline-block rounded-sm border border-shell-accent/20 bg-shell-accent/10 px-2.5 py-1 font-mono text-xs font-semibold tracking-widest text-shell-accent uppercase">
                Staff access
              </span>
            </div>
            <h1 className="font-display text-3xl leading-[1.18] font-extrabold tracking-tight uppercase sm:text-4xl lg:text-[42px]">
              <span>Evidence first.</span>
              <br />
              <span>Every line explained.</span>
              <br />
              <span className="text-primary">Every decision owned.</span>
            </h1>
            <p className="mt-7 max-w-xl text-base leading-relaxed text-on-shell-muted sm:text-lg">
              AI reads the documents. Rules compute the money. People decide.
            </p>
          </section>

          <section className="flex justify-center lg:col-span-5 lg:justify-end">
            <div className="w-full max-w-[400px] border border-white/10 bg-surface p-8 text-ink shadow-2xl md:p-10">
              <h2 className="mb-8 text-2xl font-bold tracking-tight">Sign in</h2>

              <form className="space-y-5" onSubmit={onSubmit} noValidate>
                {failure && (
                  <p role="alert" className="rounded-sm bg-danger-container px-3 py-2 text-[13px] text-danger">
                    {failure}
                  </p>
                )}

                <div className="space-y-1.5">
                  <label className="block text-xs font-medium text-on-surface-muted" htmlFor="email">
                    Work email
                  </label>
                  <input
                    id="email"
                    type="email"
                    autoComplete="email"
                    placeholder="reviewer@claimlens.test"
                    className={inputClass}
                    aria-invalid={errors.email ? true : undefined}
                    aria-describedby={errors.email ? "email-error" : undefined}
                    {...register("email")}
                  />
                  {errors.email && (
                    <p id="email-error" className="text-xs text-danger">
                      {errors.email.message}
                    </p>
                  )}
                </div>

                <div className="space-y-1.5">
                  <label className="block text-xs font-medium text-on-surface-muted" htmlFor="password">
                    Password
                  </label>
                  <div className="relative">
                    <input
                      id="password"
                      type={showPassword ? "text" : "password"}
                      autoComplete="current-password"
                      className={`${inputClass} pr-10`}
                      aria-invalid={errors.password ? true : undefined}
                      aria-describedby={errors.password ? "password-error" : undefined}
                      {...register("password")}
                    />
                    <button
                      type="button"
                      aria-label={showPassword ? "Hide password" : "Show password"}
                      aria-pressed={showPassword}
                      className="absolute inset-y-0 right-0 flex items-center pr-3 text-control-border hover:text-ink"
                      onClick={() => setShowPassword((shown) => !shown)}
                    >
                      <span className="material-symbols-outlined text-[18px]" aria-hidden="true">
                        {showPassword ? "visibility_off" : "visibility"}
                      </span>
                    </button>
                  </div>
                  {errors.password && (
                    <p id="password-error" className="text-xs text-danger">
                      {errors.password.message}
                    </p>
                  )}
                </div>

                <div className="pt-2">
                  <button
                    type="submit"
                    disabled={isSubmitting}
                    className="flex h-10 w-full items-center justify-center rounded-sm bg-primary text-sm font-semibold text-on-primary transition duration-150 hover:bg-primary-hover disabled:opacity-70"
                  >
                    {isSubmitting ? "Signing in…" : "Sign in"}
                  </button>
                </div>
              </form>
            </div>
          </section>
        </main>

        <footer className="border-t border-shell-raised pt-8 font-mono text-xs text-on-shell-muted">
          ClaimLens · synthetic data only
        </footer>
      </div>
    </div>
  );
}
