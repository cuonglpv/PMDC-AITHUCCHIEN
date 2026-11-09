import { describe, expect, it } from "vitest";
import {
  formatAge,
  formatDate,
  formatDateTime,
  formatFileSize,
  formatMoney,
  formatPeriod,
  formatRate,
  parseDate,
  parseMoney,
  todayInVietnam,
} from ".";

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

  it("reads whole dong typed with or without separators", () => {
    expect(parseMoney("1,400,000")).toBe(1_400_000);
    expect(parseMoney("1.400.000 ₫")).toBe(1_400_000);
    expect(parseMoney(" 350000 ")).toBe(350_000);
    expect(parseMoney("1,400,000.50")).toBeNull();
    expect(parseMoney("12a")).toBeNull();
    expect(parseMoney("")).toBeNull();
  });

  it("reads a date typed as dd/MM/yyyy, and only a real one", () => {
    expect(parseDate("12/10/2026")).toBe("2026-10-12");
    expect(parseDate(" 5/1/2027 ")).toBe("2027-01-05");
    expect(parseDate("29/02/2028")).toBe("2028-02-29");
    expect(parseDate("31/02/2026")).toBeNull();
    expect(parseDate("2026-10-12")).toBeNull();
    expect(parseDate("")).toBeNull();
  });

  it("shows a coverage period and a file size", () => {
    expect(formatPeriod("2026-01-01", "2026-12-31")).toBe("01/01/2026 – 31/12/2026");
    expect(formatFileSize(2_048)).toBe("2 KB");
    expect(formatFileSize(5_452_595)).toBe("5.2 MB");
  });
});

describe("formatAge", () => {
  const now = Date.parse("2026-10-17T12:00:00Z");
  const ago = (minutes: number) => new Date(now - minutes * 60_000).toISOString();

  it.each([
    [0, "<1m"],
    [35, "35m"],
    [8 * 60, "8h"],
    [24 * 60, "1d"],
    [27 * 60, "1d 3h"],
    [53 * 60, "2d 5h"],
  ])("shows %i minutes as %s", (minutes, shown) => {
    expect(formatAge(ago(minutes), now)).toBe(shown);
  });

  it("does not go negative when the clocks differ", () => {
    expect(formatAge(ago(-5), now)).toBe("<1m");
  });
});
