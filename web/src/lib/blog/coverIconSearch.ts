/**
 * Finding an icon for a cover (DECISIONS.md 2026-09-27). The set is Lucide's, whose names and tags
 * are English; an admin types Turkish. So a short hand-made list maps the words this product's
 * posts are about (maaş, mülakat, ret…) to icons, and after that the English names and tags are
 * searched as Lucide ships them. The same search turns the SEO suggestion's icon keyword into an
 * icon, so what the model proposes is always something the picker could have found.
 *
 * Pure: the picker hands it the loaded set, the tests hand it a small one.
 */

/** What the picker shows before anything is typed: the icons this product's posts reach for. */
export const FEATURED_COVER_ICONS: readonly string[] = [
  "mail", "inbox", "send", "clock", "hourglass", "calendar", "calendar-check", "bell", "list-checks", "clipboard-list",
  "file-text", "file-user", "search", "scan-search", "chart-column", "chart-line", "trending-up", "trending-down",
  "battery-low", "coffee", "repeat", "target", "flag", "route", "signpost", "briefcase", "building", "factory", "users",
  "messages-square", "mic", "video", "phone", "banknote", "wallet", "handshake", "circle-check", "circle-x", "thumbs-up",
  "lightbulb", "graduation-cap", "globe", "house", "laptop", "shield", "lock", "sparkles", "rocket", "puzzle", "bell-off",
];

/**
 * Turkish words → icons, most fitting first. Keys are lower-cased the Turkish way; a query matches
 * a key it starts (typing "müla" finds "mülakat"), or one it starts with (a suffixed "maaşlar" finds
 * "maaş"). An icon missing from the loaded set is skipped, so a Lucide rename costs a result, not
 * an error.
 */
export const TURKISH_ICON_ALIASES: Readonly<Record<string, readonly string[]>> = {
  maaş: ["banknote", "wallet", "hand-coins", "coins", "piggy-bank", "circle-dollar-sign"],
  para: ["banknote", "wallet", "coins", "hand-coins", "piggy-bank"],
  ücret: ["banknote", "wallet", "coins"],
  teklif: ["handshake", "signature", "file-pen-line", "badge-check", "banknote"],
  sözleşme: ["signature", "file-pen-line", "handshake"],
  mülakat: ["messages-square", "message-circle", "mic", "video", "users", "phone"],
  görüşme: ["messages-square", "message-circle", "phone", "video", "users"],
  başvuru: ["send", "mail", "file-text", "inbox", "clipboard-list"],
  "e-posta": ["mail", "mail-open", "inbox", "at-sign"],
  eposta: ["mail", "mail-open", "inbox", "at-sign"],
  posta: ["mail", "mail-open", "inbox"],
  cv: ["file-text", "file-user", "id-card", "scroll-text"],
  özgeçmiş: ["file-text", "file-user", "id-card", "scroll-text"],
  ilan: ["megaphone", "newspaper", "clipboard-list", "list-checks"],
  nitelik: ["list-checks", "badge-check", "graduation-cap"],
  bekleme: ["clock", "hourglass", "timer", "calendar-clock"],
  süre: ["clock", "hourglass", "timer"],
  zaman: ["clock", "hourglass", "timer", "calendar-clock"],
  takvim: ["calendar", "calendar-check", "calendar-clock"],
  hatırlatma: ["bell", "alarm-clock", "bell-ring"],
  red: ["circle-x", "thumbs-down", "ban", "x"],
  ret: ["circle-x", "thumbs-down", "ban", "x"],
  olumsuz: ["circle-x", "thumbs-down"],
  kabul: ["circle-check", "thumbs-up", "party-popper", "badge-check"],
  olumlu: ["circle-check", "thumbs-up", "party-popper"],
  şirket: ["building", "building-complex", "factory", "briefcase"],
  firma: ["building", "building-complex", "factory", "briefcase"],
  iş: ["briefcase", "briefcase-business"],
  kariyer: ["route", "signpost", "milestone", "trending-up"],
  ekip: ["users", "user-round", "contact"],
  ik: ["users", "user-round", "contact"],
  insan: ["users", "user-round"],
  arama: ["search", "scan-search", "telescope"],
  veri: ["chart-column", "chart-line", "chart-pie", "trending-up", "trending-down"],
  oran: ["chart-column", "chart-pie", "chart-line"],
  istatistik: ["chart-column", "chart-line", "chart-pie"],
  yorgunluk: ["battery-low", "battery", "coffee", "bed"],
  tükenmişlik: ["battery-low", "flame", "bed"],
  motivasyon: ["flame", "rocket", "sparkles", "target"],
  düzen: ["repeat", "list-todo", "calendar-check", "refresh-cw"],
  rutin: ["repeat", "list-todo", "refresh-cw"],
  hedef: ["target", "flag", "goal"],
  eğitim: ["graduation-cap", "book-open", "lightbulb"],
  okul: ["graduation-cap", "book-open"],
  fikir: ["lightbulb", "sparkles"],
  ipucu: ["lightbulb", "sparkles"],
  soru: ["circle-question-mark", "message-circle-question-mark"],
  uyarı: ["triangle-alert", "shield-alert"],
  gizlilik: ["shield", "lock", "eye-off"],
  güvenlik: ["shield", "lock", "shield-alert"],
  ağ: ["network", "share-2", "link"],
  yol: ["route", "signpost", "milestone"],
  sessizlik: ["bell-off", "message-circle-off", "volume-x"],
  cevapsız: ["bell-off", "message-circle-off", "volume-x"],
  eklenti: ["puzzle"],
  yurtdışı: ["globe", "plane", "map-pin"],
  uzaktan: ["house", "laptop", "wifi"],
  ev: ["house"],
};

export interface CoverIconIndex {
  /** Every icon's name, in the order results should fall back to (the set's own, alphabetical). */
  names: readonly string[];
  /** Lucide's `tags.json`: each icon's English search words. */
  tags: Readonly<Record<string, readonly string[]>>;
}

function fold(value: string): string {
  return value.trim().toLocaleLowerCase("tr");
}

/**
 * The picker's search. Empty query: the featured list. Otherwise, in this order and without
 * repeats — Turkish alias matches, names containing the query, tags starting with it.
 */
export function searchCoverIcons(query: string, index: CoverIconIndex, limit = 150): { names: string[]; total: number } {
  const known = new Set(index.names);
  const q = fold(query);
  if (!q) {
    const featured = FEATURED_COVER_ICONS.filter((name) => known.has(name));
    return { names: featured.slice(0, limit), total: featured.length };
  }

  const seen = new Set<string>();
  const found: string[] = [];
  const add = (name: string) => {
    if (known.has(name) && !seen.has(name)) {
      seen.add(name);
      found.push(name);
    }
  };

  for (const [word, icons] of Object.entries(TURKISH_ICON_ALIASES)) {
    if (word.startsWith(q) || (q.length >= 3 && q.startsWith(word))) icons.forEach(add);
  }
  // English matching on the ASCII form: Lucide's words are ASCII, and "İ" folded Turkish-style is
  // "i" already, so this is only the lower-casing the English side expects.
  const ascii = q.toLowerCase();
  for (const name of index.names) if (name.includes(ascii)) add(name);
  for (const name of index.names) if ((index.tags[name] ?? []).some((tag) => tag.toLowerCase().startsWith(ascii))) add(name);

  return { names: found.slice(0, limit), total: found.length };
}

/** The SEO suggestion's icon keyword as an icon: the picker's first hit for it, or null. */
export function iconForKeyword(keyword: string | null | undefined, index: CoverIconIndex): string | null {
  if (!keyword?.trim()) return null;
  // An exact name first ("mail" is the icon mail, not the first icon whose tags start with "mail").
  const exact = keyword.trim().toLowerCase().replace(/\s+/g, "-");
  if (index.names.includes(exact)) return exact;
  return searchCoverIcons(keyword, index, 1).names[0] ?? null;
}
