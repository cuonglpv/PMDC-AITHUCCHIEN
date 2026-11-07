/** Money is integer VND with thousand separators and ₫; dates are dd/MM/yyyy (docs/04-information-architecture.md). */
const money = new Intl.NumberFormat("en-US", { maximumFractionDigits: 0 });

export function formatMoney(amount: number | null | undefined): string {
  return amount === null || amount === undefined ? "—" : `${money.format(amount)} ₫`;
}

/** A business date, `YYYY-MM-DD`, shown as dd/MM/yyyy without any time zone conversion. */
export function formatDate(date: string | null | undefined): string {
  if (!date) {
    return "—";
  }
  const [year, month, day] = date.slice(0, 10).split("-");
  return `${day}/${month}/${year}`;
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
