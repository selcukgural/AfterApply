import { describe, expect, it } from "vitest";
import iconNodes from "lucide-static/icon-nodes.json";
import {
  COVER_TEXT_MAX,
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
    expect(coverTextSize("İletildi ≠ okundu")).toBe(88);
    expect(coverTextSize("Yoran zaman değil, zihinsel yük")).toBe(76);
    expect(coverTextSize("a".repeat(60))).toBe(64);
    expect(coverTextSize("a".repeat(85))).toBe(54);
    expect(coverTextSize("a".repeat(110))).toBe(46);
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
