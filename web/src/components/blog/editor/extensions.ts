import StarterKit from "@tiptap/starter-kit";
import { TextStyleKit } from "@tiptap/extension-text-style";
import Highlight from "@tiptap/extension-highlight";
import TextAlign from "@tiptap/extension-text-align";
import Subscript from "@tiptap/extension-subscript";
import Superscript from "@tiptap/extension-superscript";
import Typography from "@tiptap/extension-typography";
import { TableKit } from "@tiptap/extension-table";
import { TaskItem, TaskList } from "@tiptap/extension-list";
import { CharacterCount, Placeholder } from "@tiptap/extensions";
import FileHandler from "@tiptap/extension-file-handler";
import type { Editor } from "@tiptap/core";
import { MediaImage } from "./MediaImage";

/** What the upload accepts — the server decides by the bytes; this only spares a round trip. */
export const IMAGE_MIME_TYPES = ["image/png", "image/jpeg", "image/gif", "image/webp"];
export const IMAGE_MAX_BYTES = 5 * 1024 * 1024;

/** The fonts a post may switch to. System stacks only: nothing is loaded from a CDN (the CSP's
 *  font-src is 'self'), and a reader's machine has all of these or a close relative. */
export const FONT_FAMILIES: readonly { key: string; css: string | null }[] = [
  { key: "default", css: null },
  { key: "serif", css: "Georgia, 'Times New Roman', serif" },
  { key: "sans", css: "Arial, Helvetica, sans-serif" },
  { key: "humanist", css: "Verdana, Geneva, sans-serif" },
  { key: "mono", css: "'Courier New', Courier, monospace" },
];

export const FONT_SIZES: readonly string[] = ["14px", "16px", "18px", "20px", "24px", "30px"];

/** The text colours offered by name; the picker beside them takes anything. */
export const TEXT_COLORS: readonly string[] = [
  "#111827", "#6b7280", "#dc2626", "#ea580c", "#ca8a04", "#16a34a", "#0891b2", "#2563eb", "#7c3aed", "#db2777",
];

export interface EditorExtensionOptions {
  placeholder: string;
  /** Uploads a dropped or pasted image and answers its `src`; null when it was refused. */
  uploadImage: (file: File) => Promise<string | null>;
}

/**
 * The editor's feature set, in one place so the toolbar, the sanitizer's allowlist and this list
 * are reviewed together: anything added here has to survive `BlogHtmlSanitizer` or it will be
 * silently stripped on save.
 */
export function buildExtensions({ placeholder, uploadImage }: EditorExtensionOptions) {
  const insertUploaded = async (editor: Editor, files: File[], pos?: number) => {
    for (const file of files) {
      const src = await uploadImage(file);
      if (!src) continue;
      const chain = editor.chain().focus();
      if (pos !== undefined) chain.insertContentAt(pos, { type: "image", attrs: { src, alt: "" } });
      else chain.setImage({ src, alt: "" });
      chain.run();
    }
  };

  return [
    StarterKit.configure({
      heading: { levels: [1, 2, 3, 4] },
      link: {
        openOnClick: false,
        autolink: true,
        defaultProtocol: "https",
        // The sanitizer re-applies these on save; setting them here keeps the editor honest.
        HTMLAttributes: { rel: "noopener noreferrer nofollow", target: "_blank" },
      },
    }),
    TextStyleKit,
    Highlight,
    Subscript,
    Superscript,
    Typography,
    TextAlign.configure({ types: ["heading", "paragraph"] }),
    TaskList,
    TaskItem.configure({ nested: true }),
    TableKit.configure({ table: { resizable: true } }),
    MediaImage,
    Placeholder.configure({ placeholder }),
    CharacterCount,
    FileHandler.configure({
      allowedMimeTypes: [...IMAGE_MIME_TYPES],
      onDrop: (editor, files, pos) => {
        void insertUploaded(editor, files, pos);
      },
      onPaste: (editor, files) => {
        void insertUploaded(editor, files);
      },
    }),
  ];
}
