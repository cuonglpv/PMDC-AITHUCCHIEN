import Link from "next/link";

export default function NotFound() {
  return (
    <main className="flex min-h-screen flex-col items-start justify-center gap-4 p-12">
      <p className="font-mono text-[11px] font-medium tracking-[0.08em] text-primary-text uppercase">404</p>
      <h1 className="text-base font-semibold">This page does not exist</h1>
      <Link className="underline" href="/">
        Back to your home page
      </Link>
    </main>
  );
}
