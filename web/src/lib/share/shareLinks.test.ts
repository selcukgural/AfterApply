import { describe, expect, it } from "vitest";
import { SHARE_TARGETS, shareClipboardText, shareHref } from "./shareLinks";

const content = { text: "CV'm 80/100 aldı. Seninki kaç?", url: "https://ekariyerim.com/tr/cv-tarama" };

describe("shareHref", () => {
  it("points every target at that network's own share page, never at a script", () => {
    for (const target of SHARE_TARGETS) {
      const href = shareHref(target, content);
      expect(href).toMatch(/^https:\/\/(www\.linkedin\.com|wa\.me|twitter\.com)\//);
    }
  });

  it("gives LinkedIn only the URL — it reads the title from the page's Open Graph card", () => {
    expect(shareHref("linkedin", content)).toBe(
      "https://www.linkedin.com/sharing/share-offsite/?url=https%3A%2F%2Fekariyerim.com%2Ftr%2Fcv-tarama",
    );
  });

  it("gives WhatsApp the sentence and the link as one message", () => {
    const href = shareHref("whatsapp", content);
    expect(href.startsWith("https://wa.me/?text=")).toBe(true);
    expect(decodeURIComponent(href.slice("https://wa.me/?text=".length))).toBe(
      "CV'm 80/100 aldı. Seninki kaç? https://ekariyerim.com/tr/cv-tarama",
    );
  });

  it("gives X the sentence and the link as separate fields", () => {
    const href = new URL(shareHref("x", content));
    expect(href.searchParams.get("text")).toBe(content.text);
    expect(href.searchParams.get("url")).toBe(content.url);
  });

  it("encodes characters that would otherwise break the query", () => {
    const tricky = { text: "a&b=c #tag", url: "https://ekariyerim.com/tr/companies/a-b" };
    const href = new URL(shareHref("x", tricky));
    expect(href.searchParams.get("text")).toBe("a&b=c #tag");
  });
});

describe("shareClipboardText", () => {
  it("is the link alone — the button says \"link\", the sentence belongs to the share targets", () => {
    expect(shareClipboardText(content)).toBe("https://ekariyerim.com/tr/cv-tarama");
  });
});
