"use client";

import { useEffect, useRef, useState, type KeyboardEvent, type PointerEvent } from "react";
import { useTranslations } from "next-intl";
import { Modal } from "@/components/ui/Modal";
import { Button } from "@/components/ui/Button";
import { MAX_ZOOM, MIN_ZOOM, OUTPUT_SIZE, initialCrop, pan, sourceRect, zoomTo, type CropState } from "@/lib/avatar/crop";

const VIEWPORT = 280;
const KEY_STEP = 10;

interface AvatarCropDialogProps {
  file: File;
  onCancel: () => void;
  /** Receives the cropped square as a JPEG; throws to keep the dialog open with its message. */
  onSave: (photo: Blob) => Promise<void>;
}

/**
 * Positions a chosen photo in the circle (canvas variant A, DECISIONS.md 2026-09-28). The crop is
 * the browser's job: the server takes the square it is given, re-encodes it at 256 px, and keeps
 * nothing else — so there is no original to re-crop later, and "change" means choosing again.
 * The photo is read through a blob: URL (the CSP allows it) and never leaves the page uncropped.
 */
export function AvatarCropDialog({ file, onCancel, onSave }: AvatarCropDialogProps) {
  const t = useTranslations("profile.avatar");
  const imageRef = useRef<HTMLImageElement>(null);
  const drag = useRef<{ pointerId: number; x: number; y: number } | null>(null);
  const [size, setSize] = useState<{ width: number; height: number } | null>(null);
  const [crop, setCrop] = useState<CropState | null>(null);
  const [unreadable, setUnreadable] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // The blob: URL is handed to the <img> element directly rather than through state: it is a
  // resource with a lifetime (revoked on cleanup), and React's double-run of effects in development
  // must create a fresh one rather than render a revoked one.
  useEffect(() => {
    const image = imageRef.current;
    if (!image) return;
    const objectUrl = URL.createObjectURL(file);
    image.src = objectUrl;
    return () => URL.revokeObjectURL(objectUrl);
  }, [file]);

  const handleLoad = () => {
    const image = imageRef.current;
    if (!image || image.naturalWidth === 0) {
      setUnreadable(true);
      return;
    }
    const natural = { width: image.naturalWidth, height: image.naturalHeight };
    setSize(natural);
    setCrop(initialCrop(natural.width, natural.height, VIEWPORT));
  };

  const move = (dx: number, dy: number) => {
    if (!crop || !size) return;
    setCrop(pan(crop, dx, dy, size.width, size.height, VIEWPORT));
  };

  const handlePointerDown = (event: PointerEvent<HTMLDivElement>) => {
    event.currentTarget.setPointerCapture(event.pointerId);
    drag.current = { pointerId: event.pointerId, x: event.clientX, y: event.clientY };
  };

  const handlePointerMove = (event: PointerEvent<HTMLDivElement>) => {
    const last = drag.current;
    if (!last || last.pointerId !== event.pointerId) return;
    move(event.clientX - last.x, event.clientY - last.y);
    drag.current = { pointerId: event.pointerId, x: event.clientX, y: event.clientY };
  };

  const handlePointerUp = () => {
    drag.current = null;
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const steps: Record<string, [number, number]> = {
      ArrowLeft: [KEY_STEP, 0],
      ArrowRight: [-KEY_STEP, 0],
      ArrowUp: [0, KEY_STEP],
      ArrowDown: [0, -KEY_STEP],
    };
    const step = steps[event.key];
    if (step) {
      event.preventDefault();
      move(step[0], step[1]);
    }
  };

  const handleSave = async () => {
    const image = imageRef.current;
    if (!image || !crop || !size) return;
    setError(null);
    setSaving(true);
    try {
      const rect = sourceRect(crop, size.width, size.height, VIEWPORT);
      const canvas = document.createElement("canvas");
      canvas.width = OUTPUT_SIZE;
      canvas.height = OUTPUT_SIZE;
      const context = canvas.getContext("2d");
      if (!context) throw new Error("no-canvas");
      // JPEG has no transparency: a transparent PNG lands on white rather than black.
      context.fillStyle = "#ffffff";
      context.fillRect(0, 0, OUTPUT_SIZE, OUTPUT_SIZE);
      context.drawImage(image, rect.sx, rect.sy, rect.size, rect.size, 0, 0, OUTPUT_SIZE, OUTPUT_SIZE);
      const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, "image/jpeg", 0.9));
      if (!blob) throw new Error("no-blob");
      await onSave(blob);
    } catch (err) {
      setError(err instanceof Error && err.message && !["no-canvas", "no-blob"].includes(err.message) ? err.message : t("saveError"));
      setSaving(false);
    }
  };

  const scale = crop && size ? (VIEWPORT / Math.min(size.width, size.height)) * crop.zoom : 1;

  return (
    <Modal
      title={t("cropTitle")}
      onClose={onCancel}
      busy={saving}
      footer={
        <>
          <Button type="button" variant="secondary" onClick={onCancel} disabled={saving}>
            {t("cancel")}
          </Button>
          <Button type="button" onClick={handleSave} disabled={saving || !crop}>
            {saving ? t("saving") : t("savePhoto")}
          </Button>
        </>
      }
    >
      <h2 className="text-lg font-semibold">{t("cropTitle")}</h2>
      {unreadable ? (
        <p role="alert" className="mt-4 text-sm text-red-600 dark:text-red-400">
          {t("unreadable")}
        </p>
      ) : (
        <div className="mt-4 flex flex-col items-center gap-4">
          <div
            tabIndex={0}
            aria-label={t("positionLabel")}
            onPointerDown={handlePointerDown}
            onPointerMove={handlePointerMove}
            onPointerUp={handlePointerUp}
            onPointerCancel={handlePointerUp}
            onKeyDown={handleKeyDown}
            className="relative cursor-grab touch-none select-none overflow-hidden rounded-md bg-gray-900 outline-none focus-visible:ring-2 focus-visible:ring-accent active:cursor-grabbing"
            style={{ width: VIEWPORT, height: VIEWPORT }}
          >
            {
              // eslint-disable-next-line @next/next/no-img-element -- a local blob: URL being cropped, not a served image
              <img
                ref={imageRef}
                alt=""
                draggable={false}
                onLoad={handleLoad}
                onError={() => setUnreadable(true)}
                className="absolute left-0 top-0 max-w-none origin-top-left"
                style={
                  crop && size
                    ? { width: size.width * scale, height: size.height * scale, transform: `translate(${crop.x}px, ${crop.y}px)` }
                    : { visibility: "hidden" }
                }
              />
            }
            {/* The circle the photo will be shown in; the corners are dimmed, not cut, because
                the square is what is sent. */}
            <div aria-hidden="true" className="pointer-events-none absolute inset-0 rounded-full shadow-[0_0_0_400px_rgba(17,24,39,0.55)] ring-2 ring-white" />
          </div>
          <label className="flex w-full items-center gap-3 text-sm font-medium text-gray-700 dark:text-gray-300">
            {t("zoom")}
            <input
              type="range"
              min={MIN_ZOOM}
              max={MAX_ZOOM}
              step={0.01}
              value={crop?.zoom ?? MIN_ZOOM}
              disabled={!crop}
              onChange={(event) => crop && size && setCrop(zoomTo(crop, Number(event.target.value), size.width, size.height, VIEWPORT))}
              className="flex-1 accent-accent"
            />
          </label>
          <p className="text-xs text-gray-600 dark:text-gray-400">{t("cropHint")}</p>
        </div>
      )}
      {error && (
        <p role="alert" className="mt-3 text-sm text-red-600 dark:text-red-400">
          {error}
        </p>
      )}
    </Modal>
  );
}
