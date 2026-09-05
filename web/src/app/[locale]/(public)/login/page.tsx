"use client";

import { useState, type FormEvent } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { postAuthLocale } from "@/lib/auth/postAuthRedirect";
import { applyTheme, type Theme } from "@/lib/theme/theme";
import { createLoginSchema } from "@/lib/validation/loginSchema";
import { ApiError } from "@/lib/api/httpClient";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";
import { GoogleSignInButton } from "@/components/auth/GoogleSignInButton";

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
      // Applies the account's saved theme the same way as the language
      // preference below — the account's saved value wins on login,
      // regardless of which device/browser the user is signing in from.
      applyTheme(auth.user.preferredTheme as Theme);
      // Applies the account's saved language preference right after login — see postAuthLocale.
      const nextLocale = postAuthLocale(auth, locale);
      if (nextLocale) {
        router.push("/dashboard", { locale: nextLocale });
      } else {
        router.push("/dashboard");
      }
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : t("genericError"));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="flex flex-1 items-center justify-center px-4 py-12">
      <div className="w-full max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-800 dark:bg-gray-900">
        <h1 className="mb-6 text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
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
          <Link
            href="/forgot-password"
            className="-mt-2 self-end text-sm text-blue-600 hover:underline dark:text-blue-400"
          >
            {t("forgotPasswordLink")}
          </Link>
          {formError && <p className="text-sm text-red-600 dark:text-red-400">{formError}</p>}
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? t("submitting") : t("submit")}
          </Button>
        </form>
        <GoogleSignInButton />
        <p className="mt-4 text-sm text-gray-600 dark:text-gray-400">
          {t("noAccount")}{" "}
          <Link href="/register" className="text-blue-600 hover:underline dark:text-blue-400">
            {t("registerLink")}
          </Link>
        </p>
      </div>
    </div>
  );
}
