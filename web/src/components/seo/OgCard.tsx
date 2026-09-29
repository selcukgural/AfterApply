import { OG_IMAGE_HEIGHT, OG_IMAGE_WIDTH } from "@/lib/seo/ogImage";

/**
 * The 1200×630 card behind every `og:image` on the site — rendered by `next/og`'s satori, which
 * understands a subset of CSS: flexbox only, every element with more than one child needs
 * `display: flex`, and no external assets. The brand mark arrives as `logoSrc`, a data URI the route
 * reads once from `public/brand/logo-mark-og.png` — the same one the flow card draws.
 *
 * Same card for the landing page and for every other page: the kicker names the section, the title
 * is the page's own. Satori's bundled font covers Turkish (ş, ğ, ı render), which was checked on the
 * live landing image before this was written.
 */
export function OgCard({ title, kicker, logoSrc }: { title: string; kicker?: string; logoSrc: string }) {
  return (
    <div
      style={{
        width: OG_IMAGE_WIDTH,
        height: OG_IMAGE_HEIGHT,
        display: "flex",
        flexDirection: "column",
        justifyContent: "space-between",
        padding: "72px 80px",
        background: "linear-gradient(135deg, #0b1220 0%, #111a2e 55%, #1b2a4a 100%)",
        color: "#f3f5f9",
        fontFamily: "sans-serif",
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: 18 }}>
        {/* The mark's blue stroke disappears on the navy ground, so it sits on a white tile. */}
        <div
          style={{
            width: 60,
            height: 60,
            borderRadius: 16,
            background: "#ffffff",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
          }}
        >
          {/* eslint-disable-next-line @next/next/no-img-element -- satori renders <img>, not next/image */}
          <img src={logoSrc} width={52} height={52} alt="" />
        </div>
        <div style={{ display: "flex", fontSize: 40, fontWeight: 600, letterSpacing: -1 }}>e-kariyerim</div>
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: 18, maxWidth: 1000 }}>
        {kicker ? (
          <div style={{ display: "flex", fontSize: 26, color: "#8fb0f5", textTransform: "uppercase", letterSpacing: 2 }}>
            {kicker}
          </div>
        ) : null}
        <div
          style={{
            display: "flex",
            fontSize: title.length > 60 ? 54 : 66,
            fontWeight: 700,
            lineHeight: 1.12,
            letterSpacing: -1.5,
          }}
        >
          {title}
        </div>
      </div>

      <div style={{ display: "flex", fontSize: 24, color: "#9aa5bd" }}>ekariyerim.com</div>
    </div>
  );
}
