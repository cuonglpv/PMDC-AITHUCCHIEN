import { redirect } from "next/navigation";

/** SCR-41 is one screen: a plan opens on its versions (docs/04-information-architecture.md). */
export default async function Plan({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  redirect(`/plans/${id}/versions`);
}
