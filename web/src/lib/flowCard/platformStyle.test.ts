import { describe, expect, it } from "vitest";
import { BRAND_GRADIENT_STOPS, PLATFORM_STYLE } from "./platformStyle";
import { SHARE_PLATFORMS } from "./sharePlan";

describe("PLATFORM_STYLE", () => {
  it("styles every platform the dialog offers, and nothing else", () => {
    expect(Object.keys(PLATFORM_STYLE).sort()).toEqual([...SHARE_PLATFORMS].sort());
  });

  // The mark on the neutral circle and the fill of the picked circle are the same brand colour;
  // one edited without the other would make a circle change colour on selection.
  it("fills a picked circle with the same colour its mark is drawn in", () => {
    for (const platform of SHARE_PLATFORMS) {
      const { mark, picked } = PLATFORM_STYLE[platform];
      if (mark.startsWith("#")) expect(picked, platform).toContain(`bg-[${mark}]`);
    }
    for (const stop of BRAND_GRADIENT_STOPS) expect(PLATFORM_STYLE.instagram.picked).toContain(stop);
  });

  // X is black-on-white or white-on-black; a fixed colour would disappear in one of the themes.
  it("draws X in the theme's ink and flips its picked circle with the theme", () => {
    expect(PLATFORM_STYLE.x.mark).toBe("ink");
    expect(PLATFORM_STYLE.x.picked).toContain("dark:bg-white");
    expect(PLATFORM_STYLE.x.picked).toContain("dark:text-black");
  });

  it("puts a legible mark colour on every picked fill", () => {
    for (const platform of SHARE_PLATFORMS) {
      expect(PLATFORM_STYLE[platform].picked, platform).toMatch(/\btext-(white|black)\b/);
    }
  });
});
