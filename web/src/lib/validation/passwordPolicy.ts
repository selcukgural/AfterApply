import { z } from "zod";
import type { PasswordPolicy } from "@/types/api";

export type PasswordRuleKey = "minLength" | "uniqueChars" | "digit" | "lowercase" | "uppercase" | "nonAlphanumeric";

export interface PasswordRuleResult {
  key: PasswordRuleKey;
  met: boolean;
  /** Interpolation values for the rule's message ({min}, {count}). */
  values: Record<string, number>;
}

// Character classes are deliberately ASCII-only, mirroring ASP.NET Identity's PasswordValidator
// (IsDigit/IsLower/IsUpper check '0'-'9', 'a'-'z', 'A'-'Z'; anything else is "non-alphanumeric").
// A Turkish "ş" therefore satisfies the special-character rule and not the lowercase one — odd,
// but it's what the server does, and matching it is the whole point of this file.
const DIGIT = /[0-9]/;
const LOWER = /[a-z]/;
const UPPER = /[A-Z]/;
const NON_ALPHANUMERIC = /[^0-9a-zA-Z]/;

/** Every rule the policy turns on, in display order, with whether `password` currently meets it. */
export function evaluatePasswordRules(password: string, policy: PasswordPolicy): PasswordRuleResult[] {
  const rules: PasswordRuleResult[] = [
    {
      key: "minLength",
      met: password.length >= policy.requiredLength,
      values: { min: policy.requiredLength },
    },
  ];

  if (policy.requireUppercase) {
    rules.push({ key: "uppercase", met: UPPER.test(password), values: {} });
  }
  if (policy.requireLowercase) {
    rules.push({ key: "lowercase", met: LOWER.test(password), values: {} });
  }
  if (policy.requireDigit) {
    rules.push({ key: "digit", met: DIGIT.test(password), values: {} });
  }
  if (policy.requireNonAlphanumeric) {
    rules.push({ key: "nonAlphanumeric", met: NON_ALPHANUMERIC.test(password), values: {} });
  }
  // Identity counts distinct UTF-16 code units (string.Distinct()), so split("") rather than
  // iterating code points. Only shown when it's stricter than "one of each class" already implies.
  if (policy.requiredUniqueChars > 1) {
    rules.push({
      key: "uniqueChars",
      met: new Set(password.split("")).size >= policy.requiredUniqueChars,
      values: { count: policy.requiredUniqueChars },
    });
  }

  return rules;
}

const RULE_MESSAGE_KEYS: Record<PasswordRuleKey, string> = {
  minLength: "passwordMinLength",
  uniqueChars: "passwordUniqueChars",
  digit: "passwordDigit",
  lowercase: "passwordLowercase",
  uppercase: "passwordUppercase",
  nonAlphanumeric: "passwordNonAlphanumeric",
};

export type ValidationTranslator = (key: string, values?: Record<string, number>) => string;

/**
 * A zod string that enforces the server's password policy client-side, so the form reports the
 * same unmet rule the server would — before the round-trip, and in the user's language. `t` is the
 * "validation" namespace translator.
 */
export function createPasswordSchema(policy: PasswordPolicy, t: ValidationTranslator) {
  return z
    .string()
    .min(1, t("passwordRequired"))
    .superRefine((value, ctx) => {
      for (const rule of evaluatePasswordRules(value, policy)) {
        if (!rule.met) {
          ctx.addIssue({ code: "custom", message: t(RULE_MESSAGE_KEYS[rule.key], rule.values) });
        }
      }
    });
}
