"use client";

import { useState, type FormEvent } from "react";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { authApi } from "@/lib/api/auth";
import { createForgotPasswordSchema } from "@/lib/validation/forgotPasswordSchema";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";

export default function ForgotPasswordPage() {
  const t = useTranslations("auth.forgotPassword");
  const tValidation = useTranslations("validation");
  const [email, setEmail] = useState("");
  const [error, setError] = useState<string | undefined>();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();

    const result = createForgotPasswordSchema(tValidation).safeParse({ email });
    if (!result.success) {
      setError(result.error.flatten().fieldErrors.email?.[0]);
      return;
    }
    setError(undefined);

    setIsSubmitting(true);
    try {
      await authApi.forgotPassword(result.data);
    } catch {
      // Deliberately ignored — the backend already returns the same response whether or not the
      // email is registered; a network-level failure shouldn't reveal anything either, so show
      // the same generic confirmation regardless of what happened.
    } finally {
      setIsSubmitting(false);
      setSubmitted(true);
    }
  };

  return (
    <div className="flex flex-1 items-center justify-center px-4 py-12">
      <div className="w-full max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-800 dark:bg-gray-900">
        <h1 className="mb-6 text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        {submitted ? (
          <p className="text-sm text-gray-700 dark:text-gray-300">{t("submittedMessage")}</p>
        ) : (
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <p className="text-sm text-gray-600 dark:text-gray-400">{t("description")}</p>
            <FormField label={t("email")} htmlFor="email" error={error}>
              <Input
                id="email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                autoComplete="email"
              />
            </FormField>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? t("submitting") : t("submit")}
            </Button>
          </form>
        )}
        <p className="mt-4 text-sm text-gray-600 dark:text-gray-400">
          <Link href="/login" className="text-blue-600 hover:underline dark:text-blue-400">
            {t("backToLogin")}
          </Link>
        </p>
      </div>
    </div>
  );
}
