"use client";

import { useEffect, useState, type FormEvent } from "react";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { authApi } from "@/lib/api/auth";
import { ApiError } from "@/lib/api/httpClient";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";
import type { AuthResponse, EmailVerificationPendingResponse } from "@/types/api";

// The API's answer when the ticket itself is gone: no code can finish it, only a new sign-in can.
const TICKET_EXPIRED = "AUTH_VERIFICATION_EXPIRED";

function secondsUntil(iso: string): number {
  return Math.max(0, Math.ceil((new Date(iso).getTime() - Date.now()) / 1000));
}

/**
 * The emailed-code step (2026-09-24), shown in place by whichever page got the 202: register,
 * login, or a provider callback. The ticket lives only in this component's props — nothing goes to
 * storage — so a reload means signing in again, which hands out a new ticket for the same code.
 */
export function EmailVerificationStep({
  pending,
  onVerified,
}: {
  pending: EmailVerificationPendingResponse;
  onVerified: (auth: AuthResponse) => void;
}) {
  const t = useTranslations("auth.verifyEmail");
  const { verifyEmail } = useAuth();
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [ticketExpired, setTicketExpired] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [resendAvailableAt, setResendAvailableAt] = useState(pending.resendAvailableAt);
  const [secondsLeft, setSecondsLeft] = useState(() => secondsUntil(pending.resendAvailableAt));

  // The step replaces a form the visitor may have scrolled down; start them at its heading.
  useEffect(() => {
    window.scrollTo({ top: 0 });
  }, []);

  useEffect(() => {
    const tick = () => setSecondsLeft(secondsUntil(resendAvailableAt));
    tick();
    const id = window.setInterval(tick, 1000);
    return () => window.clearInterval(id);
  }, [resendAvailableAt]);

  const showError = (caught: unknown) => {
    const apiCode = caught instanceof ApiError && caught.body && typeof caught.body === "object" && "code" in caught.body
      ? String((caught.body as { code: unknown }).code)
      : null;
    setTicketExpired(apiCode === TICKET_EXPIRED);
    setError(caught instanceof ApiError ? caught.message : t("genericError"));
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setNotice(null);
    setIsSubmitting(true);
    try {
      onVerified(await verifyEmail({ verificationTicket: pending.verificationTicket, code }));
    } catch (caught) {
      showError(caught);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleResend = async () => {
    setError(null);
    setNotice(null);
    try {
      const response = await authApi.resendVerificationCode(pending.verificationTicket);
      setResendAvailableAt(response.resendAvailableAt);
      setNotice(t("resent"));
    } catch (caught) {
      showError(caught);
    }
  };

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="mb-2 text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="text-sm text-gray-600 dark:text-gray-400">
          {t.rich("sentTo", {
            email: () => <strong className="font-medium text-gray-900 dark:text-gray-100">{pending.email}</strong>,
          })}
        </p>
      </div>
      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <FormField label={t("codeLabel")} htmlFor="verification-code">
          <Input
            id="verification-code"
            value={code}
            onChange={(e) => setCode(e.target.value.replace(/\D/g, "").slice(0, 6))}
            inputMode="numeric"
            autoComplete="one-time-code"
            pattern="[0-9]{6}"
            maxLength={6}
            className="text-center font-mono text-lg tracking-[0.4em]"
            autoFocus
          />
        </FormField>
        {error && (
          <p role="alert" className="text-sm text-red-600 dark:text-red-400">
            {error}{" "}
            {ticketExpired && (
              <Link href="/login" className="text-blue-600 hover:underline dark:text-blue-400">
                {t("restart")}
              </Link>
            )}
          </p>
        )}
        {notice && <p className="text-sm text-good-ink">{notice}</p>}
        <Button type="submit" disabled={isSubmitting || code.length !== 6}>
          {isSubmitting ? t("submitting") : t("submit")}
        </Button>
      </form>
      <div className="flex flex-col gap-1 text-sm">
        <button
          type="button"
          onClick={() => void handleResend()}
          disabled={secondsLeft > 0 || ticketExpired}
          className="self-start text-blue-600 hover:underline disabled:cursor-not-allowed disabled:text-gray-400 disabled:no-underline dark:text-blue-400 dark:disabled:text-gray-500"
        >
          {secondsLeft > 0 ? t("resendIn", { seconds: secondsLeft }) : t("resend")}
        </button>
        <p className="text-gray-500 dark:text-gray-400">{t("spamHint")}</p>
      </div>
    </div>
  );
}
