import { describe, expect, it } from "vitest";
import iconNodes from "lucide-static/icon-nodes.json";
import {
  COVER_TEXT_MAX,
  COVER_TONES,
  coverTone,
  DEFAULT_COVER_ICON,
  coverText,
  coverTextSize,
  isCoverIconName,
  resolveCoverIcon,
  type CoverIconNode,
} from "./coverCard";

const NODES = iconNodes as unknown as Record<string, CoverIconNode>;

describe("coverText", () => {
  it("prints the cover line when there is one, else the title", () => {
    expect(coverText("Kariyer.net'te “Başvurun İletildi” Ne Demek?", "İletildi ≠ okundu")).toBe("İletildi ≠ okundu");
    expect(coverText("Başlık", null)).toBe("Başlık");
    expect(coverText("Başlık", "   ")).toBe("Başlık");
  });

  it("folds whitespace and cuts a long title on a word boundary", () => {
    expect(coverText("  Bir\n\niki   üç ", null)).toBe("Bir iki üç");
    const long = coverText("kelime ".repeat(40), null);
    expect(long.length).toBeLessThanOrEqual(COVER_TEXT_MAX + 1);
    expect(long.endsWith("kelime…")).toBe(true);
  });
});

describe("coverTextSize", () => {
  it("sets a short line large and a title-length one smaller", () => {
    expect(coverTextSize("İletildi ≠ okundu")).toBe(80);
    expect(coverTextSize("Yoran zaman değil, zihinsel yük")).toBe(72);
    expect(coverTextSize("a".repeat(60))).toBe(56);
    expect(coverTextSize("a".repeat(85))).toBe(44);
    expect(coverTextSize("a".repeat(110))).toBe(36);
  });
});

describe("coverTone", () => {
  it("gives the first posts' icons the colour of their uploaded covers", () => {
    expect(coverTone("mail")).toEqual(COVER_TONES[0]);
    expect(coverTone("list-checks")).toEqual(COVER_TONES[1]);
    expect(coverTone("battery-low")).toEqual(COVER_TONES[2]);
    expect(coverTone("hourglass")).toEqual(COVER_TONES[3]);
    expect(coverTone("repeat")).toEqual(COVER_TONES[4]);
  });

  it("is the same for the same icon, so a post and its translation match", () => {
    expect(coverTone("calendar-check")).toBe(coverTone("calendar-check"));
    expect(COVER_TONES).toContain(coverTone("calendar-check"));
  });

  it("treats no icon, an unknown shape and a prototype name as the default icon", () => {
    expect(coverTone(null)).toBe(coverTone(DEFAULT_COVER_ICON));
    expect(coverTone("Not An Icon")).toBe(coverTone(DEFAULT_COVER_ICON));
    expect(COVER_TONES).toContain(coverTone("constructor"));
  });

  it("spreads different icons over more than one colour", () => {
    const names = ["calendar", "clock", "banknote", "users", "building", "search", "flag", "target", "newspaper", "send"];
    expect(new Set(names.map((n) => coverTone(n).sun)).size).toBeGreaterThan(2);
  });
});

describe("isCoverIconName", () => {
  it("accepts the set's name shape only", () => {
    expect(isCoverIconName("mail")).toBe(true);
    expect(isCoverIconName("chart-column")).toBe(true);
    expect(isCoverIconName("Mail")).toBe(false);
    expect(isCoverIconName("../mail")).toBe(false);
    expect(isCoverIconName("constructor")).toBe(true);
    expect(isCoverIconName(null)).toBe(false);
    expect(isCoverIconName("a".repeat(65))).toBe(false);
  });
});

describe("resolveCoverIcon", () => {
  it("draws the chosen icon, and the default for none or an unknown name", () => {
    expect(resolveCoverIcon(NODES, "mail")).toBe(NODES.mail);
    expect(resolveCoverIcon(NODES, null)).toBe(NODES[DEFAULT_COVER_ICON]);
    expect(resolveCoverIcon(NODES, "not-an-icon-anywhere")).toBe(NODES[DEFAULT_COVER_ICON]);
    // A name shaped right but inherited from Object's prototype is not an icon.
    expect(resolveCoverIcon(NODES, "constructor")).toBe(NODES[DEFAULT_COVER_ICON]);
  });

  it("has the default icon in the pinned set", () => {
    expect(NODES[DEFAULT_COVER_ICON]).toBeDefined();
  });
});
