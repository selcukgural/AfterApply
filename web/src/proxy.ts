import createMiddleware from "next-intl/middleware";
import { NextResponse, type NextRequest } from "next/server";
import { routing } from "./i18n/routing";
import { apexRedirectUrl, isFileRequest } from "./lib/http/canonicalHost";

const withLocale = createMiddleware(routing);

export function proxy(request: NextRequest) {
  const apexUrl = apexRedirectUrl(request.headers.get("host"), request.nextUrl.pathname, request.nextUrl.search);
  if (apexUrl) {
    // 301 rather than 307: this is a permanent fact about the site's addressing. A temporary
    // redirect would leave both hosts in the index, which is the thing being fixed.
    return NextResponse.redirect(apexUrl, 301);
  }

  if (isFileRequest(request.nextUrl.pathname)) {
    return NextResponse.next();
  }

  return withLocale(request);
}

export const config = {
  // The first pattern is the original one: every page, minus Next's internals and anything with a
  // dot in it. /sitemap.xml and /robots.txt are then added back by name — not by widening the first
  // pattern, which would put this proxy in front of every image and font for no benefit — so that
  // the www redirect covers the two files a search engine asks for by URL.
  matcher: ["/((?!api|_next|.*\\..*).*)", "/sitemap.xml", "/robots.txt"],
};
