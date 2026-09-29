import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { OgCard } from "./OgCard";
import { ScoreOgCard } from "./ScoreOgCard";

// The dark share cards draw the real brand mark from the data URI the route hands them, not a
// drawn stand-in.
const logoSrc = "data:image/png;base64,AAAA";

describe("dark share cards", () => {
  it("draws the brand mark on the page card", () => {
    const html = renderToStaticMarkup(createElement(OgCard, { title: "Title", kicker: "Kicker", logoSrc }));
    expect(html).toContain(`<img src="${logoSrc}"`);
  });

  it("draws the brand mark on the score card", () => {
    const html = renderToStaticMarkup(
      createElement(ScoreOgCard, { score: 88, categories: [], kicker: "k", title: "t", footer: "f", logoSrc }),
    );
    expect(html).toContain(`<img src="${logoSrc}"`);
  });
});
