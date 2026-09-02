"use client";

import { useState, type FormEvent } from "react";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import { authApi } from "@/lib/api/auth";
import { createResetPasswordSchema } from "@/lib/validation/resetPasswordSchema";
import { ApiError } from "@/lib/api/httpClient";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";

export default function ResetPasswordPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const t = useTranslations("auth.resetPassword");
  const tValidation = useTranslations("validation");

  // Both come straight from the link in the password-reset email (see
  // AuthService.ForgotPasswordAsync) — never rendered back to the user, only forwarded as-is.
  const email = searchParams.get("email") ?? "";
  const token = searchParams.get("token") ?? "";

  const [values, setValues] = useState({ newPassword: "", confirmPassword: "" });
  const [errors, setErrors] = useState<{ newPassword?: string; confirmPassword?: string }>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const update = (field: keyof typeof values) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setValues((prev) => ({ ...prev, [field]: e.target.value }));

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);

    const result = createResetPasswordSchema(tValidation).safeParse(values);
    if (!result.success) {
      const fieldErrors = result.error.flatten().fieldErrors;
      setErrors({ newPassword: fieldErrors.newPassword?.[0], confirmPassword: fieldErrors.confirmPassword?.[0] });
      return;
    }
    setErrors({});

    setIsSubmitting(true);
    try {
      await authApi.resetPassword({ email, token, newPassword: result.data.newPassword });
      router.push("/login");
    } catch (error) {
      // Deliberately generic on purpose in most cases (see ResetPasswordAsync — an expired token
      // and a bogus one return identical wording) — the backend's own localized message is shown
      // as-is, this fallback only covers a raw network failure.
      setFormError(error instanceof ApiError ? error.message : t("genericError"));
    } finally {
      setIsSubmitting(false);
    }
  };

  if (!email || !token) {
    return (
      <div className="flex flex-1 items-center justify-center px-4 py-12">
        <div className="w-full max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-800 dark:bg-gray-900">
          <p className="text-sm text-red-600 dark:text-red-400">{t("invalidLink")}</p>
          <p className="mt-4 text-sm text-gray-600 dark:text-gray-400">
            <Link href="/forgot-password" className="text-blue-600 hover:underline dark:text-blue-400">
              {t("requestNewLink")}
            </Link>
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-1 items-center justify-center px-4 py-12">
      <div className="w-full max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-800 dark:bg-gray-900">
        <h1 className="mb-6 text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <FormField label={t("newPassword")} htmlFor="newPassword" error={errors.newPassword}>
            <Input
              id="newPassword"
              type="password"
              value={values.newPassword}
              onChange={update("newPassword")}
              autoComplete="new-password"
            />
          </FormField>
          <FormField label={t("confirmPassword")} htmlFor="confirmPassword" error={errors.confirmPassword}>
            <Input
              id="confirmPassword"
              type="password"
              value={values.confirmPassword}
              onChange={update("confirmPassword")}
              autoComplete="new-password"
            />
          </FormField>
          {formError && <p className="text-sm text-red-600 dark:text-red-400">{formError}</p>}
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? t("submitting") : t("submit")}
          </Button>
        </form>
      </div>
    </div>
  );
}
