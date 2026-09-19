"use client";

import { useCallback, useRef, useState } from "react";
import Image from "@tiptap/extension-image";
import { NodeViewWrapper, ReactNodeViewRenderer, type NodeViewProps } from "@tiptap/react";
import { useMediaObjectUrl } from "./useMediaObjectUrl";

const MIN_WIDTH = 80;

/**
 * How an image renders *inside the editor*: from a blob fetched with the author's token (see
 * useMediaObjectUrl), with a drag handle on the right edge that sets the node's `width`. The
 * serialised HTML is untouched by this — it keeps the plain `<img src="/api/blog/media/…"
 * width="…">` the sanitizer accepts and the public page renders.
 */
function MediaImageView({ node, selected, updateAttributes, editor }: NodeViewProps) {
  const { src, alt, width } = node.attrs as { src: string; alt: string | null; width: number | null };
  const { url, failed } = useMediaObjectUrl(src);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const [dragging, setDragging] = useState(false);
  const [dragWidth, setDragWidth] = useState<number | null>(null);
  // While a drag is in progress the handle's width is shown; otherwise the node's own.
  const previewWidth = dragging ? dragWidth : width;

  const onHandleMouseDown = useCallback(
    (event: React.MouseEvent) => {
      if (!editor.isEditable) return;
      event.preventDefault();
      const wrapper = wrapperRef.current;
      if (!wrapper) return;
      const startX = event.clientX;
      const startWidth = wrapper.querySelector("img")?.getBoundingClientRect().width ?? width ?? MIN_WIDTH;
      const maxWidth = wrapper.parentElement?.getBoundingClientRect().width ?? Number.POSITIVE_INFINITY;
      let next = startWidth;
      setDragWidth(Math.round(startWidth));
      setDragging(true);

      const onMove = (move: MouseEvent) => {
        next = Math.round(Math.min(maxWidth, Math.max(MIN_WIDTH, startWidth + (move.clientX - startX))));
        setDragWidth(next);
      };
      const onUp = () => {
        window.removeEventListener("mousemove", onMove);
        window.removeEventListener("mouseup", onUp);
        setDragging(false);
        updateAttributes({ width: next });
      };
      window.addEventListener("mousemove", onMove);
      window.addEventListener("mouseup", onUp);
    },
    [editor.isEditable, updateAttributes, width],
  );

  return (
    <NodeViewWrapper ref={wrapperRef} className="relative my-4 inline-block max-w-full" data-drag-handle>
      {url ? (
        <img
          src={url}
          alt={alt ?? ""}
          style={previewWidth ? { width: previewWidth } : undefined}
          className={`block h-auto max-w-full rounded-lg ${selected ? "ring-2 ring-accent" : ""}`}
          draggable={false}
        />
      ) : (
        <div
          style={{ width: previewWidth ?? 320, height: previewWidth ? previewWidth * 0.6 : 200 }}
          className={`aa-skeleton flex max-w-full items-center justify-center rounded-lg text-xs text-gray-500 ${failed ? "border border-dashed border-red-300" : ""}`}
        >
          {failed ? "!" : null}
        </div>
      )}
      {editor.isEditable && (
        <span
          role="separator"
          aria-orientation="vertical"
          onMouseDown={onHandleMouseDown}
          className={`absolute top-1/2 -right-1.5 h-10 w-3 -translate-y-1/2 cursor-col-resize rounded-full bg-accent/70 ${
            selected || dragging ? "opacity-100" : "opacity-0 hover:opacity-100"
          }`}
        />
      )}
    </NodeViewWrapper>
  );
}

/**
 * The image node with `width` kept as an attribute (so a resize survives a reload and reaches
 * the HTML) and the view above. Base64 sources are refused at the node level too — the sanitizer
 * would strip them anyway, but there is no reason to let one into the document.
 */
export const MediaImage = Image.extend({
  addAttributes() {
    return {
      ...this.parent?.(),
      width: {
        default: null,
        parseHTML: (element) => {
          const value = element.getAttribute("width");
          return value ? Number.parseInt(value, 10) || null : null;
        },
        renderHTML: (attributes) => (attributes.width ? { width: String(attributes.width) } : {}),
      },
    };
  },

  addNodeView() {
    return ReactNodeViewRenderer(MediaImageView);
  },
}).configure({ allowBase64: false, inline: false });
