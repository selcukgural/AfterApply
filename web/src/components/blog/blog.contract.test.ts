import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { describe, expect, it } from "vitest";

const root = join(__dirname, "../..");

function read(path: string): string {
  return readFileSync(join(root, path), "utf8");
}

function walk(dir: string): string[] {
  return readdirSync(dir).flatMap((entry) => {
    const path = join(dir, entry);
    return statSync(path).isDirectory() ? walk(path) : [path];
  });
}

/**
 * The blog's security contract (DECISIONS.md 2026-09-19). The body of a post is HTML the API
 * sanitized and the page renders raw; the rules below are what keep that the only such place and
 * keep the rendering server-side.
 */
describe("raw HTML on the site", () => {
  it("is written by exactly the files that justify it", () => {
    const users = walk(root)
      .filter((path) => /\.tsx?$/.test(path) && !/\.test\.tsx?$/.test(path))
      // The attribute as written in JSX — a comment that names it (CvScanFindings says "never") is
      // not a use.
      .filter((path) => /dangerouslySetInnerHTML=\{/.test(read(relative(root, path))))
      .map((path) => relative(root, path))
      .sort();

    expect(users).toEqual([
      // The theme boot script — a constant of ours, not content.
      "app/[locale]/layout.tsx",
      // Scraped job text, sanitized with DOMPurify in the browser right before the write.
      "components/applications/JobDescriptionCard.tsx",
      // A post's body: admin-written, sanitized by the API's allowlist on every save.
      "components/blog/BlogArticleBody.tsx",
      // JSON-LD: a serialised object of ours, escaped for </script>.
      "components/seo/JsonLd.tsx",
    ]);
  });
});

describe("the post body", () => {
  const body = read("components/blog/BlogArticleBody.tsx");

  it("is a server component: the sanitized HTML is in the response for crawlers, and nothing hydrates it", () => {
    expect(body).not.toContain('"use client"');
  });

  it("does not re-sanitize — the API's sanitizer is the one boundary, and a second one here would be a second allowlist to keep in step", () => {
    expect(body).not.toMatch(/from "dompurify"|from "isomorphic-dompurify"|sanitize\(/);
  });
});

describe("the blog pages", () => {
  for (const page of ["app/[locale]/(public)/blog/page.tsx", "app/[locale]/(public)/blog/[slug]/page.tsx"]) {
    it(`${page} stays dynamic, so its notFound() is a 404 and not the static-page 500 of 2026-09-16`, () => {
      const source = read(page);
      expect(source).not.toContain("generateStaticParams");
      expect(source).not.toContain("force-static");
      expect(source).not.toContain("dynamicParams");
    });
  }
});

describe("the list page", () => {
  const list = read("app/[locale]/(public)/blog/page.tsx");

  it("404s only when the blog is off or empty everywhere — a language with no posts points at the other (2026-09-20)", () => {
    // The first Turkish post lit the "Blog" link on the English site (hasPublishedPosts counts
    // both languages) and the link opened on a 404.
    expect(list).not.toContain("!list || list.totalCount === 0) notFound()");
    expect(list).toContain('t("emptyInLanguage"');
    expect(list).toContain('t("readListInOtherLanguage"');
    expect(list).toContain("if (!list) notFound();");
  });

  it("shows a card's like count with the count string, not the button's label", () => {
    expect(list).toContain('t("likeCount", { count: post.likeCount })');
    expect(list).not.toContain('t("like", { count');
  });
});

describe("the article", () => {
  const article = read("components/blog/BlogArticle.tsx");

  it("renders the comments only when the page hands them over — the preview never does (2026-09-20)", () => {
    expect(article).toContain("{comments !== undefined && (");
    expect(read("components/blog/BlogPreview.tsx")).not.toContain("comments=");
    // A comment is plain text: React escapes it, and nothing here turns it into markup.
    const item = read("components/blog/comments/CommentItem.tsx");
    expect(item).not.toContain("dangerouslySetInnerHTML");
    expect(item).toContain("{comment.content}");
  });

  it("shows how many times the post was read, next to the like (2026-09-20)", () => {
    expect(article).toContain('t("viewCount", { count: post.viewCount })');
  });
});

describe("the preview", () => {
  const publicPage = read("app/[locale]/(public)/blog/[slug]/page.tsx");
  const preview = read("components/blog/BlogPreview.tsx");

  it("renders the article with the public page's own component, so it cannot drift from the live page except by data", () => {
    expect(publicPage).toContain("<BlogArticle post={post} url={url} comments={comments} renderedAt={new Date().toISOString()} />");
    expect(preview).toContain("<BlogArticle post={post} url={url} inert />");
    // Neither page lays out a title, a body or a footer of its own.
    for (const source of [publicPage, preview]) {
      expect(source).not.toContain("<h1");
      expect(source).not.toContain("<BlogArticleBody");
      expect(source).not.toContain("<LikeButton");
    }
  });

  it("lives under the public chrome and is never indexed", () => {
    const route = read("app/[locale]/(public)/blog/preview/[id]/page.tsx");
    expect(route).toContain("robots: { index: false, follow: false }");
  });

  it("fetches a draft's images with the token instead of leaving <img src> to 404", () => {
    expect(preview).toContain("rewriteMediaSources");
    expect(preview).toContain("fetchMediaBlob");
  });
});

describe("the editor", () => {
  it("is loaded in the browser only — ProseMirror touches document at import", () => {
    const route = read("app/[locale]/(protected)/admin/blog/[id]/page.tsx");
    expect(route).toContain('"use client"');
    expect(route).toContain("ssr: false");
  });

  it("renders draft images from a token-carrying fetch, never from a bare <img src> the API would 404", () => {
    const view = read("components/blog/editor/MediaImage.tsx");
    expect(view).toContain("useMediaObjectUrl");
  });
});

describe("blog images on the web origin", () => {
  it("are rewritten to the API, so the stored HTML needs no hostname and img-src stays 'self'", () => {
    const config = readFileSync(join(root, "../next.config.ts"), "utf8");
    expect(config).toContain('source: "/api/blog/media/:id"');
    expect(config).toContain("${apiOrigin}/api/blog/media/:id");
    expect(config).toMatch(/"img-src": "'self' data: blob:"/);
  });
});

describe("the guide in the blog's editor (2026-09-26)", () => {
  const article = read("components/guide/GuideArticle.tsx");

  it("has no comments — the guide takes likes and counts views, nothing else", () => {
    expect(article).not.toContain("CommentSection");
    expect(article).toContain("<LikeButton");
    expect(article).toContain('tBlog("viewCount", { count: post.viewCount })');
  });

  it("renders the body with the blog's server component, so there is still one place raw HTML is written", () => {
    expect(article).toContain("<BlogArticleBody html={post.contentHtml}");
    expect(article).not.toContain("dangerouslySetInnerHTML");
  });

  it("renders the live page with the same component as the preview, laying out nothing of its own", () => {
    const page = read("app/[locale]/(public)/guide/[slug]/page.tsx");
    expect(page).toContain("<GuideArticle post={guide} url={url} />");
    expect(page).not.toContain("<h1");
    expect(page).not.toContain("<BlogArticleBody");
    expect(page).not.toContain("CommentSection");
  });

  it("is previewed with its own component under the guide's section, never indexed", () => {
    expect(read("components/blog/BlogPreview.tsx")).toContain("<GuideArticle post={post} url={url} inert />");
    const route = read("app/[locale]/(public)/guide/preview/[id]/page.tsx");
    expect(route).toContain("robots: { index: false, follow: false }");
    expect(route).toContain('kind="Guide"');
  });

  it("is edited in the browser-only editor, opened as a guide", () => {
    const route = read("app/[locale]/(protected)/admin/guide/[id]/page.tsx");
    expect(route).toContain('"use client"');
    expect(route).toContain("ssr: false");
    expect(route).toContain('kind="Guide"');
  });
});

