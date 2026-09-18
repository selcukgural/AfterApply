import { OG_IMAGE_HEIGHT, OG_IMAGE_WIDTH } from "@/lib/seo/ogImage";

export interface ScoreOgCardProps {
  score: number;
  /** Category label + "34/40", in report order; empty when the card carries the score alone. */
  categories: readonly { label: string; score: number; weight: number }[];
  kicker: string;
  title: string;
  footer: string;
}

/**
 * The share card behind a `/cv-tarama/puan/<card>` page: the brand card's ground and mark (OgCard),
 * with the score where the page title normally goes and the four subtotals under the question.
 * Same satori constraints — flexbox only, every multi-child element `display: flex`, no assets.
 *
 * One number, one colour. The result screen tints the score by band; the card does not, on
 * purpose: a person who chose to share a 61 should not have it carried around the internet in
 * red (DEVELOPMENT_PLAN.md, T-series standing rules).
 */
export function ScoreOgCard({ score, categories, kicker, title, footer }: ScoreOgCardProps) {
  return (
    <div
      style={{
        width: OG_IMAGE_WIDTH,
        height: OG_IMAGE_HEIGHT,
        display: "flex",
        flexDirection: "column",
        justifyContent: "space-between",
        padding: "64px 80px",
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

      <div style={{ display: "flex", alignItems: "flex-end", gap: 48 }}>
        <div style={{ display: "flex", alignItems: "baseline" }}>
          <div style={{ display: "flex", fontSize: 232, fontWeight: 700, lineHeight: 0.9, letterSpacing: -12 }}>{score}</div>
          <div style={{ display: "flex", fontSize: 44, fontWeight: 500, color: "#9aa5bd", marginLeft: 12 }}>/100</div>
        </div>
        <div style={{ display: "flex", flexDirection: "column", gap: 14, paddingBottom: 18, maxWidth: 620 }}>
          <div style={{ display: "flex", fontSize: 24, color: "#8fb0f5", textTransform: "uppercase", letterSpacing: 3 }}>
            {kicker}
          </div>
          <div style={{ display: "flex", fontSize: 50, fontWeight: 700, lineHeight: 1.12, letterSpacing: -1.5 }}>{title}</div>
          {categories.length > 0 ? (
            <div style={{ display: "flex", flexWrap: "wrap", gap: 22, fontSize: 22, color: "#9aa5bd", marginTop: 6 }}>
              {categories.map((entry) => (
                <div key={entry.label} style={{ display: "flex", gap: 8 }}>
                  <div style={{ display: "flex" }}>{entry.label}</div>
                  <div style={{ display: "flex", color: "#f3f5f9", fontWeight: 600 }}>
                    {entry.score}/{entry.weight}
                  </div>
                </div>
              ))}
            </div>
          ) : null}
        </div>
      </div>

      <div style={{ display: "flex", fontSize: 24, color: "#9aa5bd" }}>{footer}</div>
    </div>
  );
}
