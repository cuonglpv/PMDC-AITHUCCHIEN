"use client";

import { useEffect, useRef } from "react";
import { usePageImage } from "@/lib/api/claim-queries";

/**
 * A page image (docs/10-code-structure.md, rule 5): `getPage` into a blob URL, revoked when the image changes or leaves
 * the screen. The URL goes straight onto the element, so nothing renders twice.
 */
export function PageImage({
  claimId,
  pageId,
  variant,
  alt,
  className = "",
}: {
  claimId: string;
  pageId: string;
  variant: "full" | "thumbnail";
  alt: string;
  className?: string;
}) {
  const image = usePageImage(claimId, pageId, variant);
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
      <div className={`flex flex-col items-center justify-center gap-1 text-center text-xs text-on-surface-muted ${className}`}>
        <span className="material-symbols-outlined text-[20px]" aria-hidden="true">
          broken_image
        </span>
        {variant === "full" && "This page could not be loaded."}
      </div>
    );
  }

  return (
    // eslint-disable-next-line @next/next/no-img-element -- a blob URL from the API, which next/image cannot load
    <img ref={ref} alt={alt} hidden={!blob} className={className} />
  );
}
