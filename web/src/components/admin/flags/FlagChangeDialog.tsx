"use client";

import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import type { FeatureFlagResponse, PrepareFeatureFlagChangeResponse } from "@/types/api";
import { featureFlagsApi } from "@/lib/api/featureFlags";
import { ApiError } from "@/lib/api/httpClient";
import { CLIENT_CONFIG_QUERY_KEY } from "@/hooks/useClientConfig";
import {
  type ChangeKind,
  formatCountdown,
  isOvertaken,
  localDeadline,
  mustStartAgain,
  phraseMatches,
  problemCode,
  secondsLeft,
  targetOf,
} from "@/lib/admin/featureFlags";
import { Modal } from "@/components/ui/Modal";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { Textarea } from "@/components/ui/Textarea";
import { FlagCouplings, FlagStatePill, WarningIcon } from "./FlagBits";

export const FEATURE_FLAGS_QUERY_KEY = ["admin", "featureFlags"] as const;
export const FEATURE_FLAG_HISTORY_QUERY_KEY = ["admin", "featureFlagHistory"] as const;

const MIN_REASON = 5;

type Step = "reason" | "confirm" | "done";

/**
 * A flag switch in the two confirmed steps the API insists on (canvas "Özellik bayrakları paneli",
 * StepOne/StepTwo, 2026-09-27): first what will happen and why — nothing changes yet — then the
 * flag's name typed out against a five-minute token. A spent, expired or overtaken token sends the
 * admin back to the first step with the list re-read, never silently retried.
 */
export function FlagChangeDialog({
  flag,
  kind,
  onClose,
}: {
  flag: FeatureFlagResponse;
  kind: ChangeKind;
  onClose: () => void;
}) {
  const t = useTranslations("adminFlags");
  const queryClient = useQueryClient();
  const [step, setStep] = useState<Step>("reason");
  const [reason, setReason] = useState("");
  const [typed, setTyped] = useState("");
  const [prepared, setPrepared] = useState<PrepareFeatureFlagChangeResponse | null>(null);
  // When the token stops working, on this browser's clock (see localDeadline).
  const [deadline, setDeadline] = useState(0);
  const [result, setResult] = useState<FeatureFlagResponse | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [now, setNow] = useState(() => Date.now());

  // The countdown: a tick a second while the second step is open, and none otherwise.
  useEffect(() => {
    if (step !== "confirm") {
      return;
    }
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [step]);

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: FEATURE_FLAGS_QUERY_KEY }),
      queryClient.invalidateQueries({ queryKey: FEATURE_FLAG_HISTORY_QUERY_KEY }),
      // This admin's own menus follow at once rather than after the config's minute.
      queryClient.invalidateQueries({ queryKey: CLIENT_CONFIG_QUERY_KEY }),
    ]);

  // An ApiError carries the server's own (localised) message; anything else is the network.
  const describe = (error: Error) => (error instanceof ApiError ? error.message : t("dialog.networkError"));

  const prepare = useMutation({
    mutationFn: () => featureFlagsApi.prepare(flag.flag, targetOf(kind), reason.trim()),
    onSuccess: (response) => {
      const receivedAt = Date.now();
      setPrepared(response);
      setDeadline(localDeadline(receivedAt, response.expiresInSeconds));
      setTyped("");
      setMessage(null);
      setNow(receivedAt);
      setStep("confirm");
    },
    onError: async (error: Error) => {
      setMessage(describe(error));
      // "Nothing to change": another admin got there first. Re-read, so the dialog can say so.
      if (problemCode(error) === "FEATURE_FLAG_UNCHANGED") {
        await refresh();
      }
    },
  });

  const confirm = useMutation({
    mutationFn: () => featureFlagsApi.confirm(flag.flag, prepared!.confirmationToken, typed.trim()),
    onSuccess: async (response) => {
      setResult(response);
      setStep("done");
      await refresh();
    },
    onError: async (error: Error) => {
      setMessage(describe(error));
      if (mustStartAgain(error)) {
        setPrepared(null);
        setStep("reason");
        await refresh();
      }
    },
  });

  const busy = prepare.isPending || confirm.isPending;
  const remaining = prepared ? secondsLeft(deadline, now) : 0;
  // After a 409 the list is re-read: if another admin already made this very change, there is
  // nothing left to confirm — say so rather than offer a first step that can only answer that.
  const overtaken = step === "reason" && isOvertaken(kind, flag);
  const expired = step === "confirm" && remaining === 0;
  const reasonOk = reason.trim().length >= MIN_REASON;
  const phraseOk = prepared !== null && phraseMatches(typed, prepared.confirmationPhrase);
  const stateWord = (on: boolean) => t(on ? "stateWord.on" : "stateWord.off");
  const willBeOn = kind === "reset" ? flag.default : kind === "turnOn";
  const confirmLabel =
    kind === "turnOff" ? t("dialog.stepTwo.confirmTurnOff") : kind === "turnOn" ? t("dialog.stepTwo.confirmTurnOn") : t("dialog.stepTwo.confirmReset");

  const startAgain = () => {
    setPrepared(null);
    setMessage(null);
    setStep("reason");
  };

  if (step === "done" && result) {
    return (
      <Modal
        title={t("dialog.done.title", { title: result.title, state: stateWord(result.enabled) })}
        onClose={onClose}
        footer={
          <Button key="done-close" onClick={onClose} autoFocus>
            {t("dialog.done.close")}
          </Button>
        }
      >
        <div className="flex flex-col gap-3 pb-1">
          <p role="status" className="flex items-center gap-2 text-base font-semibold text-green-800 dark:text-green-400">
            <svg viewBox="0 0 24 24" className="h-5 w-5 shrink-0" fill="none" stroke="currentColor" strokeWidth={2.2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <path d="M20 6 9 17l-5-5" />
            </svg>
            {t("dialog.done.title", { title: result.title, state: stateWord(result.enabled) })}
          </p>
          <p className="text-sm leading-relaxed text-gray-700 dark:text-gray-300">{t("dialog.done.body")}</p>
        </div>
      </Modal>
    );
  }

  if (step === "confirm" && prepared) {
    return (
      <Modal
        title={t(`dialog.stepTwo.${kind}`, { title: flag.title })}
        onClose={onClose}
        busy={busy}
        footer={
          <>
            <Button key="two-back" variant="secondary" onClick={startAgain} disabled={busy} className="mr-auto">
              {t("dialog.stepTwo.back")}
            </Button>
            <Button key="two-cancel" variant="secondary" onClick={onClose} disabled={busy}>
              {t("dialog.stepTwo.cancel")}
            </Button>
            {expired ? (
              <Button key="two-again" variant="danger" onClick={startAgain} disabled={busy}>
                {t("dialog.stepTwo.startAgain")}
              </Button>
            ) : (
              <Button key="two-confirm" variant="danger" onClick={() => confirm.mutate()} disabled={!phraseOk || busy}>
                {confirm.isPending ? t("dialog.stepTwo.confirming") : confirmLabel}
              </Button>
            )}
          </>
        }
      >
        <div className="-mx-6 -mt-6 mb-5 flex items-start gap-3 border-b-2 border-red-200 bg-red-50 px-6 py-4 dark:border-red-900 dark:bg-red-950/40">
          <WarningIcon className="mt-0.5 h-5 w-5 shrink-0 text-red-700 dark:text-red-400" />
          <div className="flex flex-col gap-1">
            <p className="text-xs font-semibold tracking-wide text-red-700 dark:text-red-400">{t("dialog.stepTwo.eyebrow")}</p>
            <h2 className="text-lg font-semibold text-red-900 dark:text-red-200">{t(`dialog.stepTwo.${kind}`, { title: flag.title })}</h2>
          </div>
        </div>
        <div className="flex flex-col gap-4 pb-1">
          <dl className="grid grid-cols-[7rem_1fr] gap-x-3 gap-y-2 text-sm">
            <dt className="text-gray-500 dark:text-gray-400">{t("dialog.stepTwo.flag")}</dt>
            <dd className="font-medium text-gray-900 dark:text-gray-100">{flag.title}</dd>
            <dt className="text-gray-500 dark:text-gray-400">{t("dialog.stepTwo.change")}</dt>
            <dd className="text-gray-900 dark:text-gray-100">
              {t(flag.enabled ? "state.on" : "state.off")} → <b>{t(prepared.willBeOn ? "state.on" : "state.off")}</b>
            </dd>
            <dt className="text-gray-500 dark:text-gray-400">{t("dialog.stepTwo.reason")}</dt>
            <dd className="break-words text-gray-700 dark:text-gray-300">{reason.trim()}</dd>
          </dl>
          {expired ? (
            <p role="alert" className="text-sm font-medium text-red-700 dark:text-red-400">
              {t("dialog.stepTwo.expired")}
            </p>
          ) : (
            <div className="flex flex-col gap-1.5">
              <label htmlFor="flag-confirm-phrase" className="text-sm text-gray-800 dark:text-gray-200">
                {t("dialog.stepTwo.phraseLabel")}{" "}
                <code className="rounded bg-gray-100 px-1.5 py-0.5 font-mono text-[13px] font-medium dark:bg-gray-800">{prepared.confirmationPhrase}</code>
              </label>
              <Input
                id="flag-confirm-phrase"
                autoFocus
                value={typed}
                onChange={(event) => setTyped(event.target.value)}
                autoComplete="off"
                autoCapitalize="off"
                autoCorrect="off"
                spellCheck={false}
                className="font-mono"
                disabled={busy}
              />
              <p className="text-xs text-gray-500 dark:text-gray-400">
                {t("dialog.stepTwo.phraseHint", { time: formatCountdown(remaining) })}
              </p>
            </div>
          )}
          {message && (
            <p role="alert" className="text-sm text-red-700 dark:text-red-400">
              {message}
            </p>
          )}
        </div>
      </Modal>
    );
  }

  return (
    <Modal
      title={t(`dialog.stepOne.${kind}`, { title: flag.title })}
      onClose={onClose}
      busy={busy}
      wide
      footer={
        <>
          <Button key="one-cancel" variant="secondary" onClick={onClose} disabled={busy}>
            {t("dialog.stepOne.cancel")}
          </Button>
          <Button key="one-continue" variant="danger" onClick={() => prepare.mutate()} disabled={!reasonOk || busy || overtaken}>
            {prepare.isPending ? t("dialog.stepOne.preparing") : t("dialog.stepOne.continue")}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-5 pb-1">
        <div className="flex flex-col gap-1.5">
          <p className="text-xs font-semibold tracking-wide text-red-700 dark:text-red-400">{t("dialog.stepOne.eyebrow")}</p>
          <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t(`dialog.stepOne.${kind}`, { title: flag.title })}</h2>
          <code className="w-fit rounded bg-gray-100 px-1.5 py-0.5 font-mono text-xs text-gray-600 dark:bg-gray-800 dark:text-gray-400">{flag.flag}</code>
        </div>

        <div className="flex flex-wrap items-center gap-2 text-sm">
          <FlagStatePill on={flag.enabled} />
          <svg viewBox="0 0 24 24" className="h-5 w-5 text-gray-500" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M5 12h14" />
            <path d="m13 6 6 6-6 6" />
          </svg>
          <span className="sr-only">{t("dialog.stepOne.becomes")}</span>
          <FlagStatePill on={willBeOn} />
          <span className="text-gray-600 dark:text-gray-400">{t("dialog.stepOne.scope")}</span>
        </div>

        <div className="flex flex-col gap-1.5">
          <p className="text-sm font-semibold text-gray-700 dark:text-gray-300">{t("sections.whenOff")}</p>
          <p className="text-sm leading-relaxed text-gray-700 dark:text-gray-300">{flag.whenOff}</p>
        </div>

        {flag.notes && (
          <div className="flex flex-col gap-2 rounded-md border border-orange-200 bg-orange-50 px-3.5 py-3 dark:border-orange-900 dark:bg-orange-950/30">
            <div className="flex flex-wrap items-center gap-2">
              <p className="text-sm font-semibold text-orange-900 dark:text-orange-300">{t("sections.notes")}</p>
              <FlagCouplings couplings={flag.couplings} />
            </div>
            <p className="text-sm leading-relaxed text-orange-950 dark:text-orange-200">{flag.notes}</p>
          </div>
        )}

        <div className="flex flex-col gap-1.5">
          <label htmlFor="flag-change-reason" className="text-sm font-semibold text-gray-900 dark:text-gray-100">
            {t("dialog.stepOne.reasonLabel")}{" "}
            <span className="font-normal text-gray-500 dark:text-gray-400">{t("dialog.stepOne.reasonHint")}</span>
          </label>
          <Textarea
            id="flag-change-reason"
            // data-autofocus for the dialog's first opening (Modal focuses it after showModal);
            // autoFocus for a return to this step, when the dialog is already open.
            data-autofocus
            autoFocus
            rows={3}
            maxLength={500}
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            disabled={busy}
          />
        </div>

        {overtaken && (
          <p role="status" className="text-sm font-medium text-gray-900 dark:text-gray-100">
            {t("dialog.stepOne.overtaken")}
          </p>
        )}

        {message && (
          <p role="alert" className="text-sm text-red-700 dark:text-red-400">
            {message}
          </p>
        )}
      </div>
    </Modal>
  );
}
