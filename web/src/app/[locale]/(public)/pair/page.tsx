"use client";

import { Suspense, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { extensionPairingApi } from "@/lib/api/extensionPairing";
import { ApiError } from "@/lib/api/httpClient";
import { Button } from "@/components/ui/Button";
import type { ExtensionPairingStatus } from "@/types/api";

/**
 * The confirmation step of connecting the browser extension to an account.
 *
 * What it replaces: generate a token in Settings → copy it → open the extension's options page →
 * paste it. Six steps and a secret carried by hand between two applications, in front of the part
 * of the product that costs the least effort to use.
 *
 * The extension starts the pairing and opens this page with the code in the URL. Two things have
 * to be true for it to be safe, and both are visible on screen rather than assumed: the person
 * confirming is signed in (so the pairing gets *their* account), and the code they are looking at
 * is the one their own extension is showing. The second is why the code is displayed large and why
 * "this wasn't me" is a button rather than a support email — a device-code flow's one real attack
 * is someone else's code arriving in your inbox.
 */
export default function PairPage() {
  // useSearchParams needs a Suspense boundary above it or the route bails out of static rendering.
  return (
    <Suspense fallback={null}>
      <Pair />
    </Suspense>
  );
}

/** What the server has said so far. The situations that need no server — still checking the
 * session, signed out, no code in the URL — are derived at render time instead (see `phase`
 * below), which keeps the effect down to the one thing it is for: the fetch. */
type Remote =
  | { kind: "idle" }
  | { kind: "notFound" }
  | { kind: "error"; message: string }
  | { kind: "review"; status: ExtensionPairingStatus };

type Phase = Remote | { kind: "loading" } | { kind: "signedOut" } | { kind: "noCode" };

/** Same shape the API normalizes to; only for display, the server re-does it on every lookup. */
function normalizeCode(raw: string | null): string {
  return (raw ?? "").toUpperCase().replace(/[^A-Z0-9]/g, "").slice(0, 8);
}

function formatCode(code: string): string {
  return code.length === 8 ? `${code.slice(0, 4)}-${code.slice(4)}` : code;
}

function Pair() {
  const searchParams = useSearchParams();
  const t = useTranslations("pair");
  const { isAuthenticated, isLoading } = useAuth();

  const code = normalizeCode(searchParams.get("code"));
  const hasCode = code.length === 8;
  const canLoad = !isLoading && isAuthenticated && hasCode;

  const [remote, setRemote] = useState<Remote>({ kind: "idle" });
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (!canLoad) return;

    let cancelled = false;
    extensionPairingApi
      .get(code)
      .then((request) => {
        if (!cancelled) setRemote({ kind: "review", status: request.status });
      })
      .catch((error: unknown) => {
        if (cancelled) return;
        if (error instanceof ApiError && error.status === 404) {
          setRemote({ kind: "notFound" });
          return;
        }
        setRemote({ kind: "error", message: error instanceof ApiError ? error.message : t("genericError") });
      });

    return () => {
      cancelled = true;
    };
  }, [canLoad, code, t]);

  // Derived, not stored: three of these four situations are facts about this render (the session
  // is still loading, there is no session, there is no code) and storing them in state would mean
  // an effect writing what the render already knows.
  const phase: Phase = isLoading
    ? { kind: "loading" }
    : !isAuthenticated
      ? { kind: "signedOut" }
      : !hasCode
        ? { kind: "noCode" }
        : remote.kind === "idle"
          ? { kind: "loading" }
          : remote;

  const decide = async (decision: "approve" | "deny") => {
    setIsSubmitting(true);
    try {
      const result = decision === "approve"
        ? await extensionPairingApi.approve(code)
        : await extensionPairingApi.deny(code);
      setRemote({ kind: "review", status: result.status });
    } catch (error) {
      setRemote({ kind: "error", message: error instanceof ApiError ? error.message : t("genericError") });
    } finally {
      setIsSubmitting(false);
    }
  };

  // Carries this exact page — code included — through the sign-in and back. postAuthRedirect
  // accepts this one shape and nothing else, so it cannot become an open redirect.
  const returnTo = `/pair${code ? `?code=${code}` : ""}`;
  const authHref = (path: "/login" | "/register") => `${path}?next=${encodeURIComponent(returnTo)}`;

  return (
    <div className="mx-auto flex w-full max-w-lg flex-col gap-6 px-4 py-12">
      <header className="flex flex-col gap-2">
        <h1 className="text-2xl font-semibold tracking-tight text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </header>

      {phase.kind === "loading" && <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>}

      {phase.kind === "signedOut" && (
        <section className="flex flex-col gap-4 rounded-lg border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
          <p className="text-sm text-gray-600 dark:text-gray-400">{t("signedOut.body")}</p>
          <div className="flex flex-col gap-2 sm:flex-row">
            <Link
              href={authHref("/register")}
              className="inline-flex items-center justify-center rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
            >
              {t("signedOut.register")}
            </Link>
            <Link
              href={authHref("/login")}
              className="inline-flex items-center justify-center rounded-md border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-200 dark:hover:bg-gray-800"
            >
              {t("signedOut.login")}
            </Link>
          </div>
        </section>
      )}

      {phase.kind === "noCode" && <Notice tone="warning">{t("noCode")}</Notice>}
      {phase.kind === "notFound" && <Notice tone="warning">{t("notFound")}</Notice>}
      {phase.kind === "error" && <Notice tone="error">{phase.message}</Notice>}

      {phase.kind === "review" && phase.status === "Pending" && (
        <section className="flex flex-col gap-5 rounded-lg border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
          <div className="flex flex-col items-center gap-2">
            <p className="text-sm text-gray-600 dark:text-gray-400">{t("confirm.prompt")}</p>
            <p className="font-mono text-3xl font-semibold tracking-[0.2em] text-gray-900 dark:text-gray-100">
              {formatCode(code)}
            </p>
          </div>

          <ul className="flex list-disc flex-col gap-2 pl-5 text-sm text-gray-600 dark:text-gray-400">
            <li>{t("confirm.grants")}</li>
            <li>{t("confirm.revocable")}</li>
          </ul>

          {/* The one warning that matters on a device-code flow: a code you did not produce
              yourself is somebody else asking for your account. */}
          <Notice tone="warning">{t("confirm.warning")}</Notice>

          <div className="flex flex-col gap-2 sm:flex-row">
            <Button onClick={() => decide("approve")} disabled={isSubmitting}>
              {t("confirm.approve")}
            </Button>
            <Button variant="secondary" onClick={() => decide("deny")} disabled={isSubmitting}>
              {t("confirm.deny")}
            </Button>
          </div>
        </section>
      )}

      {phase.kind === "review" && phase.status === "Approved" && (
        <Notice tone="success">
          <span className="font-medium">{t("approved.title")}</span>
          <span className="block">{t("approved.body")}</span>
        </Notice>
      )}

      {phase.kind === "review" && phase.status === "Completed" && <Notice tone="success">{t("completed")}</Notice>}
      {phase.kind === "review" && phase.status === "Denied" && <Notice tone="warning">{t("denied")}</Notice>}
      {phase.kind === "review" && phase.status === "Expired" && <Notice tone="warning">{t("expired")}</Notice>}
      {phase.kind === "review" && phase.status === "TokenLimitReached" && (
        <Notice tone="warning">{t("tokenLimitReached")}</Notice>
      )}

      <p className="text-sm text-gray-500 dark:text-gray-400">
        <Link href="/settings" className="text-blue-600 hover:underline dark:text-blue-400">
          {t("settingsLink")}
        </Link>
      </p>
    </div>
  );
}

function Notice({ tone, children }: { tone: "success" | "warning" | "error"; children: React.ReactNode }) {
  const tones = {
    success: "border-green-200 bg-green-50 text-green-800 dark:border-green-900 dark:bg-green-950 dark:text-green-200",
    warning: "border-amber-200 bg-amber-50 text-amber-900 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-200",
    error: "border-red-200 bg-red-50 text-red-800 dark:border-red-900 dark:bg-red-950 dark:text-red-200",
  } as const;

  return <div className={`rounded-md border p-4 text-sm ${tones[tone]}`}>{children}</div>;
}
