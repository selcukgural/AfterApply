"use client";

import { useEffect, useRef, useState, type ChangeEvent } from "react";
import { useTranslations } from "next-intl";
import { authApi } from "@/lib/api/auth";
import { authStore } from "@/lib/api/authStore";
import { ApiError } from "@/lib/api/httpClient";
import { fieldErrorsOf } from "@/lib/api/validationErrors";
import type { UserProfileResponse } from "@/types/api";
import { Avatar } from "@/components/ui/Avatar";
import { AvatarCropDialog } from "./AvatarCropDialog";

/** What the file picker offers; the server decides by the bytes either way. */
const ACCEPTED_TYPES = "image/jpeg,image/png,image/webp";

/**
 * The photo on the profile card with its camera button (canvas variant A, DECISIONS.md
 * 2026-09-28). No photo: the button opens the file picker. A photo: it opens a two-item menu —
 * change, remove. Every change goes straight into the auth store, so the navbar follows at once.
 */
export function ProfileAvatarControl({ user, initials }: { user: UserProfileResponse; initials: string }) {
  const t = useTranslations("profile.avatar");
  const fileInputRef = useRef<HTMLInputElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const [menuOpen, setMenuOpen] = useState(false);
  const [chosen, setChosen] = useState<File | null>(null);
  const [removing, setRemoving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const avatarUrl = user.avatarUrl ?? null;

  useEffect(() => {
    if (!menuOpen) return;
    const close = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) setMenuOpen(false);
    };
    const escape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setMenuOpen(false);
    };
    document.addEventListener("mousedown", close);
    document.addEventListener("keydown", escape);
    return () => {
      document.removeEventListener("mousedown", close);
      document.removeEventListener("keydown", escape);
    };
  }, [menuOpen]);

  const pickFile = () => {
    setMenuOpen(false);
    setError(null);
    fileInputRef.current?.click();
  };

  const handleFile = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null;
    // Cleared so choosing the same file again still fires a change.
    event.target.value = "";
    if (file) setChosen(file);
  };

  const handleSave = async (photo: Blob) => {
    try {
      authStore.updateUser(await authApi.uploadAvatar(photo));
      setChosen(null);
    } catch (err) {
      // The dialog shows it and stays open.
      throw new Error(fieldErrorsOf(err).file ?? (err instanceof ApiError && err.message ? err.message : t("saveError")));
    }
  };

  const handleRemove = async () => {
    setMenuOpen(false);
    setError(null);
    setRemoving(true);
    try {
      authStore.updateUser(await authApi.deleteAvatar());
    } catch (err) {
      setError(err instanceof ApiError && err.message ? err.message : t("removeError"));
    } finally {
      setRemoving(false);
    }
  };

  const menuItemClass =
    "flex h-10 w-full items-center rounded-md px-2.5 text-left text-sm hover:bg-gray-100 disabled:opacity-50 dark:hover:bg-gray-800";

  return (
    <div ref={containerRef} className="relative h-14 w-14 shrink-0">
      <Avatar src={avatarUrl} initials={initials} className="h-14 w-14 bg-accent-wash text-lg font-semibold text-accent-ink" />
      <button
        type="button"
        onClick={avatarUrl ? () => setMenuOpen((open) => !open) : pickFile}
        aria-label={avatarUrl ? t("changeLabel") : t("uploadLabel")}
        aria-haspopup={avatarUrl ? "menu" : undefined}
        aria-expanded={avatarUrl ? menuOpen : undefined}
        disabled={removing}
        // 32 px drawn, 44 px to touch: the ::after extends the hit area past the circle.
        className="absolute -bottom-1 -right-1 flex h-8 w-8 items-center justify-center rounded-full border-2 border-white bg-accent text-white after:absolute after:-inset-1.5 after:content-[''] hover:bg-accent-strong disabled:opacity-60 dark:border-gray-900"
      >
        <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          <path d="M4 8h3l2-3h6l2 3h3v11H4z" />
          <circle cx="12" cy="13" r="3.5" />
        </svg>
      </button>

      {menuOpen && (
        <div
          role="menu"
          className="absolute left-0 top-16 z-20 flex w-52 flex-col rounded-xl border border-gray-200 bg-white p-1.5 shadow-lg dark:border-gray-800 dark:bg-gray-900"
        >
          <button type="button" role="menuitem" onClick={pickFile} className={`${menuItemClass} text-gray-900 dark:text-gray-100`}>
            {t("change")}
          </button>
          <button type="button" role="menuitem" onClick={handleRemove} className={`${menuItemClass} text-red-700 dark:text-red-400`}>
            {t("remove")}
          </button>
        </div>
      )}

      <input ref={fileInputRef} type="file" accept={ACCEPTED_TYPES} onChange={handleFile} className="sr-only" tabIndex={-1} aria-hidden="true" />

      {error && (
        <p role="alert" className="absolute left-0 top-16 w-60 text-xs text-red-600 dark:text-red-400">
          {error}
        </p>
      )}

      {chosen && <AvatarCropDialog file={chosen} onCancel={() => setChosen(null)} onSave={handleSave} />}
    </div>
  );
}
