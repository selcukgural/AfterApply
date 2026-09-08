import type { Metadata } from "next";
import { getLocale, getTranslations } from "next-intl/server";
import { getServerTheme } from "@/lib/theme/getServerTheme";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { JsonLd } from "@/components/seo/JsonLd";
import { jsonLdGraph, organizationJsonLd, webApplicationJsonLd } from "@/lib/seo/jsonLd";
import { LandingNavbar } from "@/components/landing/LandingNavbar";
import { HeroSection } from "@/components/landing/HeroSection";
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
import { LandingFooter } from "@/components/landing/LandingFooter";

/**
 * The hero line ("Başvurdun. Peki sonra ne oldu?") stays the <h1>, but it made a poor <title>: it
 * carries neither the product name nor the thing people search for, so a search for "e-kariyerim"
 * had nothing to match and a search for "iş başvuru takip" had no signal at all. The title now says
 * what the product is, and the OG image still shows the hero line when the link is shared.
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
      <LandingNavbar initialTheme={theme} />
      <main className="flex-1">
        <HeroSection />
        <ProblemSection />
        <AfterApplySection />
        <FeaturesSection />
        <LinkedInImportSection />
        <AnalyticsSection />
        <VisionSection />
        <MissionSection />
        <RoadmapSection />
        <PrivacySection />
        <FinalCtaSection />
      </main>
      <LandingFooter />
    </div>
  );
}
