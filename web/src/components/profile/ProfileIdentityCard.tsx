"use client";

import { useState, type FormEvent } from "react";
import { useLocale, useTranslations } from "next-intl";
import { authApi } from "@/lib/api/auth";
import { authStore } from "@/lib/api/authStore";
import { ApiError } from "@/lib/api/httpClient";
import { fieldErrorsOf } from "@/lib/api/validationErrors";
import { displayName } from "@/lib/auth/displayName";
import { createProfileSchema } from "@/lib/validation/profileSchema";
import type { UserProfileResponse } from "@/types/api";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";

/**
 * Who the account belongs to and since when, with the one thing about it a person can change:
 * the name. The e-mail is drawn as text, never as an input — there is no endpoint to change it,
 * and sign-in, the extension pairing and every notification hang off it. A saved name goes into
 * the auth store straight away so the navbar's avatar and label follow without a reload.
 */
export function ProfileIdentityCard({ user }: { user: UserProfileResponse }) {
  const t = useTranslations("profile.identity");
  const tValidation = useTranslations("validation");
  const locale = useLocale();

  const [firstName, setFirstName] = useState(user.firstName);
  const [lastName, setLastName] = useState(user.lastName);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  const { name, initials } = displayName(user);
  const hasName = Boolean(user.firstName.trim() || user.lastName.trim());
  const memberSince = new Intl.DateTimeFormat(locale, { dateStyle: "long" }).format(new Date(user.createdAt));
  const dirty = firstName !== user.firstName || lastName !== user.lastName;

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);
    setSaved(false);

    const result = createProfileSchema(tValidation).safeParse({ firstName, lastName });
    if (!result.success) {
      const fieldErrors = result.error.flatten().fieldErrors as Record<string, string[] | undefined>;
      setErrors(Object.fromEntries(Object.entries(fieldErrors).map(([k, v]) => [k, v?.[0] ?? ""])));
      return;
    }
    setErrors({});

    setIsSaving(true);
    try {
      const profile = await authApi.updateProfile(result.data);
      authStore.updateUser(profile);
      setFirstName(profile.firstName);
      setLastName(profile.lastName);
      setSaved(true);
    } catch (error) {
      const fieldErrors = fieldErrorsOf(error);
      if (Object.keys(fieldErrors).length > 0) {
        setErrors(fieldErrors);
      } else {
        setFormError(error instanceof ApiError ? error.message : t("saveError"));
      }
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <section className="flex flex-col gap-4 rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
      <div className="flex items-start gap-4">
        <span
          aria-hidden="true"
          className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-accent-wash text-lg font-semibold text-accent-ink"
        >
          {initials}
        </span>
        <div className="min-w-0">
          <h2 className="truncate text-lg font-semibold text-gray-900 dark:text-gray-100">{name}</h2>
          <p className="truncate text-sm text-gray-600 dark:text-gray-400">{user.email}</p>
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("memberSince", { date: memberSince })}</p>
        </div>
      </div>

      {!hasName && (
        <p className="rounded-lg bg-accent-wash px-3 py-2 text-sm text-accent-ink">{t("noNameHint")}</p>
      )}

      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <div className="grid gap-3 sm:grid-cols-2">
          <FormField label={t("firstName")} htmlFor="profile-firstName" error={errors.firstName}>
            <Input
              id="profile-firstName"
              autoComplete="given-name"
              maxLength={100}
              value={firstName}
              onChange={(e) => setFirstName(e.target.value)}
            />
          </FormField>
          <FormField label={t("lastName")} htmlFor="profile-lastName" error={errors.lastName}>
            <Input
              id="profile-lastName"
              autoComplete="family-name"
              maxLength={100}
              value={lastName}
              onChange={(e) => setLastName(e.target.value)}
            />
          </FormField>
        </div>

        {/* Read-only on purpose: text with a lock, not a disabled input, so nothing suggests it
            could be unlocked. */}
        <div className="flex flex-col gap-1">
          <span className="text-sm font-medium text-gray-700 dark:text-gray-300">{t("email")}</span>
          <div className="flex items-center justify-between gap-2 rounded-md border border-gray-200 bg-gray-50 px-3 py-2 text-sm text-gray-600 dark:border-gray-800 dark:bg-gray-950 dark:text-gray-400">
            <span className="truncate">{user.email}</span>
            <svg viewBox="0 0 24 24" className="h-4 w-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
              <rect x="4" y="10" width="16" height="11" rx="2" />
              <path d="M8 10V7a4 4 0 0 1 8 0v3" />
            </svg>
          </div>
          <p className="text-xs text-gray-500 dark:text-gray-400">{t("emailLocked")}</p>
        </div>

        {formError && (
          <p role="alert" className="text-sm text-red-600 dark:text-red-400">
            {formError}
          </p>
        )}

        <div className="flex items-center gap-3">
          <Button type="submit" disabled={isSaving || !dirty}>
            {isSaving ? t("saving") : t("save")}
          </Button>
          {saved && !dirty && (
            <span role="status" className="text-sm text-good-ink">
              {t("saved")}
            </span>
          )}
        </div>
      </form>
    </section>
  );
}
