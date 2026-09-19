"use client";

import { useRef, useState, type ReactNode } from "react";
import { useTranslations } from "next-intl";
import { useEditorState, type Editor } from "@tiptap/react";
import { FONT_FAMILIES, FONT_SIZES, IMAGE_MIME_TYPES, TEXT_COLORS } from "./extensions";

interface BlogToolbarProps {
  editor: Editor;
  /** Uploads a chosen file and answers its `src`, or null when refused. */
  uploadImage: (file: File) => Promise<string | null>;
}

const buttonBase =
  "inline-flex h-8 min-w-8 items-center justify-center rounded-md px-1.5 text-sm text-gray-700 hover:bg-gray-200 disabled:opacity-40 disabled:hover:bg-transparent dark:text-gray-300 dark:hover:bg-gray-700";
const buttonActive = "bg-accent/15 text-accent-ink hover:bg-accent/20 dark:hover:bg-accent/25";
const selectClass =
  "h-8 rounded-md border border-gray-300 bg-white px-1.5 text-sm text-gray-900 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100";

function Divider() {
  return <span aria-hidden="true" className="mx-1 h-6 w-px self-center bg-gray-300 dark:bg-gray-700" />;
}

/**
 * The formatting bar. Every control maps to one Tiptap command and one thing the sanitizer keeps
 * (`BlogHtmlSanitizer` — extensions.ts says why they are reviewed together). State comes from
 * `useEditorState` with a selector, so the bar re-renders on selection changes and nothing else.
 */
export function BlogToolbar({ editor, uploadImage }: BlogToolbarProps) {
  const t = useTranslations("adminBlog.toolbar");
  const fileInput = useRef<HTMLInputElement>(null);
  // The link field: null while closed, else the address being typed. An inline field rather than
  // window.prompt — a native dialog blocks the whole page (and every automation of it).
  const [linkDraft, setLinkDraft] = useState<string | null>(null);

  const s = useEditorState({
    editor,
    selector: ({ editor: e }) => ({
      bold: e.isActive("bold"),
      italic: e.isActive("italic"),
      underline: e.isActive("underline"),
      strike: e.isActive("strike"),
      code: e.isActive("code"),
      subscript: e.isActive("subscript"),
      superscript: e.isActive("superscript"),
      highlight: e.isActive("highlight"),
      link: e.isActive("link"),
      bulletList: e.isActive("bulletList"),
      orderedList: e.isActive("orderedList"),
      taskList: e.isActive("taskList"),
      blockquote: e.isActive("blockquote"),
      codeBlock: e.isActive("codeBlock"),
      table: e.isActive("table"),
      block: e.isActive("heading", { level: 1 })
        ? "h1"
        : e.isActive("heading", { level: 2 })
          ? "h2"
          : e.isActive("heading", { level: 3 })
            ? "h3"
            : e.isActive("heading", { level: 4 })
              ? "h4"
              : "p",
      align: (["left", "center", "right", "justify"] as const).find((value) => e.isActive({ textAlign: value })) ?? "left",
      fontFamily: (e.getAttributes("textStyle").fontFamily as string | undefined) ?? "",
      fontSize: (e.getAttributes("textStyle").fontSize as string | undefined) ?? "",
      color: (e.getAttributes("textStyle").color as string | undefined) ?? "",
      canUndo: e.can().undo(),
      canRedo: e.can().redo(),
      words: e.storage.characterCount.words() as number,
      characters: e.storage.characterCount.characters() as number,
    }),
  });

  const button = (label: string, active: boolean, onClick: () => void, children: ReactNode, disabled = false) => (
    <button
      type="button"
      title={label}
      aria-label={label}
      aria-pressed={active}
      disabled={disabled}
      onMouseDown={(event) => event.preventDefault()}
      onClick={onClick}
      className={`${buttonBase} ${active ? buttonActive : ""}`}
    >
      {children}
    </button>
  );

  const setBlock = (value: string) => {
    const chain = editor.chain().focus();
    if (value === "p") chain.setParagraph().run();
    else chain.toggleHeading({ level: Number(value.slice(1)) as 1 | 2 | 3 | 4 }).run();
  };

  const openLink = () => {
    if (linkDraft !== null) {
      setLinkDraft(null);
      return;
    }
    setLinkDraft((editor.getAttributes("link").href as string | undefined) ?? "");
  };

  const applyLink = () => {
    const href = (linkDraft ?? "").trim();
    setLinkDraft(null);
    // An empty address removes the link; anything else is set (the sanitizer refuses schemes
    // other than http(s) on save, and autolink's default protocol covers a bare host).
    if (href === "") editor.chain().focus().extendMarkRange("link").unsetLink().run();
    else editor.chain().focus().extendMarkRange("link").setLink({ href }).run();
  };

  const pickImage = () => fileInput.current?.click();

  const onFilePicked = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    const src = await uploadImage(file);
    if (src) editor.chain().focus().setImage({ src, alt: "" }).run();
  };

  return (
    <div
      role="toolbar"
      aria-label={t("label")}
      className="sticky top-14 z-20 flex flex-wrap items-center gap-0.5 rounded-t-lg border-b border-gray-200 bg-gray-50/95 p-1.5 backdrop-blur dark:border-gray-800 dark:bg-gray-900/95"
    >
      {button(t("undo"), false, () => editor.chain().focus().undo().run(), "↶", !s.canUndo)}
      {button(t("redo"), false, () => editor.chain().focus().redo().run(), "↷", !s.canRedo)}
      <Divider />

      <select aria-label={t("block")} value={s.block} onChange={(e) => setBlock(e.target.value)} className={selectClass}>
        <option value="p">{t("paragraph")}</option>
        <option value="h1">{t("heading1")}</option>
        <option value="h2">{t("heading2")}</option>
        <option value="h3">{t("heading3")}</option>
        <option value="h4">{t("heading4")}</option>
      </select>
      <select
        aria-label={t("fontFamily")}
        value={FONT_FAMILIES.find((f) => f.css === s.fontFamily)?.key ?? "default"}
        onChange={(e) => {
          const css = FONT_FAMILIES.find((f) => f.key === e.target.value)?.css ?? null;
          if (css) editor.chain().focus().setFontFamily(css).run();
          else editor.chain().focus().unsetFontFamily().run();
        }}
        className={selectClass}
      >
        {FONT_FAMILIES.map((font) => (
          <option key={font.key} value={font.key} style={font.css ? { fontFamily: font.css } : undefined}>
            {font.css ? font.css.split(",")[0].replace(/'/g, "") : t("fontDefault")}
          </option>
        ))}
      </select>
      <select
        aria-label={t("fontSize")}
        value={s.fontSize || "16px"}
        onChange={(e) => {
          if (e.target.value === "16px") editor.chain().focus().unsetFontSize().run();
          else editor.chain().focus().setFontSize(e.target.value).run();
        }}
        className={selectClass}
      >
        {FONT_SIZES.map((size) => (
          <option key={size} value={size}>
            {size.replace("px", "")}
          </option>
        ))}
      </select>
      <Divider />

      {button(t("bold"), s.bold, () => editor.chain().focus().toggleBold().run(), <b>B</b>)}
      {button(t("italic"), s.italic, () => editor.chain().focus().toggleItalic().run(), <i>I</i>)}
      {button(t("underline"), s.underline, () => editor.chain().focus().toggleUnderline().run(), <u>U</u>)}
      {button(t("strike"), s.strike, () => editor.chain().focus().toggleStrike().run(), <s>S</s>)}
      {button(t("code"), s.code, () => editor.chain().focus().toggleCode().run(), <code>{"<>"}</code>)}
      {button(t("subscript"), s.subscript, () => editor.chain().focus().toggleSubscript().run(), <span>x<sub>2</sub></span>)}
      {button(t("superscript"), s.superscript, () => editor.chain().focus().toggleSuperscript().run(), <span>x<sup>2</sup></span>)}
      {button(t("highlight"), s.highlight, () => editor.chain().focus().toggleHighlight().run(), <mark className="rounded px-0.5">ab</mark>)}
      <span className="relative inline-flex items-center" title={t("textColor")}>
        <span
          aria-hidden="true"
          className="inline-flex h-8 min-w-8 items-center justify-center rounded-md px-1.5 text-sm font-semibold"
          style={{ color: s.color || undefined, borderBottom: `3px solid ${s.color || "currentColor"}` }}
        >
          A
        </span>
        <input
          type="color"
          aria-label={t("textColor")}
          value={s.color || "#111827"}
          onChange={(e) => editor.chain().focus().setColor(e.target.value).run()}
          className="absolute inset-0 h-full w-full cursor-pointer opacity-0"
        />
      </span>
      <span className="hidden items-center gap-0.5 lg:inline-flex">
        {TEXT_COLORS.map((color) => (
          <button
            key={color}
            type="button"
            title={color}
            aria-label={color}
            onMouseDown={(event) => event.preventDefault()}
            onClick={() => editor.chain().focus().setColor(color).run()}
            className={`h-4 w-4 rounded-full border border-black/10 ${s.color === color ? "ring-2 ring-accent ring-offset-1" : ""}`}
            style={{ backgroundColor: color }}
          />
        ))}
      </span>
      {button(t("clearColor"), false, () => editor.chain().focus().unsetColor().unsetFontFamily().unsetFontSize().unsetHighlight().run(), "⌫")}
      <Divider />

      {button(t("alignLeft"), s.align === "left", () => editor.chain().focus().setTextAlign("left").run(), "⇤")}
      {button(t("alignCenter"), s.align === "center", () => editor.chain().focus().setTextAlign("center").run(), "☰")}
      {button(t("alignRight"), s.align === "right", () => editor.chain().focus().setTextAlign("right").run(), "⇥")}
      {button(t("alignJustify"), s.align === "justify", () => editor.chain().focus().setTextAlign("justify").run(), "≡")}
      <Divider />

      {button(t("bulletList"), s.bulletList, () => editor.chain().focus().toggleBulletList().run(), "•≡")}
      {button(t("orderedList"), s.orderedList, () => editor.chain().focus().toggleOrderedList().run(), "1≡")}
      {button(t("taskList"), s.taskList, () => editor.chain().focus().toggleTaskList().run(), "☑")}
      {button(t("blockquote"), s.blockquote, () => editor.chain().focus().toggleBlockquote().run(), "❝")}
      {button(t("codeBlock"), s.codeBlock, () => editor.chain().focus().toggleCodeBlock().run(), "{ }")}
      {button(t("horizontalRule"), false, () => editor.chain().focus().setHorizontalRule().run(), "—")}
      <Divider />

      {button(t("link"), s.link || linkDraft !== null, openLink, "🔗")}
      {linkDraft !== null && (
        <form
          className="inline-flex items-center gap-1"
          onSubmit={(event) => {
            event.preventDefault();
            applyLink();
          }}
        >
          <input
            type="text"
            inputMode="url"
            autoFocus
            aria-label={t("linkPrompt")}
            placeholder="https://"
            value={linkDraft}
            onChange={(event) => setLinkDraft(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Escape") setLinkDraft(null);
            }}
            className="h-8 w-56 rounded-md border border-gray-300 bg-white px-2 text-sm text-gray-900 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100"
          />
          <button type="submit" className={buttonBase} onMouseDown={(event) => event.preventDefault()}>
            ↵
          </button>
        </form>
      )}
      {s.link && button(t("unlink"), false, () => editor.chain().focus().unsetLink().run(), "⛓")}
      {button(t("image"), false, pickImage, "🖼")}
      <input ref={fileInput} type="file" accept={IMAGE_MIME_TYPES.join(",")} className="hidden" onChange={onFilePicked} />
      {button(t("table"), s.table, () => editor.chain().focus().insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run(), "⊞")}
      {s.table && (
        <>
          {button(t("addRowAfter"), false, () => editor.chain().focus().addRowAfter().run(), "+↓")}
          {button(t("addColumnAfter"), false, () => editor.chain().focus().addColumnAfter().run(), "+→")}
          {button(t("deleteRow"), false, () => editor.chain().focus().deleteRow().run(), "−↓")}
          {button(t("deleteColumn"), false, () => editor.chain().focus().deleteColumn().run(), "−→")}
          {button(t("deleteTable"), false, () => editor.chain().focus().deleteTable().run(), "⊠")}
        </>
      )}
      {button(t("clearFormatting"), false, () => editor.chain().focus().unsetAllMarks().clearNodes().run(), "Tx")}

      <span className="ml-auto pr-1 text-xs tabular-nums text-gray-500 dark:text-gray-400">
        {t("words", { count: s.words })} · {t("characters", { count: s.characters })}
      </span>
    </div>
  );
}
