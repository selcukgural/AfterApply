import createMiddleware from "next-intl/middleware";
import { NextResponse, type NextRequest } from "next/server";
import { routing } from "./i18n/routing";
import { guideRedirectForPath } from "./lib/guide/articles";
import { cvScanRedirectForPath, cvScanScoreCardOf } from "./lib/cvScan/path";
import { parseScoreCard } from "./lib/cvScan/scoreCard";
import { parseFlowCard } from "./lib/flowCard/card";
import { flowCardOf, flowCardRedirectForPath } from "./lib/flowCard/path";
import { aboutRedirectForPath } from "./lib/about/path";
import { offerCompareRedirectForPath } from "./lib/offerCompare/path";
import { blogSlugRedirectForPath } from "./lib/blog/slugRedirects";
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

  // A guide article at the wrong address — the other locale's slug under /tr or /en, or no locale
  // at all — is sent to the right one from here, permanently. Here and not in the page: the
  // article pages are static, and a redirect from inside one is a runtime static-to-dynamic error.
  const guideUrl = guideRedirectForPath(request.nextUrl.pathname);
  if (guideUrl) {
    return NextResponse.redirect(new URL(`${guideUrl}${request.nextUrl.search}`, request.url), 301);
  }

  // The CV scan is the other page with a translated slug: /en/cv-tarama and /tr/cv-scan (and the
  // score pages under them) go to the right spelling the same way. The English address is served
  // by a rewrite in next.config.ts, so this only ever fires for the wrong one.
  const cvScanUrl = cvScanRedirectForPath(request.nextUrl.pathname);
  if (cvScanUrl) {
    return NextResponse.redirect(new URL(`${cvScanUrl}${request.nextUrl.search}`, request.url), 301);
  }

  // A shared flow card under the other locale's slug (/tr/flow/…, /en/akis/…).
  const flowUrl = flowCardRedirectForPath(request.nextUrl.pathname);
  if (flowUrl) {
    return NextResponse.redirect(new URL(`${flowUrl}${request.nextUrl.search}`, request.url), 301);
  }

  // The about page, translated the same way (/tr/hakkimizda, /en/about).
  const aboutUrl = aboutRedirectForPath(request.nextUrl.pathname);
  if (aboutUrl) {
    return NextResponse.redirect(new URL(`${aboutUrl}${request.nextUrl.search}`, request.url), 301);
  }

  // The offer comparison, the same way (/tr/teklif-karsilastirma, /en/offer-comparison).
  const offerCompareUrl = offerCompareRedirectForPath(request.nextUrl.pathname);
  if (offerCompareUrl) {
    return NextResponse.redirect(new URL(`${offerCompareUrl}${request.nextUrl.search}`, request.url), 301);
  }

  // A blog post whose slug was corrected after it went live: the old address, permanently, to
  // the new one — the list lives next to the migration that renamed it (lib/blog/slugRedirects).
  const blogUrl = blogSlugRedirectForPath(request.nextUrl.pathname);
  if (blogUrl) {
    return NextResponse.redirect(new URL(`${blogUrl}${request.nextUrl.search}`, request.url), 301);
  }

  // A score page whose card the scan could not have produced ("101", parts that do not add up)
  // is a 404 — decided here, because the score pages are prerendered and a notFound() from
  // inside one is a runtime static-to-dynamic error. Rewritten onto a path nothing claims, so
  // the locale's own catch-all renders the site's 404 page under the original address.
  const scoreCard = cvScanScoreCardOf(request.nextUrl.pathname);
  if (scoreCard && !parseScoreCard(scoreCard.card)) {
    return NextResponse.rewrite(new URL(`/${scoreCard.locale}/404`, request.url));
  }

  // A flow card that does not add up is a 404 the same way, before the page renders.
  const flowCard = flowCardOf(request.nextUrl.pathname);
  if (flowCard && !parseFlowCard(flowCard.card)) {
    return NextResponse.rewrite(new URL(`/${flowCard.locale}/404`, request.url));
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
