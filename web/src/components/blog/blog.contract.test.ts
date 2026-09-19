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

describe("the preview", () => {
  const publicPage = read("app/[locale]/(public)/blog/[slug]/page.tsx");
  const preview = read("components/blog/BlogPreview.tsx");

  it("renders the article with the public page's own component, so it cannot drift from the live page except by data", () => {
    expect(publicPage).toContain("<BlogArticle post={post} url={url} />");
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
