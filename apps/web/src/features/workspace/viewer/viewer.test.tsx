import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import { CLAIM_ID } from "@/test/intake";
import { API, problem, server } from "@/test/server";
import { PageImage } from "./PageImage";
import { RegionDraw } from "./RegionDraw";
import { RegionOverlay } from "./RegionOverlay";

const IMAGE = `${API}/claims/${CLAIM_ID}/pages/page-1/image`;

function renderImage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <PageImage claimId={CLAIM_ID} pageId="page-1" variant="full" alt="Page 1" />
    </QueryClientProvider>,
  );
}

describe("PageImage", () => {
  it("fetches the page into a blob URL and revokes it on leaving", async () => {
    const create = vi.fn(() => "blob:page-1");
    const revoke = vi.fn();
    Object.assign(URL, { createObjectURL: create, revokeObjectURL: revoke });
    let variant: string | null = null;
    server.use(
      http.get(IMAGE, ({ request }) => {
        variant = new URL(request.url).searchParams.get("variant");
        return HttpResponse.arrayBuffer(new Uint8Array([137, 80, 78, 71]).buffer, { headers: { "Content-Type": "image/png" } });
      }),
    );

    const { unmount } = renderImage();

    await waitFor(() => expect(screen.getByAltText("Page 1").getAttribute("src")).toBe("blob:page-1"));
    expect(variant).toBe("full");
    expect(revoke).not.toHaveBeenCalled();

    unmount();
    expect(revoke).toHaveBeenCalledWith("blob:page-1");
  });

  it("says when the page cannot be loaded", async () => {
    server.use(http.get(IMAGE, () => problem(404, "NOT_FOUND")));

    renderImage();

    expect(await screen.findByText("This page could not be loaded.")).toBeTruthy();
  });
});

describe("RegionOverlay", () => {
  it("draws the region in page coordinates, with its tag", () => {
    render(<RegionOverlay bbox={{ x: 0.1, y: 0.2, w: 0.3, h: 0.05 }} label="invoice_total · 0.62" tone="ai" />);

    const svg = screen.getByRole("img", { name: "Source region: invoice_total · 0.62" });
    expect(svg.getAttribute("viewBox")).toBe("0 0 1 1");
    const rect = svg.querySelector("rect")!;
    expect([rect.getAttribute("x"), rect.getAttribute("y"), rect.getAttribute("width"), rect.getAttribute("height")]).toEqual([
      "0.1",
      "0.2",
      "0.3",
      "0.05",
    ]);
    expect(screen.getByText("invoice_total · 0.62")).toBeTruthy();
  });

  it("is violet only for what the AI produced", () => {
    const { rerender } = render(<RegionOverlay bbox={{ x: 0, y: 0, w: 1, h: 1 }} label="x" tone="ai" />);
    expect(screen.getByRole("img").querySelector("rect")!.getAttribute("class")).toContain("stroke-source-highlight");

    rerender(<RegionOverlay bbox={{ x: 0, y: 0, w: 1, h: 1 }} label="x" tone="person" />);
    expect(screen.getByRole("img").querySelector("rect")!.getAttribute("class")).toContain("stroke-ink");
  });
});

describe("RegionDraw", () => {
  const surface = () => screen.getByRole("application", { name: "Draw the source region of Diagnosis" });

  function setup() {
    const onDraw = vi.fn();
    const onCancel = vi.fn();
    render(<RegionDraw fieldName="Diagnosis" onDraw={onDraw} onCancel={onCancel} />);
    vi.spyOn(surface(), "getBoundingClientRect").mockReturnValue({
      left: 100,
      top: 50,
      width: 400,
      height: 800,
    } as DOMRect);
    return { onDraw, onCancel };
  }

  it("reports a drag normalised to the page, whichever way it went", () => {
    const { onDraw } = setup();

    fireEvent.pointerDown(surface(), { clientX: 300, clientY: 450, pointerId: 1 });
    fireEvent.pointerMove(surface(), { clientX: 200, clientY: 250, pointerId: 1 });
    fireEvent.pointerUp(surface(), { clientX: 200, clientY: 250, pointerId: 1 });

    expect(onDraw).toHaveBeenCalledWith({ x: 0.25, y: 0.25, w: 0.25, h: 0.25 });
  });

  it("keeps a drag past the edge inside the page", () => {
    const { onDraw } = setup();

    fireEvent.pointerDown(surface(), { clientX: 300, clientY: 450, pointerId: 1 });
    fireEvent.pointerUp(surface(), { clientX: 900, clientY: 2000, pointerId: 1 });

    expect(onDraw).toHaveBeenCalledWith({ x: 0.5, y: 0.5, w: 0.5, h: 0.5 });
  });

  it("ignores a click, which is not a region", () => {
    const { onDraw } = setup();

    fireEvent.pointerDown(surface(), { clientX: 300, clientY: 450, pointerId: 1 });
    fireEvent.pointerUp(surface(), { clientX: 300, clientY: 450, pointerId: 1 });

    expect(onDraw).not.toHaveBeenCalled();
  });

  it("takes focus, so the keyboard path starts here", () => {
    setup();

    expect(document.activeElement).toBe(surface());
  });

  it("moves and resizes with the arrow keys, and Enter uses the box", async () => {
    const { onDraw } = setup();

    await userEvent.keyboard("{ArrowRight}{ArrowDown}{Shift>}{ArrowRight}{ArrowDown}{/Shift}{Enter}");

    expect(onDraw).toHaveBeenCalledWith({ x: 0.26, y: 0.26, w: 0.51, h: 0.09 });
  });

  it("Escape cancels", async () => {
    const { onCancel, onDraw } = setup();

    await userEvent.keyboard("{Escape}");

    expect(onCancel).toHaveBeenCalled();
    expect(onDraw).not.toHaveBeenCalled();
  });
});
