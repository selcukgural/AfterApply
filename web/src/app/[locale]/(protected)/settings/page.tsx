"use client";

import { useEffect, useState, type FormEvent } from "react";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { authApi } from "@/lib/api/auth";
import { emailIntegrationsApi } from "@/lib/api/emailIntegrations";
import { matchingApi } from "@/lib/api/matching";
import { personalAccessTokensApi } from "@/lib/api/personalAccessTokens";
import { ApiError } from "@/lib/api/httpClient";
import type { CreatedPersonalAccessTokenResponse, EmailConnectionStatusResponse, PersonalAccessTokenResponse } from "@/types/api";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Textarea } from "@/components/ui/Textarea";
import { Button } from "@/components/ui/Button";

export default function SettingsPage() {
  const t = useTranslations("settings");
  const tCommon = useTranslations("common");
  const { deleteAccount } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();

  const [isExporting, setIsExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  const [emailStatus, setEmailStatus] = useState<EmailConnectionStatusResponse | null>(null);
  const [emailStatusLoading, setEmailStatusLoading] = useState(true);
  // Read once from the URL synchronously during the initial render (not via an effect —
  // setState-in-effect causes an avoidable extra render pass).
  const [emailActionError, setEmailActionError] = useState<string | null>(() =>
    searchParams.get("emailIntegration") === "error" ? t("email.errorNotice") : null,
  );
  const [emailNotice] = useState<string | null>(() =>
    searchParams.get("emailIntegration") === "success" ? t("email.successNotice") : null,
  );

  const [cvText, setCvText] = useState("");
  const [cvLoading, setCvLoading] = useState(true);
  const [cvSaving, setCvSaving] = useState(false);
  const [cvNotice, setCvNotice] = useState<string | null>(null);
  const [cvError, setCvError] = useState<string | null>(null);

  const [tokens, setTokens] = useState<PersonalAccessTokenResponse[]>([]);
  const [tokensLoading, setTokensLoading] = useState(true);
  const [newTokenName, setNewTokenName] = useState("");
  const [creatingToken, setCreatingToken] = useState(false);
  const [tokenError, setTokenError] = useState<string | null>(null);
  const [justCreatedToken, setJustCreatedToken] = useState<CreatedPersonalAccessTokenResponse | null>(null);
  const [tokenCopied, setTokenCopied] = useState(false);

  const [password, setPassword] = useState("");
  const [confirmationText, setConfirmationText] = useState("");
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  useEffect(() => {
    if (searchParams.get("emailIntegration")) {
      router.replace("/settings");
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    emailIntegrationsApi
      .getStatus()
      .then(setEmailStatus)
      .catch(() => setEmailStatus(null))
      .finally(() => setEmailStatusLoading(false));
  }, []);

  useEffect(() => {
    matchingApi
      .getProfile()
      .then((profile) => setCvText(profile.cvText))
      .catch(() => setCvText(""))
      .finally(() => setCvLoading(false));
  }, []);

  const handleSaveCv = async () => {
    setCvError(null);
    setCvNotice(null);
    setCvSaving(true);
    try {
      await matchingApi.updateProfile(cvText);
      setCvNotice(t("cv.saved"));
    } catch (error) {
      setCvError(error instanceof ApiError ? error.message : t("cv.saveError"));
    } finally {
      setCvSaving(false);
    }
  };

  const loadTokens = () => {
    personalAccessTokensApi
      .list()
      .then(setTokens)
      .catch(() => setTokens([]))
      .finally(() => setTokensLoading(false));
  };

  useEffect(loadTokens, []);

  const handleCreateToken = async () => {
    setTokenError(null);
    setCreatingToken(true);
    try {
      const created = await personalAccessTokensApi.create(newTokenName.trim() || "Browser Extension");
      setJustCreatedToken(created);
      setTokenCopied(false);
      setNewTokenName("");
      loadTokens();
    } catch (error) {
      setTokenError(error instanceof ApiError ? error.message : t("extension.generateError"));
    } finally {
      setCreatingToken(false);
    }
  };

  const handleCopyToken = async () => {
    if (!justCreatedToken) return;
    await navigator.clipboard.writeText(justCreatedToken.token);
    setTokenCopied(true);
  };

  const handleRevokeToken = async (id: string) => {
    if (!confirm(t("extension.revokeConfirm"))) return;
    setTokenError(null);
    try {
      await personalAccessTokensApi.revoke(id);
      setTokens((prev) => prev.filter((token) => token.id !== id));
    } catch (error) {
      setTokenError(error instanceof ApiError ? error.message : t("extension.revokeError"));
    }
  };

  const handleConnectGmail = async () => {
    setEmailActionError(null);
    try {
      const { authorizationUrl } = await emailIntegrationsApi.getAuthorizationUrl();
      window.location.href = authorizationUrl;
    } catch (error) {
      setEmailActionError(error instanceof ApiError ? error.message : t("email.connectError"));
    }
  };

  const handleDisconnectGmail = async () => {
    setEmailActionError(null);
    try {
      await emailIntegrationsApi.disconnect();
      setEmailStatus((prev) => (prev ? { ...prev, connected: false, providerAccountEmail: null } : prev));
    } catch (error) {
      setEmailActionError(error instanceof ApiError ? error.message : t("email.disconnectError"));
    }
  };

  const handleExport = async () => {
    setExportError(null);
    setIsExporting(true);
    try {
      await authApi.exportData();
    } catch (error) {
      setExportError(error instanceof ApiError ? error.message : t("export.error"));
    } finally {
      setIsExporting(false);
    }
  };

  const handleDelete = async (event: FormEvent) => {
    event.preventDefault();
    setDeleteError(null);

    if (confirmationText !== t("delete.confirmWord")) {
      setDeleteError(t("delete.confirmMismatch"));
      return;
    }

    setIsDeleting(true);
    try {
      await deleteAccount(password);
      router.replace("/login");
    } catch (error) {
      setDeleteError(error instanceof ApiError ? error.message : t("delete.genericError"));
      setIsDeleting(false);
    }
  };

  return (
    <div className="flex max-w-lg flex-col gap-8">
      <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>

      <section className="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-6 shadow-sm">
        <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("export.title")}</h2>
        <p className="mb-4 text-sm text-gray-600 dark:text-gray-400">{t("export.description")}</p>
        {exportError && <p className="mb-3 text-sm text-red-600 dark:text-red-400">{exportError}</p>}
        <Button variant="secondary" onClick={handleExport} disabled={isExporting}>
          {isExporting ? t("export.preparing") : t("export.download")}
        </Button>
      </section>

      <section className="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-6 shadow-sm">
        <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("email.title")}</h2>
        <p className="mb-4 text-sm text-gray-600 dark:text-gray-400">{t("email.description")}</p>
        {emailNotice && <p className="mb-3 text-sm text-green-700 dark:text-green-400">{emailNotice}</p>}
        {emailActionError && <p className="mb-3 text-sm text-red-600 dark:text-red-400">{emailActionError}</p>}
        {emailStatusLoading ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>
        ) : emailStatus?.connected ? (
          <div className="flex flex-col gap-3">
            <p className="text-sm text-gray-700 dark:text-gray-300">
              {t("email.connectedAccount")} <span className="font-medium">{emailStatus.providerAccountEmail}</span>
            </p>
            {emailStatus.needsReattention && (
              <p className="text-sm text-amber-600 dark:text-amber-400">{t("email.needsReattention")}</p>
            )}
            <div className="flex items-center gap-4">
              <Link href="/settings/email-suggestions" className="text-sm text-blue-600 dark:text-blue-400 hover:underline">
                {t("email.viewSuggestions")}
              </Link>
              <Button variant="secondary" onClick={handleDisconnectGmail}>
                {t("email.disconnect")}
              </Button>
            </div>
          </div>
        ) : (
          <Button variant="secondary" onClick={handleConnectGmail}>
            {t("email.connect")}
          </Button>
        )}
      </section>

      <section className="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-6 shadow-sm">
        <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("cv.title")}</h2>
        <p className="mb-4 text-sm text-gray-600 dark:text-gray-400">{t("cv.description")}</p>
        {cvLoading ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>
        ) : (
          <div className="flex flex-col gap-3">
            {cvNotice && <p className="text-sm text-green-700 dark:text-green-400">{cvNotice}</p>}
            {cvError && <p className="text-sm text-red-600 dark:text-red-400">{cvError}</p>}
            <Textarea
              rows={10}
              value={cvText}
              placeholder={t("cv.placeholder")}
              onChange={(e) => setCvText(e.target.value)}
            />
            <div>
              <Button variant="secondary" onClick={handleSaveCv} disabled={cvSaving}>
                {cvSaving ? t("cv.saving") : t("cv.save")}
              </Button>
            </div>
          </div>
        )}
      </section>

      <section className="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-6 shadow-sm">
        <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("extension.title")}</h2>
        <p className="mb-4 text-sm text-gray-600 dark:text-gray-400">{t("extension.description")}</p>

        {tokenError && <p className="mb-3 text-sm text-red-600 dark:text-red-400">{tokenError}</p>}

        {justCreatedToken && (
          <div className="mb-4 flex flex-col gap-2 rounded-md border border-amber-300 dark:border-amber-800 bg-amber-50 dark:bg-amber-950/40 p-3">
            <p className="text-sm text-amber-800 dark:text-amber-300">{t("extension.newTokenWarning")}</p>
            <div className="flex items-center gap-2">
              <code className="flex-1 overflow-x-auto rounded bg-white dark:bg-gray-900 px-2 py-1 text-xs text-gray-900 dark:text-gray-100">
                {justCreatedToken.token}
              </code>
              <Button variant="secondary" onClick={handleCopyToken}>
                {tokenCopied ? t("extension.copied") : t("extension.copy")}
              </Button>
            </div>
          </div>
        )}

        <div className="mb-4 flex items-end gap-2">
          <div className="flex-1">
            <FormField label={t("extension.nameLabel")} htmlFor="pat-name">
              <Input
                id="pat-name"
                placeholder={t("extension.namePlaceholder")}
                value={newTokenName}
                onChange={(e) => setNewTokenName(e.target.value)}
              />
            </FormField>
          </div>
          <Button variant="secondary" onClick={handleCreateToken} disabled={creatingToken}>
            {creatingToken ? t("extension.generating") : t("extension.generate")}
          </Button>
        </div>

        {tokensLoading ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>
        ) : tokens.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("extension.noTokens")}</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {tokens.map((token) => (
              <li
                key={token.id}
                className="flex items-center justify-between rounded-md border border-gray-200 dark:border-gray-800 px-3 py-2 text-sm"
              >
                <div>
                  <p className="font-medium text-gray-900 dark:text-gray-100">{token.name}</p>
                  <p className="text-xs text-gray-500 dark:text-gray-400">
                    {t("extension.createdAt")} {new Date(token.createdAt).toLocaleDateString()} ·{" "}
                    {token.lastUsedAt
                      ? `${t("extension.lastUsedAt")} ${new Date(token.lastUsedAt).toLocaleDateString()}`
                      : t("extension.neverUsed")}
                  </p>
                </div>
                <Button variant="danger" onClick={() => handleRevokeToken(token.id)}>
                  {t("extension.revoke")}
                </Button>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="rounded-lg border border-red-200 dark:border-red-900 bg-white dark:bg-gray-900 p-6 shadow-sm">
        <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("delete.title")}</h2>
        <p className="mb-4 text-sm text-gray-600 dark:text-gray-400">{t("delete.description")}</p>
        <form onSubmit={handleDelete} className="flex flex-col gap-4">
          <FormField label={t("delete.passwordLabel")} htmlFor="delete-password">
            <Input
              id="delete-password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </FormField>
          <FormField label={t("delete.confirmLabel")} htmlFor="delete-confirmation">
            <Input
              id="delete-confirmation"
              value={confirmationText}
              onChange={(e) => setConfirmationText(e.target.value)}
            />
          </FormField>
          {deleteError && <p className="text-sm text-red-600 dark:text-red-400">{deleteError}</p>}
          <Button type="submit" variant="danger" disabled={isDeleting}>
            {isDeleting ? t("delete.deleting") : t("delete.submit")}
          </Button>
        </form>
      </section>
    </div>
  );
}
