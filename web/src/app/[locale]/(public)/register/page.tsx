"use client";

import { useState, type FormEvent } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import { routing } from "@/i18n/routing";
import { useAuth } from "@/lib/auth/AuthContext";
import { authApi } from "@/lib/api/auth";
import { getStoredThemeCookie } from "@/lib/theme/theme";
import { createRegisterSchema } from "@/lib/validation/registerSchema";
import { ApiError } from "@/lib/api/httpClient";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";
import { Checkbox } from "@/components/ui/Checkbox";

type FieldErrors = Partial<Record<"email" | "password" | "firstName" | "lastName" | "consentAccepted", string>>;

export default function RegisterPage() {
  const { register } = useAuth();
  const router = useRouter();
  const locale = useLocale();
  const t = useTranslations("auth.register");
  const tValidation = useTranslations("validation");
  const [values, setValues] = useState({
    email: "",
    password: "",
    firstName: "",
    lastName: "",
    consentAccepted: false,
  });
  const [errors, setErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const update = (field: keyof typeof values) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setValues((prev) => ({ ...prev, [field]: e.target.value }));

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);

    const result = createRegisterSchema(tValidation).safeParse(values);
    if (!result.success) {
      const fieldErrors = result.error.flatten().fieldErrors;
      setErrors({
        email: fieldErrors.email?.[0],
        password: fieldErrors.password?.[0],
        firstName: fieldErrors.firstName?.[0],
        lastName: fieldErrors.lastName?.[0],
        consentAccepted: fieldErrors.consentAccepted?.[0],
      });
      return;
    }
    setErrors({});

    setIsSubmitting(true);
    try {
      const auth = await register(result.data);
      // A brand-new account always starts with the server default theme
      // ("light" — there's no Accept-Language-like header for OS theme
      // preference). If this visitor had already switched to Dark on this
      // browser before registering, push that choice up to the new account
      // instead of letting it snap back to Light right after signup.
      const localTheme = getStoredThemeCookie();
      if (localTheme && localTheme !== auth.user.preferredTheme) {
        void authApi.updateTheme(localTheme);
      }
      // Same post-login redirect rule as the login page — kept for
      // consistency even though a fresh registration's preferredLanguage
      // should already match the current locale (see AuthService.RegisterAsync).
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
      <div className="w-full max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-800 dark:bg-gray-900">
        <h1 className="mb-6 text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <div className="grid grid-cols-2 gap-3">
            <FormField label={t("firstName")} htmlFor="firstName" error={errors.firstName}>
              <Input id="firstName" value={values.firstName} onChange={update("firstName")} />
            </FormField>
            <FormField label={t("lastName")} htmlFor="lastName" error={errors.lastName}>
              <Input id="lastName" value={values.lastName} onChange={update("lastName")} />
            </FormField>
          </div>
          <FormField label={t("email")} htmlFor="email" error={errors.email}>
            <Input id="email" type="email" value={values.email} onChange={update("email")} autoComplete="email" />
          </FormField>
          <FormField label={t("password")} htmlFor="password" error={errors.password}>
            <Input
              id="password"
              type="password"
              value={values.password}
              onChange={update("password")}
              autoComplete="new-password"
            />
          </FormField>
          <Checkbox
            id="consentAccepted"
            checked={values.consentAccepted}
            onChange={(e) => setValues((prev) => ({ ...prev, consentAccepted: e.target.checked }))}
            error={errors.consentAccepted}
            label={
              <>
                <Link href="/privacy" target="_blank" className="text-blue-600 hover:underline dark:text-blue-400">
                  {t("consentLink")}
                </Link>{" "}
                {t("consentSuffix")}
              </>
            }
          />
          {formError && <p className="text-sm text-red-600 dark:text-red-400">{formError}</p>}
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? t("submitting") : t("submit")}
          </Button>
        </form>
        <p className="mt-4 text-sm text-gray-600 dark:text-gray-400">
          {t("haveAccount")}{" "}
          <Link href="/login" className="text-blue-600 hover:underline dark:text-blue-400">
            {t("loginLink")}
          </Link>
        </p>
      </div>
    </div>
  );
}
