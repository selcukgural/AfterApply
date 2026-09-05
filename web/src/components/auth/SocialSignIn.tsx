"use client";

import { useTranslations } from "next-intl";
import { useClientConfig } from "@/hooks/useClientConfig";
import { GoogleSignInButton } from "./GoogleSignInButton";
import { LinkedInSignInButton } from "./LinkedInSignInButton";

// The "or" divider plus every configured social sign-in button, under the password form on both
// the login and the sign-up page. Renders nothing at all when no provider is configured on this
// deployment, so the divider never dangles above an empty space. Each button still decides for
// itself whether to render (same /api/config flags), this only owns the divider.
//
// Both pages render this one component rather than listing buttons themselves: when LinkedIn was
// added, login moved here and sign-up kept its lone <GoogleSignInButton />, so you could sign in
// with LinkedIn but not sign up with it — and the sign-up page had no divider at all.
export function SocialSignIn() {
  const { config } = useClientConfig();
  const t = useTranslations("auth.social");

  const anyEnabled = Boolean(config.googleAuth?.enabled || config.linkedInAuth?.enabled);
  if (!anyEnabled) {
    return null;
  }

  return (
    <div className="mt-4 flex flex-col gap-3">
      <div className="flex items-center gap-3 text-xs uppercase tracking-wide text-gray-400 dark:text-gray-500">
        <span className="h-px flex-1 bg-gray-200 dark:bg-gray-800" />
        {t("or")}
        <span className="h-px flex-1 bg-gray-200 dark:bg-gray-800" />
      </div>
      <GoogleSignInButton />
      <LinkedInSignInButton />
    </div>
  );
}
