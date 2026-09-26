import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { imagePathsIn, markdownToEditor, type EditorNode } from "./markdownToEditor";

const CONTENT = join(process.cwd(), "src/content/guide");

describe("markdownToEditor", () => {
  it("maps headings, paragraphs and marks to the editor's nodes and TipTap's tags", () => {
    const { json, html } = markdownToEditor("## Başlık\n\nBir **kalın** ve *eğik* söz, `kod` ile.");

    expect(json.content?.[0]).toEqual({ type: "heading", attrs: { level: 2 }, content: [{ type: "text", text: "Başlık" }] });
    expect(json.content?.[1].content).toEqual([
      { type: "text", text: "Bir " },
      { type: "text", text: "kalın", marks: [{ type: "bold" }] },
      { type: "text", text: " ve " },
      { type: "text", text: "eğik", marks: [{ type: "italic" }] },
      { type: "text", text: " söz, " },
      { type: "text", text: "kod", marks: [{ type: "code" }] },
      { type: "text", text: " ile." },
    ]);
    expect(html).toBe("<h2>Başlık</h2><p>Bir <strong>kalın</strong> ve <em>eğik</em> söz, <code>kod</code> ile.</p>");
  });

  it("keeps internal links relative, with the attributes the editor stores", () => {
    const { json, html } = markdownToEditor("Bkz. [bekleme süresi](/guide/is-basvurusundan-sonra-ne-kadar-beklenir).");

    const link = json.content?.[0].content?.[1];
    expect(link?.marks?.[0]).toEqual({
      type: "link",
      attrs: { href: "/guide/is-basvurusundan-sonra-ne-kadar-beklenir", target: "_blank", rel: "noopener noreferrer nofollow" },
    });
    expect(html).toContain('href="/guide/is-basvurusundan-sonra-ne-kadar-beklenir">bekleme süresi</a>');
  });

  it("writes lists, a numbered list's start, quotes and rules", () => {
    const { html } = markdownToEditor("- bir\n- iki\n\n3. üç\n4. dört\n\n> alıntı\n\n---");

    expect(html).toBe(
      "<ul><li><p>bir</p></li><li><p>iki</p></li></ul>" +
        '<ol start="3"><li><p>üç</p></li><li><p>dört</p></li></ol>' +
        "<blockquote><p>alıntı</p></blockquote><hr>",
    );
  });

  it("turns a soft line break into a space, never an empty text node", () => {
    const { json } = markdownToEditor("bir\niki");
    expect(json.content?.[0].content).toEqual([{ type: "text", text: "bir iki" }]);
  });

  it("points an image at its upload and keeps its caption as the italic line under it", () => {
    const md = '![Akış kartı](/guide/kart.png "Örnek bir kart.")';
    const { json, html } = markdownToEditor(md, new Map([["/guide/kart.png", "/api/blog/media/abc"]]));

    expect(json.content).toEqual([
      { type: "image", attrs: { src: "/api/blog/media/abc", alt: "Akış kartı", title: null } },
      { type: "paragraph", content: [{ type: "text", text: "Örnek bir kart.", marks: [{ type: "italic" }] }] },
    ]);
    expect(html).toBe('<img src="/api/blog/media/abc" alt="Akış kartı"><p><em>Örnek bir kart.</em></p>');
    expect(imagePathsIn(md)).toEqual(["/guide/kart.png"]);
  });

  it("refuses an image that was not uploaded, rather than leaving a broken src", () => {
    expect(() => markdownToEditor("![x](/guide/kart.png)")).toThrow("/guide/kart.png");
  });

  it("escapes text, so a guide's prose can never become markup", () => {
    expect(markdownToEditor("a < b & \"c\"").html).toBe("<p>a &lt; b &amp; &quot;c&quot;</p>");
  });

  it("converts every guide file without losing a node type (no throw, nothing empty)", () => {
    for (const file of readdirSync(CONTENT).filter((name) => name.endsWith(".mdx"))) {
      const markdown = readFileSync(join(CONTENT, file), "utf8");
      const images = new Map(imagePathsIn(markdown).map((path) => [path, "/api/blog/media/x"]));
      const { json, html } = markdownToEditor(markdown, images);

      expect(json.content?.length, file).toBeGreaterThan(3);
      expect(html, file).toContain("<h2>");
      const walk = (node: EditorNode): void => {
        if (node.type === "text") expect(node.text, file).not.toBe("");
        node.content?.forEach(walk);
      };
      walk(json);
    }
  });
});
