import { describe, expect, it } from "vitest";
import { hasDraftText, textOf } from "./draftText";

describe("hasDraftText", () => {
  it.each([
    ["", null, ""],
    [" \t\n", "  ", "<p></p>"],
    ["", "", "<p>&nbsp;</p>"],
    ["", "", "<h2></h2><ul><li><p> </p></li></ul>"],
    ["", "", "<p><strong></strong><em> </em></p>"],
  ])("is false with nothing written (%j, %j, %j)", (title, excerpt, contentHtml) => {
    expect(hasDraftText({ title, excerpt, contentHtml })).toBe(false);
  });

  it.each([
    ["a", "", ""],
    ["", "a", ""],
    ["", "", "<p>a</p>"],
    ["", "", "<p>&amp;</p>"],
    ["", "", "<p><strong>a</strong></p>"],
    ["", "", "<h2>Başlık</h2>"],
  ])("is true with one character anywhere (%j, %j, %j)", (title, excerpt, contentHtml) => {
    expect(hasDraftText({ title, excerpt, contentHtml })).toBe(true);
  });
});

describe("textOf", () => {
  it("drops tags and keeps the text between them", () => {
    expect(textOf("<p>a</p><p>b</p>").replace(/\s+/g, " ").trim()).toBe("a b");
  });
});
