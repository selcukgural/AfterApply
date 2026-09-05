"use client";

import { Suspense, useEffect, useRef, useState, type FormEvent } from "react";
import { useSearchParams } from "next/navigation";
import { useLocale, useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { authApi } from "@/lib/api/auth";
import { consumeLinkedInSignIn } from "@/lib/auth/linkedinOAuth";
import { postAuthLocale } from "@/lib/auth/postAuthRedirect";
import { applyTheme, getStoredThemeCookie, type Theme } from "@/lib/theme/theme";
import { createLinkedInSignupSchema } from "@/lib/validation/linkedinSignupSchema";
import { ApiError } from "@/lib/api/httpClient";
import type { AuthResponse, LinkedInSignupPrefill } from "@/types/api";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";
import { Checkbox } from "@/components/ui/Checkbox";

// Where linkedin.com sends the browser back to (registered on the LinkedIn app as
// <origin>/{locale}/auth/linkedin/callback). Three outcomes: signed in → dashboard; a LinkedIn
// account we don't know → the complete-your-sign-up form below; anything else → an error with a
// way back to login.
export default function LinkedInCallbackPage() {
  // useSearchParams needs a Suspense boundary above it or the whole route bails out of static
  // rendering at build time.
  return (
    <Suspense fallback={null}>
      <LinkedInCallback />
    </Suspense>
  );
}

type Phase =
  | { kind: "working" }
  | { kind: "signup"; prefill: LinkedInSignupPrefill }
  | { kind: "error"; message: string };

function LinkedInCallback() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const locale = useLocale();
  const t = useTranslations("auth.linkedin.callback");
  const { signInWithLinkedIn } = useAuth();
  const [phase, setPhase] = useState<Phase>({ kind: "working" });
  // The authorization code is single-use and the stored state is consumed on first read, so this
  // effect must run exactly once — including under React Strict Mode's mount/unmount/mount in
  // development, which a ref (unlike state) survives.
  const started = useRef(false);

  const finishSignIn = (auth: AuthResponse) => {
    // Same post-login handling as the login page: the account's saved theme and language win.
    applyTheme(auth.user.preferredTheme as Theme);
    const nextLocale = postAuthLocale(auth, locale);
    if (nextLocale) {
      router.replace("/dashboard", { locale: nextLocale });
    } else {
      router.replace("/dashboard");
    }
  };

  useEffect(() => {
    if (started.current) return;
    started.current = true;

    const resolve = async (): Promise<Phase | null> => {
      // LinkedIn reports a declined login/consent as ?error=user_cancelled_login /
      // user_cancelled_authorize rather than a code.
      if (searchParams.get("error")) {
        return { kind: "error", message: t("cancelled") };
      }

      const code = searchParams.get("code");
      const pending = consumeLinkedInSignIn(searchParams.get("state"));
      if (!code || !pending) {
        return { kind: "error", message: t("invalidState") };
      }

      try {
        const result = await signInWithLinkedIn({ code, redirectUri: pending.redirectUri });
        if (result.auth) {
          finishSignIn(result.auth);
          return null;
        }
        if (result.pendingSignup) {
          return { kind: "signup", prefill: result.pendingSignup };
        }
        return { kind: "error", message: t("genericError") };
      } catch (error) {
        return { kind: "error", message: error instanceof ApiError ? error.message : t("genericError") };
      }
    };

    void resolve().then((next) => {
      if (next) setPhase(next);
    });
    // Runs once on mount by design (see `started` above); the values it reads are fixed for the
    // lifetime of this page load.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="flex flex-1 items-center justify-center px-4 py-12">
      <div className="w-full max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-800 dark:bg-gray-900">
        {phase.kind === "working" && (
          <>
            <h1 className="mb-4 text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
            <p className="text-sm text-gray-600 dark:text-gray-400">{t("working")}</p>
          </>
        )}
        {phase.kind === "error" && (
          <>
            <h1 className="mb-4 text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
            <p className="text-sm text-red-600 dark:text-red-400">{phase.message}</p>
            <p className="mt-4 text-sm text-gray-600 dark:text-gray-400">
              <Link href="/login" className="text-blue-600 hover:underline dark:text-blue-400">
                {t("backToLogin")}
              </Link>
            </p>
          </>
        )}
        {phase.kind === "signup" && <CompleteSignupForm prefill={phase.prefill} onSignedUp={finishSignIn} />}
      </div>
    </div>
  );
}

type FieldErrors = Partial<Record<"email" | "firstName" | "lastName" | "consentAccepted", string>>;

function CompleteSignupForm({
  prefill,
  onSignedUp,
}: {
  prefill: LinkedInSignupPrefill;
  onSignedUp: (auth: AuthResponse) => void;
}) {
  const t = useTranslations("auth.linkedin.completeSignup");
  const tRegister = useTranslations("auth.register");
  const tValidation = useTranslations("validation");
  const { completeLinkedInSignup } = useAuth();
  // LinkedIn's OpenID Connect response makes email optional. When it came through (verified) it
  // is shown read-only exactly like Google's; when it didn't, the user has to type one — and is
  // told plainly that this is LinkedIn not sharing it, not something they did wrong.
  const requiresEmail = prefill.email === null;
  const [values, setValues] = useState({
    email: prefill.email ?? "",
    firstName: prefill.firstName,
    lastName: prefill.lastName,
    consentAccepted: false,
  });
  const [errors, setErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const update = (field: "email" | "firstName" | "lastName") => (e: React.ChangeEvent<HTMLInputElement>) =>
    setValues((prev) => ({ ...prev, [field]: e.target.value }));

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);

    const result = createLinkedInSignupSchema(tValidation, requiresEmail).safeParse(values);
    if (!result.success) {
      const fieldErrors = result.error.flatten().fieldErrors;
      setErrors({
        email: fieldErrors.email?.[0],
        firstName: fieldErrors.firstName?.[0],
        lastName: fieldErrors.lastName?.[0],
        consentAccepted: fieldErrors.consentAccepted?.[0],
      });
      return;
    }
    setErrors({});

    setIsSubmitting(true);
    try {
      const auth = await completeLinkedInSignup({
        signupToken: prefill.signupToken,
        firstName: result.data.firstName,
        lastName: result.data.lastName,
        consentAccepted: result.data.consentAccepted,
        // Only when LinkedIn gave us none — the API ignores it otherwise.
        email: requiresEmail ? result.data.email : undefined,
      });
      // Same as the register page: push a theme already chosen on this browser up to the new
      // account instead of letting it snap back to the server default.
      const localTheme = getStoredThemeCookie();
      if (localTheme && localTheme !== auth.user.preferredTheme) {
        void authApi.updateTheme(localTheme);
      }
      onSignedUp(auth);
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : t("genericError"));
      setIsSubmitting(false);
    }
  };

  return (
    <>
      <h1 className="mb-2 text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
      <p className="mb-6 text-sm text-gray-600 dark:text-gray-400">{t("description")}</p>
      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        {requiresEmail && (
          <p className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-200">
            {t("emailMissing")}
          </p>
        )}
        <FormField label={t("email")} htmlFor="linkedin-email" error={errors.email}>
          {requiresEmail ? (
            <Input
              id="linkedin-email"
              type="email"
              value={values.email}
              onChange={update("email")}
              autoComplete="email"
              required
            />
          ) : (
            <Input id="linkedin-email" type="email" value={values.email} readOnly disabled />
          )}
        </FormField>
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t("firstName")} htmlFor="linkedin-firstName" error={errors.firstName}>
            <Input id="linkedin-firstName" value={values.firstName} onChange={update("firstName")} autoComplete="given-name" />
          </FormField>
          <FormField label={t("lastName")} htmlFor="linkedin-lastName" error={errors.lastName}>
            <Input id="linkedin-lastName" value={values.lastName} onChange={update("lastName")} autoComplete="family-name" />
          </FormField>
        </div>
        <Checkbox
          id="linkedin-consentAccepted"
          checked={values.consentAccepted}
          onChange={(e) => setValues((prev) => ({ ...prev, consentAccepted: e.target.checked }))}
          error={errors.consentAccepted}
          label={
            <>
              <Link href="/privacy" target="_blank" className="text-blue-600 hover:underline dark:text-blue-400">
                {tRegister("consentLink")}
              </Link>{" "}
              {tRegister("consentSuffix")}
            </>
          }
        />
        {formError && <p className="text-sm text-red-600 dark:text-red-400">{formError}</p>}
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? t("submitting") : t("submit")}
        </Button>
      </form>
    </>
  );
}
