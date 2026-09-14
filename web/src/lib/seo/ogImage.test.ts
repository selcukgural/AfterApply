import { describe, expect, it } from "vitest";
import { OG_KICKER_MAX_LENGTH, OG_TITLE_MAX_LENGTH, ogImagePath, sanitizeOgText } from "./ogImage";

/**
 * The title reaches the image route through a public query string, so what the route draws is
 * whatever these two functions let through. The clamps are the whole defence.
 */
describe("sanitizeOgText", () => {
  it("passes an ordinary title through, Turkish letters included", () => {
    expect(sanitizeOgText("Kaç iş başvurusu yapmak gerekir?", OG_TITLE_MAX_LENGTH)).toBe("Kaç iş başvurusu yapmak gerekir?");
  });

  it("collapses whitespace and strips control characters", () => {
    expect(sanitizeOgText("  a\n\nb\t c\u0007d ", OG_TITLE_MAX_LENGTH)).toBe("a b c d");
  });

  it("answers an empty string for nothing", () => {
    expect(sanitizeOgText(null, 10)).toBe("");
    expect(sanitizeOgText(undefined, 10)).toBe("");
    expect(sanitizeOgText("   ", 10)).toBe("");
  });

  it("clamps a long title on a word boundary and marks the cut", () => {
    const words = Array.from({ length: 40 }, (_, i) => `kelime${i}`);
    const clamped = sanitizeOgText(words.join(" "), OG_TITLE_MAX_LENGTH);

    expect(clamped.length).toBeLessThanOrEqual(OG_TITLE_MAX_LENGTH + 1);
    expect(clamped.endsWith("…")).toBe(true);
    // Cut between words, not inside one: every word kept is a whole word from the input.
    for (const word of clamped.slice(0, -1).split(" ")) {
      expect(words).toContain(word);
    }
  });

  it("still cuts a single unbroken word rather than letting it through", () => {
    const clamped = sanitizeOgText("x".repeat(500), 20);

    expect(clamped).toHaveLength(21);
    expect(clamped.endsWith("…")).toBe(true);
  });
});

describe("ogImagePath", () => {
  it("builds the locale-prefixed route with the title in the query", () => {
    const url = new URL(ogImagePath("tr", "Rehber yazısı", "Rehber"), "https://ekariyerim.com");

    expect(url.pathname).toBe("/tr/og");
    expect(url.searchParams.get("t")).toBe("Rehber yazısı");
    expect(url.searchParams.get("k")).toBe("Rehber");
  });

  it("leaves the kicker out when there is none", () => {
    const url = new URL(ogImagePath("en", "CV Scan"), "https://ekariyerim.com");

    expect(url.searchParams.has("k")).toBe(false);
  });

  it("never emits a title or kicker longer than the renderer's clamp", () => {
    const url = new URL(ogImagePath("tr", "a ".repeat(400), "b ".repeat(200)), "https://ekariyerim.com");

    expect(url.searchParams.get("t")!.length).toBeLessThanOrEqual(OG_TITLE_MAX_LENGTH + 1);
    expect(url.searchParams.get("k")!.length).toBeLessThanOrEqual(OG_KICKER_MAX_LENGTH + 1);
  });
});
