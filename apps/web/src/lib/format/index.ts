/** Money is integer VND with thousand separators and ₫; dates are dd/MM/yyyy (docs/04-information-architecture.md). */
const money = new Intl.NumberFormat("en-US", { maximumFractionDigits: 0 });

export function formatMoney(amount: number | null | undefined): string {
  return amount === null || amount === undefined ? "—" : `${money.format(amount)} ₫`;
}

/** Reads whole VND typed with or without separators: "1,400,000", "1.400.000" or "1400000 ₫". Null if it is not one. */
export function parseMoney(text: string): number | null {
  const typed = text.replace(/[\s₫]/g, "");
  // Plain digits, or groups of three split by one kind of separator. Anything after a decimal point is not whole VND.
  if (!/^(\d+|\d{1,3}(,\d{3})+|\d{1,3}(\.\d{3})+)$/.test(typed)) {
    return null;
  }
  const amount = Number(typed.replace(/[,.]/g, ""));
  return Number.isSafeInteger(amount) ? amount : null;
}

/** A business date, `YYYY-MM-DD`, shown as dd/MM/yyyy without any time zone conversion. */
export function formatDate(date: string | null | undefined): string {
  if (!date) {
    return "—";
  }
  const [year, month, day] = date.slice(0, 10).split("-");
  return `${day}/${month}/${year}`;
}

/**
 * Reads a business date typed as dd/MM/yyyy, the way it is written on Vietnamese forms, into `YYYY-MM-DD`.
 * Null if it is not a real calendar date. A native date input would show the browser's order instead (10/12 or 12/10).
 */
export function parseDate(text: string): string | null {
  const match = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(text.trim());
  if (!match) {
    return null;
  }
  const [day, month, year] = [Number(match[1]), Number(match[2]), Number(match[3])];
  const date = new Date(Date.UTC(year, month - 1, day));
  if (date.getUTCFullYear() !== year || date.getUTCMonth() !== month - 1 || date.getUTCDate() !== day) {
    return null;
  }
  return `${year}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
}

/** A period between two business dates, such as a policy's coverage. */
export function formatPeriod(from: string | null | undefined, to: string | null | undefined): string {
  return `${formatDate(from)} – ${formatDate(to)}`;
}

/** A file size in KB or MB, with one decimal from 1 MB. */
export function formatFileSize(bytes: number): string {
  if (bytes < 1024 * 1024) {
    return `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/** A moment, shown in Vietnam time, where every user works. */
export function formatDateTime(moment: string | null | undefined): string {
  if (!moment) {
    return "—";
  }
  const parts = new Intl.DateTimeFormat("en-GB", {
    timeZone: "Asia/Ho_Chi_Minh",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).formatToParts(new Date(moment));
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((item) => item.type === type)?.value ?? "";
  return `${part("day")}/${part("month")}/${part("year")} ${part("hour")}:${part("minute")}`;
}

/** Today's business date, `YYYY-MM-DD`, in Vietnam whatever the browser's time zone. The API uses the same day. */
export function todayInVietnam(now: Date = new Date()): string {
  const parts = new Intl.DateTimeFormat("en-GB", {
    timeZone: "Asia/Ho_Chi_Minh",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).formatToParts(now);
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((item) => item.type === type)?.value ?? "";
  return `${part("year")}-${part("month")}-${part("day")}`;
}

/** Basis points as a percentage: 8000 is 80%. */
export function formatRate(rateBp: number | null | undefined): string {
  return rateBp === null || rateBp === undefined ? "—" : `${rateBp / 100}%`;
}
