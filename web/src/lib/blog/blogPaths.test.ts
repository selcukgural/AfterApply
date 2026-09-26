import { describe, expect, it } from "vitest";
import { adminPostsPath, blogAlternates, blogPostPath, postPath, postPreviewPath } from "./blogPaths";

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

describe("kind-aware paths (2026-09-26)", () => {
  it("puts a guide under /guide and a blog post under /blog, for the page and the preview", () => {
    expect(postPath("Blog", "x")).toBe("/blog/x");
    expect(postPath("Guide", "x")).toBe("/guide/x");
    expect(postPreviewPath("Blog", "id")).toBe("/blog/preview/id");
    expect(postPreviewPath("Guide", "id")).toBe("/guide/preview/id");
  });

  it("gives each kind its own admin table", () => {
    expect(adminPostsPath("Blog")).toBe("/admin/blog");
    expect(adminPostsPath("Guide")).toBe("/admin/guide");
  });
});
