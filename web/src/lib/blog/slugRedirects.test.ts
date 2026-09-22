import { describe, expect, it } from "vitest";
import { BLOG_SLUG_REDIRECTS, blogSlugRedirectForPath } from "./slugRedirects";

describe("blogSlugRedirectForPath", () => {
  it("sends the misspelled English motivation post to its corrected slug", () => {
    expect(blogSlugRedirectForPath("/en/blog/how-can-i-kep-my-motivation-while-job-searching")).toBe(
      "/en/blog/how-can-i-keep-my-motivation-while-job-searching",
    );
    expect(blogSlugRedirectForPath("/en/blog/how-can-i-kep-my-motivation-while-job-searching/")).toBe(
      "/en/blog/how-can-i-keep-my-motivation-while-job-searching",
    );
  });

  it("only redirects within the language the old slug lived in", () => {
    expect(blogSlugRedirectForPath("/tr/blog/how-can-i-kep-my-motivation-while-job-searching")).toBeNull();
  });

  it("leaves the corrected slug, other posts and other sections alone", () => {
    expect(blogSlugRedirectForPath("/en/blog/how-can-i-keep-my-motivation-while-job-searching")).toBeNull();
    expect(blogSlugRedirectForPath("/tr/blog/is-ararken-motivasyonumu-nasil-koruyabilirim")).toBeNull();
    expect(blogSlugRedirectForPath("/en/blog")).toBeNull();
    expect(blogSlugRedirectForPath("/en/guide/how-can-i-kep-my-motivation-while-job-searching")).toBeNull();
  });

  it("never points an old slug at itself or at another old slug", () => {
    for (const table of Object.values(BLOG_SLUG_REDIRECTS)) {
      for (const [from, to] of Object.entries(table)) {
        expect(to).not.toBe(from);
        expect(table[to]).toBeUndefined();
      }
    }
  });
});
