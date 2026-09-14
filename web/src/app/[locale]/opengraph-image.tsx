import { ImageResponse } from "next/og";
import { getTranslations } from "next-intl/server";
import { OgCard } from "@/components/seo/OgCard";
import { OG_IMAGE_HEIGHT, OG_IMAGE_WIDTH } from "@/lib/seo/ogImage";

export const alt = "e-kariyerim";
export const size = { width: OG_IMAGE_WIDTH, height: OG_IMAGE_HEIGHT };
export const contentType = "image/png";

/** The landing page's card: the hero line as the title. Every other page uses `/[locale]/og`. */
export default async function Image({ params }: { params: Promise<{ locale: string }> }) {
  const { locale } = await params;
  const t = await getTranslations({ locale, namespace: "landing.hero" });

  return new ImageResponse(<OgCard title={t("title")} kicker={t("eyebrow")} />, { ...size });
}
