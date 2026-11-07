import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { OFFICER } from "@/test/intake";
import { ConfidenceBadge } from "./ConfidenceBadge";
import { PersonMarker } from "./PersonMarker";

describe("ConfidenceBadge", () => {
  it.each([
    ["HIGH", "High", "check_circle"],
    ["CHECK", "Check", "visibility"],
    ["MISSING", "Missing", "radio_button_unchecked"],
  ] as const)("names the %s level in words, with an icon kept from screen readers", (level, word, icon) => {
    const { container } = render(<ConfidenceBadge level={level} confidence={0.8} />);

    expect(screen.getByText(word)).toBeTruthy();
    expect(container.textContent).toBe(`${icon}Confidence: ${word}`);
    expect(container.querySelector("[aria-hidden='true']")?.textContent).toBe(icon);
  });

  it("puts the raw score in the tooltip, as the API sent it", () => {
    const { container } = render(<ConfidenceBadge level="HIGH" confidence={0.974} />);
    expect(container.firstElementChild?.getAttribute("title")).toBe("Confidence 0.97");
  });

  it("has no tooltip without a score", () => {
    const { container } = render(<ConfidenceBadge level="CHECK" />);
    expect(container.firstElementChild?.hasAttribute("title")).toBe(false);
  });
});

describe("PersonMarker", () => {
  it("shows the initials of whoever set the value, and their name on hover", () => {
    render(<PersonMarker person={OFFICER} />);

    const chip = screen.getByText("PB");
    expect(chip.textContent).toBe("Set by PB");
    expect(chip.getAttribute("title")).toBe("Set by Phạm Gia Bảo");
  });
});
