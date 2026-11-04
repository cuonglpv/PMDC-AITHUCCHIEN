"use client";

import { useSyncExternalStore } from "react";
import { session } from "@/lib/api/session";

const unknown = { status: "unknown" } as const;

export function useSession() {
  // The server never knows the session, so the first render is always "unknown".
  return useSyncExternalStore(session.subscribe, session.getState, () => unknown);
}
