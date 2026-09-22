import type { BenchmarkSource } from "@/types/api";

/**
 * Which campaign channel brought a visitor to the benchmark, read from the link's `utm_source`.
 *
 * The threshold is being crossed with a one-off community push (growth research 2026-09-21, item
 * 0.1), and which channel actually brings answers is the one thing worth learning from it. The
 * query string is untrusted text, so it is only ever mapped onto the fixed list the API accepts —
 * anything else, including a missing parameter, is `null` and the answer is sent without a channel.
 */

// Lower-case aliases a hand-written campaign link might plausibly use, onto the API's enum.
const ALIASES: Readonly<Record<string, BenchmarkSource>> = {
  x: "X",
  twitter: "X",
  eksi: "Eksi",
  eksisozluk: "Eksi",
  reddit: "Reddit",
  discord: "Discord",
  linkedin: "LinkedIn",
  whatsapp: "WhatsApp",
  share: "Share",
};

export function benchmarkSourceFromSearch(search: string): BenchmarkSource | null {
  const raw = new URLSearchParams(search).get("utm_source");
  if (!raw) return null;

  const key = raw.trim().toLowerCase();
  // Own keys only, so "constructor" or "__proto__" in a link never resolves to something.
  return Object.hasOwn(ALIASES, key) ? ALIASES[key] : null;
}

/** Every channel the page can send — kept in step with the C# enum by options.test.ts. */
export const BENCHMARK_SOURCES: BenchmarkSource[] = [...new Set(Object.values(ALIASES))];
