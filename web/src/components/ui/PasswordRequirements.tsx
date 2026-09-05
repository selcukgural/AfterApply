"use client";

import { useTranslations } from "next-intl";
import type { PasswordPolicy } from "@/types/api";
import { evaluatePasswordRules } from "@/lib/validation/passwordPolicy";

interface PasswordRequirementsProps {
  /** Set as the target of the password input's aria-describedby. */
  id: string;
  password: string;
  policy: PasswordPolicy;
}

/**
 * Lists every rule the server will enforce on a new password and ticks each one off as the user
 * types — so the rules are visible before the first submit, not discovered one rejection at a time.
 * Rules come from GET /api/config (see useClientConfig), never from a copy of them.
 */
export function PasswordRequirements({ id, password, policy }: PasswordRequirementsProps) {
  const t = useTranslations("auth.passwordRules");
  const rules = evaluatePasswordRules(password, policy);

  return (
    <div id={id} className="mt-1 rounded-md bg-gray-50 px-3 py-2 text-xs dark:bg-gray-800/60">
      <p className="mb-1 font-medium text-gray-700 dark:text-gray-300">{t("title")}</p>
      <ul className="flex flex-col gap-0.5">
        {rules.map((rule) => (
          <li
            key={rule.key}
            className={`flex items-center gap-1.5 ${
              rule.met ? "text-green-700 dark:text-green-400" : "text-gray-600 dark:text-gray-400"
            }`}
          >
            <span aria-hidden="true" className="inline-block w-3 text-center">
              {rule.met ? "✓" : "•"}
            </span>
            <span>{t(rule.key, rule.values)}</span>
            <span className="sr-only">{rule.met ? t("met") : t("unmet")}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}
