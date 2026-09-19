import { describe, expect, it } from "vitest";
import { blogAlternates, blogPostPath } from "./blogPaths";

describe("blogPostPath", () => {
  it("mounts a slug under /blog", () => {
    expect(blogPostPath("ise-alim")).toBe("/blog/ise-alim");
  });
});

describe("blogAlternates", () => {
  it("names only the language the post exists in, and makes it the default", () => {
    expect(blogAlternates({ language: "en", slug: "hello", translation: null }, "https://ekariyerim.com")).toEqual({
      en: "https://ekariyerim.com/en/blog/hello",
      "x-default": "https://ekariyerim.com/en/blog/hello",
    });
  });

  it("names both languages for a linked pair, with Turkish as the default", () => {
    expect(
      blogAlternates({ language: "en", slug: "hello", translation: { language: "tr", slug: "merhaba" } }),
    ).toEqual({
      en: "/en/blog/hello",
      tr: "/tr/blog/merhaba",
      "x-default": "/tr/blog/merhaba",
    });
  });
});
