import createMiddleware from "next-intl/middleware";
import { NextRequest, NextResponse } from "next/server";
import { routing } from "./i18n/routing";
import { guideRedirectForPath } from "./lib/guide/guideLinks";
import { cvScanRedirectForPath, cvScanScoreCardOf } from "./lib/cvScan/path";
import { parseScoreCard } from "./lib/cvScan/scoreCard";
import { parseFlowCard } from "./lib/flowCard/card";
import { flowCardOf, flowCardRedirectForPath } from "./lib/flowCard/path";
import { aboutRedirectForPath } from "./lib/about/path";
import { offerCompareRedirectForPath } from "./lib/offerCompare/path";
import { salaryMarketRedirectForPath } from "./lib/salaryMarket/path";
import { blogSlugRedirectForPath } from "./lib/blog/slugRedirects";
import { apexRedirectUrl, hasDottedFirstSegment, isFileRequest, stripIndexHtml } from "./lib/http/canonicalHost";
import { buildCsp, createNonce, cspOriginsFromEnv } from "./lib/http/contentSecurityPolicy";

const withLocale = createMiddleware(routing);

const CSP_ORIGINS = cspOriginsFromEnv();

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

  // A file is served as it is — but a root-level file nothing serves (/llms.txt) is rewritten to the
  // site's 404 page after this proxy has run (UNSERVED_ROOT_FILE_REWRITE), and that page needs the
  // nonce like any other.
  if (isFileRequest(pathname) || hasDottedFirstSegment(pathname)) {
    return rendered(request, (headers) => NextResponse.next({ request: { headers } }));
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

  // The salary pages, the same way (/tr/maaslar[/…], /en/salaries[/…]).
  const salaryMarketUrl = salaryMarketRedirectForPath(request.nextUrl.pathname);
  if (salaryMarketUrl) {
    return NextResponse.redirect(new URL(`${salaryMarketUrl}${request.nextUrl.search}`, request.url), 301);
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
    return rendered(request, (headers) => NextResponse.rewrite(new URL(`/${scoreCard.locale}/404`, request.url), { request: { headers } }));
  }

  // A flow card that does not add up is a 404 the same way, before the page renders.
  const flowCard = flowCardOf(request.nextUrl.pathname);
  if (flowCard && !parseFlowCard(flowCard.card)) {
    return rendered(request, (headers) => NextResponse.rewrite(new URL(`/${flowCard.locale}/404`, request.url), { request: { headers } }));
  }

  // next-intl copies the request's headers into the response it builds, so the nonce reaches the
  // page through a request carrying them.
  return rendered(request, (headers) => withLocale(new NextRequest(request, { headers })));
}

/**
 * Every response that renders a page gets a fresh nonce and the CSP that names it: on the request,
 * where Next.js reads the nonce for its own scripts and the root layout reads `x-nonce`, and on the
 * response, where the browser enforces it. See lib/http/contentSecurityPolicy.ts.
 */
function rendered(request: NextRequest, respond: (headers: Headers) => NextResponse): NextResponse {
  const nonce = createNonce();
  const csp = buildCsp({
    nonce,
    pathname: request.nextUrl.pathname,
    origins: CSP_ORIGINS,
    isDev: process.env.NODE_ENV === "development",
  });

  const headers = new Headers(request.headers);
  headers.set("x-nonce", nonce);
  headers.set("Content-Security-Policy", csp);

  const response = respond(headers);
  response.headers.set("Content-Security-Policy", csp);
  return response;
}

export const config = {
  // The first pattern is every page, minus Next's internals and anything with a dot in it. The
  // second adds back paths whose *first* segment has a dot — /sitemap.xml, /robots.txt, /index.html,
  // /llms.txt, /.well-known/… — so the www redirect covers the files a search engine asks for by
  // URL, and the 404 an unserved one is rewritten to gets its CSP nonce. Nested assets
  // (/_next/…, /vendor/…, /brand/…) still never reach the proxy.
  matcher: ["/((?!api|_next|.*\\..*).*)", "/:file([^/]*\\.[^/]*)/:rest*"],
};
