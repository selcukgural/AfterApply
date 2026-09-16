import createMiddleware from "next-intl/middleware";
import { NextResponse, type NextRequest } from "next/server";
import { routing } from "./i18n/routing";
import { guideRedirectForUnprefixedPath } from "./lib/guide/articles";
import { apexRedirectUrl, isFileRequest, stripIndexHtml } from "./lib/http/canonicalHost";

const withLocale = createMiddleware(routing);

export function proxy(request: NextRequest) {
  const pathname = stripIndexHtml(request.nextUrl.pathname);

  const apexUrl = apexRedirectUrl(request.headers.get("host"), pathname, request.nextUrl.search);
  if (apexUrl) {
    // 301 rather than 307: this is a permanent fact about the site's addressing. A temporary
    // redirect would leave both hosts in the index, which is the thing being fixed.
    return NextResponse.redirect(apexUrl, 301);
  }

  if (pathname !== request.nextUrl.pathname) {
    // /index.html on the canonical host: the root, permanently.
    return NextResponse.redirect(new URL(`${pathname}${request.nextUrl.search}`, request.url), 301);
  }

  if (isFileRequest(pathname)) {
    return NextResponse.next();
  }

  // A locale-less /guide/<slug> goes to the slug's own language. Left to next-intl it would take
  // the locale guessed from the cookie or Accept-Language, and an English slug under /tr is a 404
  // (Search Console listed both /guide/<slug> and the /tr/<english slug> it then led to).
  const guideUrl = guideRedirectForUnprefixedPath(request.nextUrl.pathname);
  if (guideUrl) {
    return NextResponse.redirect(new URL(`${guideUrl}${request.nextUrl.search}`, request.url), 301);
  }

  return withLocale(request);
}

export const config = {
  // The first pattern is the original one: every page, minus Next's internals and anything with a
  // dot in it. /sitemap.xml and /robots.txt are then added back by name — not by widening the first
  // pattern, which would put this proxy in front of every image and font for no benefit — so that
  // the www redirect covers the two files a search engine asks for by URL. /index.html is the
  // third: a crawler's guess at the front page, folded onto the root above.
  matcher: ["/((?!api|_next|.*\\..*).*)", "/sitemap.xml", "/robots.txt", "/index.html"],
};
