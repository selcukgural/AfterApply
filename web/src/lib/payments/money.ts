/**
 * Kuruş to a currency string in the viewer's locale. PayTR's currency code is "TL"; Intl wants
 * the ISO code, and both mean Turkish lira.
 */
export function formatMinor(amountMinor: number, currency: string, locale: string): string {
  const iso = currency === "TL" ? "TRY" : currency;
  return new Intl.NumberFormat(locale === "en" ? "en-GB" : "tr-TR", {
    style: "currency",
    currency: iso,
    currencyDisplay: "narrowSymbol",
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(amountMinor / 100);
}

/** "yyyy-MM" → a month name in the viewer's locale ("Eylül 2026" / "September 2026"). */
export function formatMonth(month: string, locale: string): string {
  const [year, m] = month.split("-").map(Number);
  if (!year || !m) {
    return month;
  }
  return new Intl.DateTimeFormat(locale === "en" ? "en-GB" : "tr-TR", { month: "long", year: "numeric" }).format(
    new Date(Date.UTC(year, m - 1, 15)),
  );
}
