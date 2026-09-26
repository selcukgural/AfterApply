import { describe, expect, it } from "vitest";
import { newPostSeedFrom, newTranslationHref } from "./newPostSeed";

const id = "3f2c9d1e-5b6a-4c7d-8e9f-0a1b2c3d4e5f";

describe("newPostSeedFrom", () => {
  it("reads the language and the post to link from the add-translation address", () => {
    expect(newPostSeedFrom(new URLSearchParams(newTranslationHref("en", id).split("?")[1]))).toEqual({
      language: "en",
      translationOfPostId: id,
    });
  });

  it("is nothing without a language — a plain new post in the UI's language", () => {
    expect(newPostSeedFrom(new URLSearchParams(""))).toBeUndefined();
    expect(newPostSeedFrom(new URLSearchParams(`translationOf=${id}`))).toBeUndefined();
    expect(newPostSeedFrom(new URLSearchParams(`lang=de&translationOf=${id}`))).toBeUndefined();
  });

  it("keeps the language and drops a link that is not an id", () => {
    expect(newPostSeedFrom(new URLSearchParams("lang=tr"))).toEqual({ language: "tr", translationOfPostId: null });
    expect(newPostSeedFrom(new URLSearchParams("lang=tr&translationOf=../x"))).toEqual({ language: "tr", translationOfPostId: null });
    expect(newPostSeedFrom(new URLSearchParams("lang=tr&translationOf="))).toEqual({ language: "tr", translationOfPostId: null });
  });
});

describe("newTranslationHref", () => {
  it("opens a blog translation under the blog's editor by default", () => {
    expect(newTranslationHref("en", id)).toBe(`/admin/blog/new?lang=en&translationOf=${id}`);
  });

  it("opens a guide's translation under the guide's editor, so it is created as a guide", () => {
    expect(newTranslationHref("tr", id, "Guide")).toBe(`/admin/guide/new?lang=tr&translationOf=${id}`);
  });
});
