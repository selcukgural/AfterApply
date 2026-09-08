import { describe, expect, it } from "vitest";
import { breadcrumbJsonLd, jsonLdGraph, organizationJsonLd, serializeJsonLd, webApplicationJsonLd } from "./jsonLd";

describe("serializeJsonLd", () => {
  // A literal "</script>" in a value would close the script element early and let the rest of the
  // string be parsed as markup. Nothing in these nodes comes from user input today, but a company
  // name or a job title could end up in one.
  it("escapes the characters that could break out of the script element", () => {
    const serialized = serializeJsonLd({ name: "</script><img src=x onerror=alert(1)>" });

    expect(serialized).not.toContain("</script>");
    expect(serialized).not.toContain("<");
    expect(serialized).not.toContain(">");
    expect(JSON.parse(serialized).name).toBe("</script><img src=x onerror=alert(1)>");
  });

  it("escapes ampersands too", () => {
    const serialized = serializeJsonLd({ name: "Kariyer & Co" });

    expect(serialized).not.toContain("&");
    expect(JSON.parse(serialized).name).toBe("Kariyer & Co");
  });

  it("stays parseable JSON for the whole landing graph", () => {
    const graph = jsonLdGraph(organizationJsonLd(), webApplicationJsonLd("tr", "İş başvurularını takip et."));

    expect(() => JSON.parse(serializeJsonLd(graph))).not.toThrow();
  });
});

describe("jsonLdGraph", () => {
  it("wraps the nodes under one @context", () => {
    const graph = jsonLdGraph({ "@type": "Thing" });

    expect(graph["@context"]).toBe("https://schema.org");
    expect(graph["@graph"]).toEqual([{ "@type": "Thing" }]);
  });
});

describe("webApplicationJsonLd", () => {
  it("references the organization by @id rather than repeating it", () => {
    const app = webApplicationJsonLd("en", "Track your job applications.");

    expect(app.publisher).toEqual({ "@id": organizationJsonLd()["@id"] });
    expect(app.url).toBe("https://ekariyerim.com/en");
    expect(app.inLanguage).toBe("en");
    expect(app.description).toBe("Track your job applications.");
  });

  it("says the product is free", () => {
    expect(webApplicationJsonLd("tr", "…").offers).toEqual({ "@type": "Offer", price: "0", priceCurrency: "TRY" });
  });
});

describe("breadcrumbJsonLd", () => {
  it("numbers the trail from one and absolutises each step for the locale", () => {
    const crumb = breadcrumbJsonLd("tr", [
      { name: "e-kariyerim", path: "" },
      { name: "Genel Bakış", path: "/help" },
      { name: "Chrome Eklentisi", path: "/help/chrome-extension" },
    ]);

    expect(crumb.itemListElement).toEqual([
      { "@type": "ListItem", position: 1, name: "e-kariyerim", item: "https://ekariyerim.com/tr" },
      { "@type": "ListItem", position: 2, name: "Genel Bakış", item: "https://ekariyerim.com/tr/help" },
      {
        "@type": "ListItem",
        position: 3,
        name: "Chrome Eklentisi",
        item: "https://ekariyerim.com/tr/help/chrome-extension",
      },
    ]);
  });
});
