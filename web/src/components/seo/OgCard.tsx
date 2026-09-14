import { OG_IMAGE_HEIGHT, OG_IMAGE_WIDTH } from "@/lib/seo/ogImage";

/**
 * The 1200×630 card behind every `og:image` on the site — rendered by `next/og`'s satori, which
 * understands a subset of CSS: flexbox only, every element with more than one child needs
 * `display: flex`, and no external assets. The brand mark is therefore drawn with boxes rather than
 * loaded from `/brand/logo-mark.png` (the landing card used to embed the PNG as a data URI and it
 * never rendered — the card shipped with an empty space where the logo should be).
 *
 * Same card for the landing page and for every other page: the kicker names the section, the title
 * is the page's own. Satori's bundled font covers Turkish (ş, ğ, ı render), which was checked on the
 * live landing image before this was written.
 */
export function OgCard({ title, kicker }: { title: string; kicker?: string }) {
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
        <div
          style={{
            width: 52,
            height: 52,
            borderRadius: 14,
            background: "#2b62d9",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
          }}
        >
          <div style={{ width: 22, height: 22, borderRadius: 6, background: "#f3f5f9", display: "flex" }} />
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
