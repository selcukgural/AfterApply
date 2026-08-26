import { ImageResponse } from "next/og";
import { getTranslations } from "next-intl/server";

export const alt = "e-kariyerim";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

export default async function Image({ params }: { params: Promise<{ locale: string }> }) {
  const { locale } = await params;
  const t = await getTranslations({ locale, namespace: "landing.hero" });

  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "center",
          padding: "80px",
          background: "#f9fafb",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 20 }}>
          <svg
            width="72"
            height="72"
            viewBox="0 0 24 24"
            fill="none"
            stroke="#2563eb"
            strokeWidth={1.75}
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <path d="M4 5.5h16a1 1 0 011 1v9a1 1 0 01-1 1H9.5l-4 3.5v-3.5H4a1 1 0 01-1-1v-9a1 1 0 011-1z" />
            <path d="M8 10.5l2.4 2.4L16.5 7.5" />
          </svg>
          <span style={{ fontSize: 56, fontWeight: 600, color: "#111827" }}>e-kariyerim</span>
        </div>
        <div style={{ display: "flex", marginTop: 40, fontSize: 34, color: "#374151", maxWidth: 920 }}>{t("title")}</div>
      </div>
    ),
    { ...size },
  );
}
