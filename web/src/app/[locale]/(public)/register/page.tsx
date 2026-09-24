"use client";

import { useState, type FormEvent } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { postAuthDestination, postAuthLocale, returnToFromLocation } from "@/lib/auth/postAuthRedirect";
import { authApi } from "@/lib/api/auth";
import { getStoredThemeCookie } from "@/lib/theme/theme";
import { createRegisterSchema } from "@/lib/validation/registerSchema";
import { useClientConfig } from "@/hooks/useClientConfig";
import { ApiError } from "@/lib/api/httpClient";
import { trackSiteTraffic } from "@/lib/analytics/siteTraffic";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";
import { Checkbox } from "@/components/ui/Checkbox";
import { PasswordRequirements } from "@/components/ui/PasswordRequirements";
import { SocialSignIn } from "@/components/auth/SocialSignIn";
import { EmailVerificationStep } from "@/components/auth/EmailVerificationStep";
import type { AuthResponse, EmailVerificationPendingResponse } from "@/types/api";

type FieldErrors = Partial<Record<"email" | "password" | "confirmPassword" | "consentAccepted", string>>;

export default function RegisterPage() {
  const { register } = useAuth();
  const router = useRouter();
  const locale = useLocale();
  const t = useTranslations("auth.register");
  const tValidation = useTranslations("validation");
  // Same rules the server enforces (GET /api/config) — shown up front and validated client-side,
  // so the user never learns them one rejected submit at a time.
  const { config } = useClientConfig();
  const [values, setValues] = useState({
    email: "",
    password: "",
    confirmPassword: "",
    consentAccepted: false,
  });
  const [errors, setErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  // A sign-up gets no session until the emailed code comes back (2026-09-24).
  const [pending, setPending] = useState<EmailVerificationPendingResponse | null>(null);

  // Live "passwords match" feedback. Silent until the user has left the confirm field once
  // (so they aren't shown red text mid-typing), then re-checked on every keystroke in either
  // password field. The submit-time Zod check below stays as the final gate.
  const [confirmTouched, setConfirmTouched] = useState(false);
  const liveMismatch =
    confirmTouched && values.confirmPassword !== "" && values.password !== values.confirmPassword;
  const confirmPasswordError =
    errors.confirmPassword ?? (liveMismatch ? tValidation("passwordsDoNotMatch") : undefined);

  const update = (field: keyof typeof values) => (e: React.ChangeEvent<HTMLInputElement>) => {
    setValues((prev) => ({ ...prev, [field]: e.target.value }));
    // A submit-time mismatch error must not linger once the user starts fixing it —
    // the live check above takes over from here.
    if (field === "password" || field === "confirmPassword") {
      setErrors((prev) => (prev.confirmPassword ? { ...prev, confirmPassword: undefined } : prev));
    }
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);

    const result = createRegisterSchema(tValidation, config.passwordPolicy).safeParse(values);
    if (!result.success) {
      const fieldErrors = result.error.flatten().fieldErrors;
      setErrors({
        email: fieldErrors.email?.[0],
        password: fieldErrors.password?.[0],
        confirmPassword: fieldErrors.confirmPassword?.[0],
        consentAccepted: fieldErrors.consentAccepted?.[0],
      });
      return;
    }
    setErrors({});

    // Counted after client-side validation passes, so this measures "a filled-in form was sent",
    // not "someone typed something". Paired with register_completed below, the gap between the two
    // separates "nobody tries" from "people try and the server turns them away".
    trackSiteTraffic("register_started");

    setIsSubmitting(true);
    try {
      // confirmPassword is a client-side check only — the API never sees it. No name: the form
      // stopped asking for one on 2026-09-14 (Settings can add it later); the API takes the empty
      // strings and the header falls back to the e-mail until then.
      const { email, password, consentAccepted } = result.data;
      setPending(await register({ email, password, firstName: "", lastName: "", consentAccepted }));
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : t("genericError"));
    } finally {
      setIsSubmitting(false);
    }
  };

  const finishSignUp = (auth: AuthResponse) => {
    // Counted once the account exists and is verified — the step a sign-up now has to clear.
    trackSiteTraffic("register_completed");
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
    const nextLocale = postAuthLocale(auth, locale);
    // Same as the login page: a pairing confirmation the extension opened wins over the
    // dashboard, and nothing else can. See postAuthDestination.
    const destination = postAuthDestination(returnToFromLocation());
    if (nextLocale) {
      router.push(destination, { locale: nextLocale });
    } else {
      router.push(destination);
    }
  };

  return (
    <div className="flex flex-1 items-center justify-center px-4 py-12">
      <div className="w-full max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-800 dark:bg-gray-900">
        {pending ? (
          <EmailVerificationStep pending={pending} onVerified={finishSignUp} />
        ) : (
          <>
            <h1 className="mb-2 text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
            {/* What the account is for, before the form asks for anything — the page used to open on
                four empty fields and a password rule box. */}
            <ul className="mb-5 flex flex-col gap-1 text-sm text-gray-600 dark:text-gray-400">
              {(["item1", "item2", "item3"] as const).map((key) => (
                <li key={key} className="flex gap-2">
                  <span aria-hidden="true" className="text-blue-600 dark:text-blue-400">✓</span>
                  <span>{t(`benefits.${key}`)}</span>
                </li>
              ))}
            </ul>
            <SocialSignIn />
            <form onSubmit={handleSubmit} className="flex flex-col gap-4">
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
                  aria-describedby="password-requirements"
                />
                <PasswordRequirements id="password-requirements" password={values.password} policy={config.passwordPolicy} />
              </FormField>
              <FormField label={t("confirmPassword")} htmlFor="confirmPassword" error={confirmPasswordError}>
                <Input
                  id="confirmPassword"
                  type="password"
                  value={values.confirmPassword}
                  onChange={update("confirmPassword")}
                  onBlur={() => setConfirmTouched(true)}
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
                  {t.rich("consent", {
                      privacy: (chunks) => (
                        <Link href="/privacy" target="_blank" className="text-blue-600 hover:underline dark:text-blue-400">
                          {chunks}
                        </Link>
                      ),
                      terms: (chunks) => (
                        <Link href="/terms" target="_blank" className="text-blue-600 hover:underline dark:text-blue-400">
                          {chunks}
                        </Link>
                      ),
                    })}
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
          </>
        )}
      </div>
    </div>
  );
}
