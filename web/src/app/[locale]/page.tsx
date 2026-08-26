import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { getServerTheme } from "@/lib/theme/getServerTheme";
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

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("landing.hero");
  return {
    title: t("title"),
    description: t("subtitle"),
    openGraph: {
      title: t("title"),
      description: t("subtitle"),
      type: "website",
    },
    twitter: {
      card: "summary",
      title: t("title"),
      description: t("subtitle"),
    },
  };
}

export default async function LandingPage() {
  const theme = await getServerTheme();

  return (
    <div className="flex min-h-screen flex-col">
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
