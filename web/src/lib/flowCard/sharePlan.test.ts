import { describe, expect, it } from "vitest";
import tr from "../../../messages/tr.json";
import { SHARE_OUTPUTS, SHARE_PLATFORMS, outputSize } from "./sharePlan";

describe("the share dialog's plan", () => {
  it("gives every network at least one output, and a note the catalogue has", () => {
    const notes = tr.flowCard.share.notes as Record<string, string>;
    for (const platform of SHARE_PLATFORMS) {
      expect(SHARE_OUTPUTS[platform].length).toBeGreaterThan(0);
      for (const output of SHARE_OUTPUTS[platform]) expect(notes[output.note]).toBeTruthy();
    }
  });

  it("never offers Instagram a link, since links in its posts do not open", () => {
    expect(SHARE_OUTPUTS.instagram.some((output) => output.kind === "link")).toBe(false);
  });

  it("sizes each network's post the way the canvas decided", () => {
    expect(outputSize(SHARE_OUTPUTS.linkedin[0])).toBe("1080×1350");
    expect(outputSize(SHARE_OUTPUTS.x[0])).toBe("1600×900");
    expect(outputSize(SHARE_OUTPUTS.facebook[1])).toBe("1080×1920");
    expect(SHARE_OUTPUTS.whatsapp[0].kind).toBe("link");
  });

  it("reports the downloaded file at twice the network's size", () => {
    expect(outputSize(SHARE_OUTPUTS.linkedin[0], 2)).toBe("2160×2700");
  });
});
