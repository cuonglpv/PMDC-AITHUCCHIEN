"use client";

import { useEffect, useId, useRef, useState, type KeyboardEvent, type PointerEvent } from "react";
import type { Bbox } from "@/lib/api/types";

/** Where the box starts for someone using the keyboard. */
const START_BOX: Bbox = { x: 0.25, y: 0.25, w: 0.5, h: 0.08 };
/** A drag smaller than this is a slip of the mouse, not a region. */
const SMALLEST = 0.005;
const STEP = 0.01;

const clamp = (value: number) => Math.min(1, Math.max(0, value));
const rounded = (value: number) => Math.round(value * 10_000) / 10_000;

function normalised(box: Bbox): Bbox {
  return { x: rounded(box.x), y: rounded(box.y), w: rounded(box.w), h: rounded(box.h) };
}

/** Moves the box inside the page, or with Shift changes its size; the box never leaves the page. */
function keyed(box: Bbox, key: string, resize: boolean): Bbox {
  const dx = key === "ArrowLeft" ? -STEP : key === "ArrowRight" ? STEP : 0;
  const dy = key === "ArrowUp" ? -STEP : key === "ArrowDown" ? STEP : 0;
  if (resize) {
    return {
      ...box,
      w: Math.max(STEP, Math.min(1 - box.x, box.w + dx)),
      h: Math.max(STEP, Math.min(1 - box.y, box.h + dy)),
    };
  }
  return { ...box, x: Math.max(0, Math.min(1 - box.w, box.x + dx)), y: Math.max(0, Math.min(1 - box.h, box.y + dy)) };
}

type Point = { x: number; y: number };

const spanned = (from: Point, to: Point): Bbox => ({
  x: Math.min(from.x, to.x),
  y: Math.min(from.y, to.y),
  w: Math.abs(to.x - from.x),
  h: Math.abs(to.y - from.y),
});

/**
 * Lets the reviewer mark where a value came from (BR-02): drag over the page, or use the keyboard, and the box is
 * reported normalised 0 to 1. It covers the page image exactly, and takes focus so the keyboard path starts here.
 */
export function RegionDraw({
  fieldName,
  onDraw,
  onCancel,
}: {
  fieldName: string;
  onDraw: (bbox: Bbox) => void;
  onCancel: () => void;
}) {
  const [box, setBox] = useState<Bbox>(START_BOX);
  const start = useRef<Point | null>(null);
  const surface = useRef<HTMLDivElement>(null);
  const hintId = useId();

  useEffect(() => {
    surface.current?.focus({ preventScroll: true });
  }, []);

  const pointAt = (event: PointerEvent<HTMLDivElement>): Point | null => {
    const rect = event.currentTarget.getBoundingClientRect();
    return rect.width > 0 && rect.height > 0
      ? { x: clamp((event.clientX - rect.left) / rect.width), y: clamp((event.clientY - rect.top) / rect.height) }
      : null;
  };

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    const point = pointAt(event);
    if (!point) {
      return;
    }
    start.current = point;
    event.currentTarget.setPointerCapture?.(event.pointerId);
    setBox({ ...point, w: 0, h: 0 });
  };

  const onPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    const point = pointAt(event);
    if (start.current && point) {
      setBox(spanned(start.current, point));
    }
  };

  const onPointerUp = (event: PointerEvent<HTMLDivElement>) => {
    const point = pointAt(event);
    const from = start.current;
    start.current = null;
    if (!from || !point) {
      return;
    }
    const drawn = spanned(from, point);
    if (drawn.w >= SMALLEST && drawn.h >= SMALLEST) {
      onDraw(normalised(drawn));
    }
  };

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === "Escape") {
      event.preventDefault();
      onCancel();
    } else if (event.key === "Enter") {
      event.preventDefault();
      onDraw(normalised(box));
    } else if (event.key.startsWith("Arrow")) {
      event.preventDefault();
      setBox(keyed(box, event.key, event.shiftKey));
    }
  };

  return (
    <div
      ref={surface}
      data-region-draw
      role="application"
      tabIndex={0}
      aria-label={`Draw the source region of ${fieldName}`}
      aria-describedby={hintId}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onKeyDown={onKeyDown}
      className="absolute inset-0 cursor-crosshair touch-none"
    >
      <span id={hintId} className="sr-only">
        Drag over the page. Or use the arrow keys to move the box, Shift and the arrow keys to change its size, Enter to
        use it and Escape to cancel.
      </span>
      <svg viewBox="0 0 1 1" preserveAspectRatio="none" className="pointer-events-none absolute inset-0 h-full w-full" aria-hidden="true">
        <rect
          data-testid="region-draw-box"
          x={box.x}
          y={box.y}
          width={box.w}
          height={box.h}
          strokeWidth={2}
          strokeDasharray="6 3"
          vectorEffect="non-scaling-stroke"
          className="fill-ink/10 stroke-ink"
        />
      </svg>
    </div>
  );
}
