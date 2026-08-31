"use client";

import { useEffect, useState, type FormEvent } from "react";
import { useTranslations } from "next-intl";
import { useRouter } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { authApi } from "@/lib/api/auth";
import { personalAccessTokensApi } from "@/lib/api/personalAccessTokens";
import { emailForwardingApi } from "@/lib/api/emailForwarding";
import { ApiError } from "@/lib/api/httpClient";
import type { CreatedPersonalAccessTokenResponse, PersonalAccessTokenResponse } from "@/types/api";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";

export default function SettingsPage() {
  const t = useTranslations("settings");
  const tCommon = useTranslations("common");
  const { deleteAccount } = useAuth();
  const router = useRouter();

  const [isExporting, setIsExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  const [tokens, setTokens] = useState<PersonalAccessTokenResponse[]>([]);
  const [tokensLoading, setTokensLoading] = useState(true);
  const [newTokenName, setNewTokenName] = useState("");
  const [creatingToken, setCreatingToken] = useState(false);
  const [tokenError, setTokenError] = useState<string | null>(null);
  const [justCreatedToken, setJustCreatedToken] = useState<CreatedPersonalAccessTokenResponse | null>(null);
  const [tokenCopied, setTokenCopied] = useState(false);

  const [forwardingAddress, setForwardingAddress] = useState<string | null>(null);
  const [forwardingLoading, setForwardingLoading] = useState(true);
  const [forwardingError, setForwardingError] = useState<string | null>(null);
  const [addressCopied, setAddressCopied] = useState(false);

  const [gmailConfirmationCode, setGmailConfirmationCode] = useState<string | null>(null);
  const [gmailConfirmationLink, setGmailConfirmationLink] = useState<string | null>(null);
  const [codeCopied, setCodeCopied] = useState(false);
  const [dismissingConfirmation, setDismissingConfirmation] = useState(false);

  const [password, setPassword] = useState("");
  const [confirmationText, setConfirmationText] = useState("");
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const loadTokens = () => {
    personalAccessTokensApi
      .list()
      .then(setTokens)
      .catch(() => setTokens([]))
      .finally(() => setTokensLoading(false));
  };

  useEffect(loadTokens, []);

  useEffect(() => {
    emailForwardingApi
      .getAddress()
      .then(({ address, gmailConfirmationCode, gmailConfirmationLink }) => {
        setForwardingAddress(address);
        setGmailConfirmationCode(gmailConfirmationCode);
        setGmailConfirmationLink(gmailConfirmationLink);
      })
      .catch((error) => {
        // 404 means EmailForwarding:Enabled is off in this environment — hide the card rather
        // than show an error.
        if (!(error instanceof ApiError && error.status === 404)) {
          setForwardingError(t("emailForwarding.loadError"));
        }
      })
      .finally(() => setForwardingLoading(false));
  }, [t]);

  const handleCopyAddress = async () => {
    if (!forwardingAddress) return;
    await navigator.clipboard.writeText(forwardingAddress);
    setAddressCopied(true);
  };

  const handleCopyCode = async () => {
    if (!gmailConfirmationCode) return;
    await navigator.clipboard.writeText(gmailConfirmationCode);
    setCodeCopied(true);
  };

  const handleDismissGmailConfirmation = async () => {
    setDismissingConfirmation(true);
    try {
      await emailForwardingApi.dismissGmailConfirmation();
      setGmailConfirmationCode(null);
      setGmailConfirmationLink(null);
    } finally {
      setDismissingConfirmation(false);
    }
  };

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

      {(forwardingLoading || forwardingAddress || forwardingError) && (
        <section className="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-6 shadow-sm">
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("emailForwarding.title")}</h2>
          <p className="mb-4 text-sm text-gray-600 dark:text-gray-400">{t("emailForwarding.description")}</p>

          {forwardingLoading ? (
            <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>
          ) : forwardingError ? (
            <p className="text-sm text-red-600 dark:text-red-400">{forwardingError}</p>
          ) : (
            forwardingAddress && (
              <>
                <p className="mb-1 text-sm text-gray-600 dark:text-gray-400">{t("emailForwarding.addressLabel")}</p>
                <div className="mb-4 flex items-center gap-2">
                  <code className="flex-1 overflow-x-auto rounded bg-gray-50 dark:bg-gray-800 px-2 py-1 text-xs text-gray-900 dark:text-gray-100">
                    {forwardingAddress}
                  </code>
                  <Button variant="secondary" onClick={handleCopyAddress}>
                    {addressCopied ? t("emailForwarding.copied") : t("emailForwarding.copy")}
                  </Button>
                </div>

                {gmailConfirmationCode && (
                  <div className="mb-4 rounded-lg border border-blue-200 dark:border-blue-900 bg-blue-50 dark:bg-blue-950 p-4">
                    <p className="mb-1 text-sm font-medium text-blue-900 dark:text-blue-100">
                      {t("emailForwarding.gmailConfirmation.title")}
                    </p>
                    <p className="mb-3 text-sm text-blue-800 dark:text-blue-200">
                      {t("emailForwarding.gmailConfirmation.help")}
                    </p>
                    <div className="mb-3 flex items-center gap-2">
                      <code className="flex-1 overflow-x-auto rounded bg-white dark:bg-gray-900 px-2 py-1 text-sm font-semibold text-gray-900 dark:text-gray-100">
                        {gmailConfirmationCode}
                      </code>
                      <Button variant="secondary" onClick={handleCopyCode}>
                        {codeCopied ? t("emailForwarding.copied") : t("emailForwarding.copy")}
                      </Button>
                    </div>
                    <div className="flex items-center gap-4">
                      {gmailConfirmationLink && (
                        <a
                          href={gmailConfirmationLink}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="text-sm text-blue-600 dark:text-blue-400 hover:underline"
                        >
                          {t("emailForwarding.gmailConfirmation.openLink")}
                        </a>
                      )}
                      <button
                        type="button"
                        onClick={handleDismissGmailConfirmation}
                        disabled={dismissingConfirmation}
                        className="text-sm text-blue-600 dark:text-blue-400 hover:underline disabled:opacity-50"
                      >
                        {t("emailForwarding.gmailConfirmation.dismiss")}
                      </button>
                    </div>
                  </div>
                )}
              </>
            )
          )}
        </section>
      )}

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
