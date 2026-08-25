"use client";

import { useState, type FormEvent } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import { routing } from "@/i18n/routing";
import { useAuth } from "@/lib/auth/AuthContext";
import { createLoginSchema } from "@/lib/validation/loginSchema";
import { ApiError } from "@/lib/api/httpClient";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";

export default function LoginPage() {
  const { login } = useAuth();
  const router = useRouter();
  const locale = useLocale();
  const t = useTranslations("auth.login");
  const tValidation = useTranslations("validation");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [errors, setErrors] = useState<{ email?: string; password?: string }>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);

    const result = createLoginSchema(tValidation).safeParse({ email, password });
    if (!result.success) {
      const fieldErrors = result.error.flatten().fieldErrors;
      setErrors({ email: fieldErrors.email?.[0], password: fieldErrors.password?.[0] });
      return;
    }
    setErrors({});

    setIsSubmitting(true);
    try {
      const auth = await login(result.data);
      // Applies the account's saved language preference right after login,
      // regardless of which device/browser the user is signing in from —
      // until they explicitly switch again via the language switcher.
      const preferredLanguage = auth.user.preferredLanguage;
      const supportedLocales: readonly string[] = routing.locales;
      if (supportedLocales.includes(preferredLanguage) && preferredLanguage !== locale) {
        router.push("/", { locale: preferredLanguage as (typeof routing.locales)[number] });
      } else {
        router.push("/");
      }
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : t("genericError"));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="flex flex-1 items-center justify-center px-4 py-12">
      <div className="w-full max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
        <h1 className="mb-6 text-xl font-semibold text-gray-900">{t("title")}</h1>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <FormField label={t("email")} htmlFor="email" error={errors.email}>
            <Input
              id="email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoComplete="email"
            />
          </FormField>
          <FormField label={t("password")} htmlFor="password" error={errors.password}>
            <Input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
            />
          </FormField>
          {formError && <p className="text-sm text-red-600">{formError}</p>}
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? t("submitting") : t("submit")}
          </Button>
        </form>
        <p className="mt-4 text-sm text-gray-600">
          {t("noAccount")}{" "}
          <Link href="/register" className="text-blue-600 hover:underline">
            {t("registerLink")}
          </Link>
        </p>
      </div>
    </div>
  );
}
