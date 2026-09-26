import { describe, expect, it } from "vitest";
import iconNodes from "lucide-static/icon-nodes.json";
import tags from "lucide-static/tags.json";
import { FEATURED_COVER_ICONS, TURKISH_ICON_ALIASES, iconForKeyword, searchCoverIcons, type CoverIconIndex } from "./coverIconSearch";

// The real, pinned set: a Lucide upgrade that renames an icon these lists name fails here.
const INDEX: CoverIconIndex = { names: Object.keys(iconNodes).sort(), tags: tags as Record<string, string[]> };

describe("the curated lists", () => {
  it("name only icons the pinned set has", () => {
    const known = new Set(INDEX.names);
    expect(FEATURED_COVER_ICONS.filter((name) => !known.has(name))).toEqual([]);
    const aliased = Object.values(TURKISH_ICON_ALIASES).flat();
    expect(aliased.filter((name) => !known.has(name))).toEqual([]);
  });
});

describe("searchCoverIcons", () => {
  it("shows the featured icons for an empty query", () => {
    const result = searchCoverIcons("  ", INDEX);
    expect(result.names).toEqual([...FEATURED_COVER_ICONS]);
  });

  it("finds Turkish words through the alias list, first", () => {
    expect(searchCoverIcons("maaş", INDEX).names.slice(0, 2)).toEqual(["banknote", "wallet"]);
    // A prefix of the word, and the word with a suffix, both reach it.
    expect(searchCoverIcons("müla", INDEX).names[0]).toBe("messages-square");
    expect(searchCoverIcons("maaşlar", INDEX).names[0]).toBe("banknote");
    // Turkish casing: "MÜLAKAT" folds to "mülakat", the dotted İ to i.
    expect(searchCoverIcons("MÜLAKAT", INDEX).names[0]).toBe("messages-square");
    expect(searchCoverIcons("İlan", INDEX).names[0]).toBe("megaphone");
  });

  it("finds English names and Lucide's tags", () => {
    expect(searchCoverIcons("calendar", INDEX).names).toContain("calendar-check");
    // "money" is a tag of banknote, not part of its name.
    expect(searchCoverIcons("money", INDEX).names).toContain("banknote");
  });

  it("caps the list and reports the full count, without repeats", () => {
    const result = searchCoverIcons("a", INDEX, 20);
    expect(result.names).toHaveLength(20);
    expect(result.total).toBeGreaterThan(20);
    expect(new Set(result.names).size).toBe(result.names.length);
  });

  it("finds nothing for nonsense", () => {
    expect(searchCoverIcons("qqzzxx", INDEX)).toEqual({ names: [], total: 0 });
  });
});

describe("iconForKeyword", () => {
  it("takes an exact name first, else the search's first hit", () => {
    expect(iconForKeyword("mail", INDEX)).toBe("mail");
    expect(iconForKeyword("bell off", INDEX)).toBe("bell-off");
    expect(iconForKeyword("money", INDEX)).not.toBeNull();
    expect(iconForKeyword("qqzzxx", INDEX)).toBeNull();
    expect(iconForKeyword(null, INDEX)).toBeNull();
  });
});
