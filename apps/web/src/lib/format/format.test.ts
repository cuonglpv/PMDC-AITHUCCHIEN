import { describe, expect, it } from "vitest";
import { formatDate, formatDateTime, formatMoney, formatRate, todayInVietnam } from ".";

describe("formats", () => {
  it("shows money as whole dong with separators", () => {
    expect(formatMoney(1_400_000)).toBe("1,400,000 ₫");
    expect(formatMoney(0)).toBe("0 ₫");
    expect(formatMoney(null)).toBe("—");
  });

  it("shows a business date as dd/MM/yyyy without moving it across time zones", () => {
    expect(formatDate("2026-01-01")).toBe("01/01/2026");
    expect(formatDate(null)).toBe("—");
  });

  it("shows a moment in Vietnam time", () => {
    expect(formatDateTime("2026-10-14T18:30:00Z")).toBe("15/10/2026 01:30");
  });

  it("takes today as the date in Vietnam, where business dates are kept", () => {
    expect(todayInVietnam(new Date("2025-12-31T16:59:00Z"))).toBe("2025-12-31");
    expect(todayInVietnam(new Date("2025-12-31T17:00:00Z"))).toBe("2026-01-01");
    expect(todayInVietnam(new Date("2026-01-01T16:59:00Z"))).toBe("2026-01-01");
  });

  it("shows basis points as a percentage", () => {
    expect(formatRate(8000)).toBe("80%");
    expect(formatRate(8050)).toBe("80.5%");
  });
});
