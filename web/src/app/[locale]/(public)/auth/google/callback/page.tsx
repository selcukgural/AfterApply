"use client";

import { Suspense, useEffect, useRef, useState, type FormEvent } from "react";
import { useSearchParams } from "next/navigation";
import { useLocale, useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { authApi } from "@/lib/api/auth";
import { consumeGoogleSignIn } from "@/lib/auth/googleOAuth";
import { postAuthLocale } from "@/lib/auth/postAuthRedirect";
import { applyTheme, getStoredThemeCookie, type Theme } from "@/lib/theme/theme";
import { createGoogleSignupSchema } from "@/lib/validation/googleSignupSchema";
import { ApiError } from "@/lib/api/httpClient";
import type { AuthResponse, GoogleSignupPrefill } from "@/types/api";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";
import { Checkbox } from "@/components/ui/Checkbox";

// Where accounts.google.com sends the browser back to (registered on the OAuth client as
// <origin>/{locale}/auth/google/callback). Three outcomes: signed in → dashboard; a Google
// account we don't know → the complete-your-sign-up form below; anything else → an error with a
// way back to login.
export default function GoogleCallbackPage() {
  // useSearchParams needs a Suspense boundary above it or the whole route bails out of static
  // rendering at build time.
  return (
    <Suspense fallback={null}>
      <GoogleCallback />
    </Suspense>
  );
}

type Phase =
  | { kind: "working" }
  | { kind: "signup"; prefill: GoogleSignupPrefill }
  | { kind: "error"; message: string };

function GoogleCallback() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const locale = useLocale();
  const t = useTranslations("auth.google.callback");
  const { signInWithGoogle } = useAuth();
  const [phase, setPhase] = useState<Phase>({ kind: "working" });
  // The authorization code is single-use and the stored PKCE state is consumed on first read, so
  // this effect must run exactly once — including under React Strict Mode's mount/unmount/mount
  // in development, which a ref (unlike state) survives.
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
      // Google reports a declined consent screen as ?error=access_denied rather than a code.
      if (searchParams.get("error")) {
        return { kind: "error", message: t("cancelled") };
      }

      const code = searchParams.get("code");
      const pending = consumeGoogleSignIn(searchParams.get("state"));
      if (!code || !pending) {
        return { kind: "error", message: t("invalidState") };
      }

      try {
        const result = await signInWithGoogle({
          code,
          codeVerifier: pending.codeVerifier,
          redirectUri: pending.redirectUri,
        });
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

type FieldErrors = Partial<Record<"firstName" | "lastName" | "consentAccepted", string>>;

function CompleteSignupForm({
  prefill,
  onSignedUp,
}: {
  prefill: GoogleSignupPrefill;
  onSignedUp: (auth: AuthResponse) => void;
}) {
  const t = useTranslations("auth.google.completeSignup");
  const tRegister = useTranslations("auth.register");
  const tValidation = useTranslations("validation");
  const { completeGoogleSignup } = useAuth();
  const [values, setValues] = useState({
    firstName: prefill.firstName,
    lastName: prefill.lastName,
    consentAccepted: false,
  });
  const [errors, setErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const update = (field: "firstName" | "lastName") => (e: React.ChangeEvent<HTMLInputElement>) =>
    setValues((prev) => ({ ...prev, [field]: e.target.value }));

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);

    const result = createGoogleSignupSchema(tValidation).safeParse(values);
    if (!result.success) {
      const fieldErrors = result.error.flatten().fieldErrors;
      setErrors({
        firstName: fieldErrors.firstName?.[0],
        lastName: fieldErrors.lastName?.[0],
        consentAccepted: fieldErrors.consentAccepted?.[0],
      });
      return;
    }
    setErrors({});

    setIsSubmitting(true);
    try {
      const auth = await completeGoogleSignup({ signupToken: prefill.signupToken, ...result.data });
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
        <FormField label={t("email")} htmlFor="google-email">
          <Input id="google-email" type="email" value={prefill.email} readOnly disabled />
        </FormField>
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t("firstName")} htmlFor="google-firstName" error={errors.firstName}>
            <Input id="google-firstName" value={values.firstName} onChange={update("firstName")} autoComplete="given-name" />
          </FormField>
          <FormField label={t("lastName")} htmlFor="google-lastName" error={errors.lastName}>
            <Input id="google-lastName" value={values.lastName} onChange={update("lastName")} autoComplete="family-name" />
          </FormField>
        </div>
        <Checkbox
          id="google-consentAccepted"
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
