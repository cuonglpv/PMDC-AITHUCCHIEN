import { redirect } from "next/navigation";

/** Evidence is the only tab built so far; docs/10-code-structure.md has this redirect go to Overview once that exists. */
export default async function ClaimHome({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  redirect(`/claims/${id}/evidence`);
}
