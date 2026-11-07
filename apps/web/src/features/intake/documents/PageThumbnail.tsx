"use client";

import { useEffect, useRef } from "react";
import { usePageImage } from "../queries";

/**
 * A page thumbnail fetched through the API (`getPage?variant=thumbnail`) and shown from a blob URL, which is revoked
 * when the image changes or leaves the screen. The URL goes straight onto the element, so nothing renders twice.
 */
export function PageThumbnail({ claimId, pageId, label }: { claimId: string; pageId: string; label: string }) {
  const image = usePageImage(claimId, pageId, "thumbnail");
  const ref = useRef<HTMLImageElement>(null);
  const blob = image.data;

  useEffect(() => {
    const element = ref.current;
    if (!blob || !element) {
      return;
    }
    const url = URL.createObjectURL(blob);
    element.src = url;
    return () => {
      element.removeAttribute("src");
      URL.revokeObjectURL(url);
    };
  }, [blob]);

  if (image.isError) {
    return (
      <div className="flex aspect-[3/4] w-full flex-col items-center justify-center gap-1 border border-line bg-background text-xs text-on-surface-muted">
        <span className="material-symbols-outlined text-[20px]" aria-hidden="true">
          broken_image
        </span>
        Preview unavailable
      </div>
    );
  }

  return (
    <div className="aspect-[3/4] w-full border border-line bg-background">
      {/* eslint-disable-next-line @next/next/no-img-element -- a blob URL from the API, which next/image cannot load */}
      <img
        ref={ref}
        alt={label}
        hidden={!blob}
        className="h-full w-full object-contain"
      />
    </div>
  );
}
