import type { Metadata } from "next";
import { getLocale, getTranslations } from "next-intl/server";
import { getServerTheme } from "@/lib/theme/getServerTheme";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { JsonLd } from "@/components/seo/JsonLd";
import { jsonLdGraph, organizationJsonLd, webApplicationJsonLd } from "@/lib/seo/jsonLd";
import { LANDING_SITE_LINKS, SiteHeader } from "@/components/layout/SiteHeader";
import { SiteFooter } from "@/components/layout/SiteFooter";
import { HeroSection } from "@/components/landing/HeroSection";
import { ToolsStrip } from "@/components/landing/ToolsStrip";
import { ProblemSection } from "@/components/landing/ProblemSection";
import { AfterApplySection } from "@/components/landing/AfterApplySection";
import { FeaturesSection } from "@/components/landing/FeaturesSection";
import { LinkedInImportSection } from "@/components/landing/LinkedInImportSection";
import { AnalyticsSection } from "@/components/landing/AnalyticsSection";
import { VisionSection } from "@/components/landing/VisionSection";
import { MissionSection } from "@/components/landing/MissionSection";
import { RoadmapSection } from "@/components/landing/RoadmapSection";
import { PrivacySection } from "@/components/landing/PrivacySection";
import { FinalCtaSection } from "@/components/landing/FinalCtaSection";
import { SiteTrafficReporter } from "@/components/analytics/SiteTrafficReporter";

/**
 * The hero line ("Başvurdun. Peki sonra ne oldu?") stays the <h1>, but it made a poor <title>: it
 * carries neither the product name nor the thing people search for, so a search for "e-kariyerim"
 * had nothing to match and a search for "iş başvuru takip" had no signal at all. The title now says
 * what the product is, and the OG image still shows the hero line when the link is shared.
 *
 * That split survived the 2026-09-08 promise rewrite on purpose. The page's own copy stopped
 * selling "track your applications in one place" and now leads with what you get out of it — but
 * the <title> still carries "İş Başvuru Takip Uygulaması" / "Job Application Tracker", because a
 * title answers a query someone typed and the hero answers "why should I care". Nobody searches
 * for "how many of my applications got a reply"; they search for the category. The description is
 * where the two meet: it opens with the outcome and still contains the term. A test in
 * routes.test.ts pins the term so a future copy pass cannot quietly delete it.
 */
export async function generateMetadata({ params }: PageProps<"/[locale]">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "", "home");
}

export default async function LandingPage() {
  const theme = await getServerTheme();
  const locale = await getLocale();
  const t = await getTranslations("metadata.pages");

  return (
    <div className="flex min-h-screen flex-col">
      <JsonLd
        data={jsonLdGraph(organizationJsonLd(), webApplicationJsonLd(locale, t("home.description")))}
      />
      {/* Mounted here rather than in [locale]/layout.tsx, which also wraps the signed-in pages.
          Until 2026-09-10 this page reported nothing at all: the reporter lives in the (public)
          layout and the landing page is not in that group, so "/" sat in the API's path allowlist
          with no caller — the funnel had a first step nobody was counting. */}
      <SiteTrafficReporter />
      <SiteHeader initialTheme={theme} links={LANDING_SITE_LINKS} />
      <main className="flex-1">
        <HeroSection />
        {/* The three things that work without an account, the extension's tab open first — one
            screen under the hero, before the page starts explaining itself (2026-09-12). Also the
            #extension target: this is the extension's whole showing on the page. */}
        <ToolsStrip />
        <ProblemSection />
        <AfterApplySection />
        {/*
          Order carries the promise (2026-09-08). The page used to run
          hero → problem → why → *feature list* → import → numbers, which put the thing the product
          is actually for six sections down, well below anywhere a first-time visitor reads. The
          numbers now come straight after the "why", and the import right behind them, because the
          two together are the whole pitch: here is what you get, and here is how it fills itself
          without you typing anything. The feature list is what you read *after* you want it.
          #how-it-works still resolves to AfterApplySection and #features to FeaturesSection, so the
          navbar and the hero's secondary CTA are unaffected.
        */}
        <AnalyticsSection />
        <LinkedInImportSection />
        <FeaturesSection />
        <VisionSection />
        <MissionSection />
        <RoadmapSection />
        <PrivacySection />
        <FinalCtaSection />
      </main>
      <SiteFooter />
    </div>
  );
}
