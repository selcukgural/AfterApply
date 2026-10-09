/**
 * "Whose 14:00 is this?" for an interview (canvas "İnce dokunuşlar — Paket 6", 5A): the zones a
 * company's country uses, and the conversion between a wall-clock time there and an instant. Pure,
 * built on Intl, so the browser's own time-zone database does the daylight-saving work.
 */

/**
 * The company's zone by its ISO country code (Company.Country). One entry where the country keeps
 * one clock for practical purposes; several where a recruiter could be in any of them — the user
 * picks. A country not listed asks with the full list.
 */
const COUNTRY_ZONES: Record<string, readonly string[]> = {
  TR: ["Europe/Istanbul"],
  DE: ["Europe/Berlin"],
  NL: ["Europe/Amsterdam"],
  GB: ["Europe/London"],
  IE: ["Europe/Dublin"],
  FR: ["Europe/Paris"],
  ES: ["Europe/Madrid"],
  PT: ["Europe/Lisbon"],
  IT: ["Europe/Rome"],
  BE: ["Europe/Brussels"],
  LU: ["Europe/Luxembourg"],
  CH: ["Europe/Zurich"],
  AT: ["Europe/Vienna"],
  PL: ["Europe/Warsaw"],
  CZ: ["Europe/Prague"],
  SK: ["Europe/Bratislava"],
  HU: ["Europe/Budapest"],
  SI: ["Europe/Ljubljana"],
  HR: ["Europe/Zagreb"],
  RS: ["Europe/Belgrade"],
  RO: ["Europe/Bucharest"],
  BG: ["Europe/Sofia"],
  GR: ["Europe/Athens"],
  CY: ["Asia/Nicosia"],
  UA: ["Europe/Kyiv"],
  SE: ["Europe/Stockholm"],
  NO: ["Europe/Oslo"],
  DK: ["Europe/Copenhagen"],
  FI: ["Europe/Helsinki"],
  EE: ["Europe/Tallinn"],
  LV: ["Europe/Riga"],
  LT: ["Europe/Vilnius"],
  IS: ["Atlantic/Reykjavik"],
  AZ: ["Asia/Baku"],
  GE: ["Asia/Tbilisi"],
  IL: ["Asia/Jerusalem"],
  AE: ["Asia/Dubai"],
  SA: ["Asia/Riyadh"],
  QA: ["Asia/Qatar"],
  EG: ["Africa/Cairo"],
  ZA: ["Africa/Johannesburg"],
  PK: ["Asia/Karachi"],
  IN: ["Asia/Kolkata"],
  SG: ["Asia/Singapore"],
  CN: ["Asia/Shanghai"],
  HK: ["Asia/Hong_Kong"],
  JP: ["Asia/Tokyo"],
  KR: ["Asia/Seoul"],
  NZ: ["Pacific/Auckland"],
  AR: ["America/Argentina/Buenos_Aires"],
  CO: ["America/Bogota"],
  CL: ["America/Santiago"],
  US: ["America/New_York", "America/Chicago", "America/Denver", "America/Phoenix", "America/Los_Angeles"],
  CA: ["America/Halifax", "America/Toronto", "America/Winnipeg", "America/Edmonton", "America/Vancouver"],
  AU: ["Australia/Sydney", "Australia/Brisbane", "Australia/Adelaide", "Australia/Perth"],
  BR: ["America/Sao_Paulo", "America/Manaus"],
  MX: ["America/Mexico_City", "America/Cancun", "America/Tijuana"],
  RU: ["Europe/Moscow", "Asia/Yekaterinburg", "Asia/Novosibirsk", "Asia/Vladivostok"],
  KZ: ["Asia/Almaty"],
  ID: ["Asia/Jakarta", "Asia/Makassar", "Asia/Jayapura"],
};

/** The zones to offer for a company in `country`; empty when the country is unknown or unlisted. */
export function zonesForCountry(country: string | null | undefined): readonly string[] {
  return country ? (COUNTRY_ZONES[country.toUpperCase()] ?? []) : [];
}

/** Every zone the browser knows, for a company of unknown or unlisted country. */
export function allZones(): string[] {
  const intl = Intl as typeof Intl & { supportedValuesOf?: (key: string) => string[] };
  return intl.supportedValuesOf ? intl.supportedValuesOf("timeZone") : Array.from(new Set(Object.values(COUNTRY_ZONES).flat())).sort();
}

/** The reader's own zone, as the browser reports it. */
export function readerZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone;
}

/** "Europe/Berlin" → "Berlin", "America/New_York" → "New York": the label a zone goes by. The zone
 *  database spells Istanbul without the dotted capital İ its own language uses. */
export function zoneCity(zone: string, locale = "en"): string {
  const city = zone.slice(zone.lastIndexOf("/") + 1).replace(/_/g, " ");
  return city === "Istanbul" && locale.startsWith("tr") ? "İstanbul" : city;
}

/** How far `zone` is ahead of UTC at `instant`, in minutes. */
export function zoneOffsetMinutes(zone: string, instant: Date): number {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone: zone,
    hourCycle: "h23",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  }).formatToParts(instant);
  const get = (type: string) => Number(parts.find((part) => part.type === type)?.value);
  const asUtc = Date.UTC(get("year"), get("month") - 1, get("day"), get("hour"), get("minute"), get("second"));
  return Math.round((asUtc - Math.floor(instant.getTime() / 1000) * 1000) / 60_000);
}

/**
 * The instant at which the clocks in `zone` read `date` `time` ("2026-10-14", "14:00"), as an ISO
 * string; null when either input is incomplete. The offset is read at a first guess and once more
 * at the result, which settles a guess that straddled a daylight-saving change.
 */
export function zonedTimeToInstant(date: string, time: string, zone: string): string | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date) || !/^\d{2}:\d{2}$/.test(time)) return null;
  const [year, month, day] = date.split("-").map(Number);
  const [hour, minute] = time.split(":").map(Number);
  const wall = Date.UTC(year, month - 1, day, hour, minute);
  if (Number.isNaN(wall)) return null;
  let at = wall - zoneOffsetMinutes(zone, new Date(wall)) * 60_000;
  at = wall - zoneOffsetMinutes(zone, new Date(at)) * 60_000;
  return new Date(at).toISOString();
}

/** "14:00": what the clocks in `zone` read at `iso`. */
export function timeInZone(iso: string, zone: string, locale: string): string {
  return new Date(iso).toLocaleTimeString(locale, { timeZone: zone, hour: "2-digit", minute: "2-digit" });
}

/**
 * Whether asking "whose time?" is worth it for a company with these zones: not when its only zone
 * keeps the reader's own clock on that day (a Turkish company for a reader in Istanbul).
 */
export function zoneQuestionNeeded(companyZones: readonly string[], ownZone: string, around: Date): boolean {
  if (companyZones.length !== 1) return companyZones.length > 1;
  return zoneOffsetMinutes(companyZones[0], around) !== zoneOffsetMinutes(ownZone, around);
}
