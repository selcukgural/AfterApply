import { fromMarkdown } from "mdast-util-from-markdown";
import type { Nodes as MdNode, PhrasingContent, RootContent } from "mdast";

/**
 * The one-off move of the file-based guides into the blog's editor (DECISIONS.md 2026-09-26):
 * one guide's Markdown turned into what the editor stores — its document (ProseMirror JSON, what
 * it reopens) and the HTML the API sanitizes and the page renders.
 *
 * The guide files are plain CommonMark with no JSX and no GFM (`articles.test.ts` keeps them so),
 * which is a small, closed set of nodes: headings, paragraphs, bold, italic, links, lists, quotes,
 * rules and the odd image. Each maps to a node the editor already has, so the document is built
 * directly from the Markdown tree — no browser, no DOM — and the HTML is written from that
 * document with the same tags TipTap writes. Anything outside the set throws: a guide that needs
 * it is moved by hand rather than silently losing a part.
 */

export interface EditorMark {
  type: "bold" | "italic" | "code" | "link";
  attrs?: Record<string, string>;
}

export interface EditorNode {
  type: string;
  attrs?: Record<string, string | number | null>;
  content?: EditorNode[];
  marks?: EditorMark[];
  text?: string;
}

export interface EditorDocument {
  json: EditorNode;
  html: string;
}

/** The links the editor stores carry these (StarterKit's link config); the sanitizer re-applies them anyway. */
const LINK_ATTRS = { target: "_blank", rel: "noopener noreferrer nofollow" };

/**
 * @param imageSources where each image in the Markdown now lives — the file's path as written
 *   (`/guide/x.png`) mapped to its uploaded `/api/blog/media/<id>`. An image with no entry throws.
 */
export function markdownToEditor(markdown: string, imageSources: ReadonlyMap<string, string> = new Map()): EditorDocument {
  const tree = fromMarkdown(markdown);
  const json: EditorNode = { type: "doc", content: tree.children.flatMap((node) => block(node, imageSources)) };
  return { json, html: (json.content ?? []).map(toHtml).join("") };
}

/** Every image path the Markdown refers to, in order — what the importer uploads first. */
export function imagePathsIn(markdown: string): string[] {
  const paths: string[] = [];
  const visit = (node: MdNode) => {
    if (node.type === "image") paths.push(node.url);
    if ("children" in node) node.children.forEach(visit);
  };
  visit(fromMarkdown(markdown));
  return paths;
}

function block(node: RootContent, images: ReadonlyMap<string, string>): EditorNode[] {
  switch (node.type) {
    case "heading":
      return [{ type: "heading", attrs: { level: node.depth }, content: inline(node.children) }];
    case "paragraph": {
      // A paragraph that is one image is the image: the editor's image node is a block. Its
      // Markdown title was the guide's caption; the editor has no caption, so it becomes the
      // italic line under the picture that a reader sees in the same place.
      if (node.children.length === 1 && node.children[0].type === "image") {
        const image = node.children[0];
        const src = images.get(image.url);
        if (!src) throw new Error(`No uploaded source for image ${image.url}`);
        const figure: EditorNode = { type: "image", attrs: { src, alt: image.alt ?? "", title: null } };
        return image.title
          ? [figure, { type: "paragraph", content: [{ type: "text", text: image.title, marks: [{ type: "italic" }] }] }]
          : [figure];
      }
      return [{ type: "paragraph", content: inline(node.children) }];
    }
    case "list":
      return [
        {
          type: node.ordered ? "orderedList" : "bulletList",
          ...(node.ordered ? { attrs: { start: node.start ?? 1 } } : {}),
          content: node.children.map((item) => ({
            type: "listItem",
            content: item.children.flatMap((child) => block(child, images)),
          })),
        },
      ];
    case "blockquote":
      return [{ type: "blockquote", content: node.children.flatMap((child) => block(child, images)) }];
    case "thematicBreak":
      return [{ type: "horizontalRule" }];
    case "code":
      return [{ type: "codeBlock", attrs: { language: node.lang ?? null }, content: node.value ? [{ type: "text", text: node.value }] : [] }];
    default:
      throw new Error(`Unsupported Markdown block: ${node.type}`);
  }
}

function inline(nodes: PhrasingContent[], marks: EditorMark[] = []): EditorNode[] {
  return nodes.flatMap((node): EditorNode[] => {
    switch (node.type) {
      case "text":
        // The editor has no empty text nodes, and Markdown's soft line break is a space.
        return node.value ? [text(node.value.replace(/\n/g, " "), marks)] : [];
      case "strong":
        return inline(node.children, [...marks, { type: "bold" }]);
      case "emphasis":
        return inline(node.children, [...marks, { type: "italic" }]);
      case "inlineCode":
        return [text(node.value, [...marks, { type: "code" }])];
      case "link":
        return inline(node.children, [{ type: "link", attrs: { href: node.url, ...LINK_ATTRS } }, ...marks]);
      case "break":
        return [{ type: "hardBreak" }];
      default:
        throw new Error(`Unsupported Markdown inline: ${node.type}`);
    }
  });
}

function text(value: string, marks: EditorMark[]): EditorNode {
  return marks.length > 0 ? { type: "text", text: value, marks } : { type: "text", text: value };
}

// ---- HTML, as TipTap writes it ----------------------------------------------------------------

function toHtml(node: EditorNode): string {
  const inner = (node.content ?? []).map(toHtml).join("");
  switch (node.type) {
    case "paragraph":
      return `<p>${inner}</p>`;
    case "heading":
      return `<h${node.attrs!.level}>${inner}</h${node.attrs!.level}>`;
    case "bulletList":
      return `<ul>${inner}</ul>`;
    case "orderedList":
      return node.attrs?.start && node.attrs.start !== 1 ? `<ol start="${node.attrs.start}">${inner}</ol>` : `<ol>${inner}</ol>`;
    case "listItem":
      return `<li>${inner}</li>`;
    case "blockquote":
      return `<blockquote>${inner}</blockquote>`;
    case "horizontalRule":
      return "<hr>";
    case "hardBreak":
      return "<br>";
    case "codeBlock":
      return `<pre><code${node.attrs?.language ? ` class="language-${escape(String(node.attrs.language))}"` : ""}>${inner}</code></pre>`;
    case "image":
      return `<img src="${escape(String(node.attrs!.src))}" alt="${escape(String(node.attrs!.alt ?? ""))}">`;
    case "text":
      return (node.marks ?? []).reduceRight((html, mark) => wrap(mark, html), escape(node.text ?? ""));
    default:
      throw new Error(`No HTML for editor node ${node.type}`);
  }
}

function wrap(mark: EditorMark, html: string): string {
  switch (mark.type) {
    case "bold":
      return `<strong>${html}</strong>`;
    case "italic":
      return `<em>${html}</em>`;
    case "code":
      return `<code>${html}</code>`;
    case "link":
      return `<a target="${LINK_ATTRS.target}" rel="${LINK_ATTRS.rel}" href="${escape(mark.attrs!.href)}">${html}</a>`;
  }
}

function escape(value: string): string {
  return value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}
