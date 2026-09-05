"use client";

import { useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { useClientConfig } from "@/hooks/useClientConfig";
import { beginLinkedInSignIn } from "@/lib/auth/linkedinOAuth";

// Renders nothing until GET /api/config says Sign in with LinkedIn is configured on this
// deployment — the endpoints behind it answer 404 otherwise, so a button would be a dead end.
// The "or" divider above the social buttons lives in SocialSignIn, not here.
export function LinkedInSignInButton() {
  const { config } = useClientConfig();
  const locale = useLocale();
  const t = useTranslations("auth.linkedin");
  const [isRedirecting, setIsRedirecting] = useState(false);

  // Optional chaining on purpose: /api/config is cached for 5 minutes (Cache-Control), so right
  // after a deploy a browser can still hold a response from before this field existed.
  const clientId = config.linkedInAuth?.enabled ? config.linkedInAuth.clientId : null;
  if (!clientId) {
    return null;
  }

  const handleClick = () => {
    setIsRedirecting(true);
    try {
      beginLinkedInSignIn(clientId, locale);
    } catch {
      // Only reachable if the browser refused sessionStorage (e.g. storage disabled); the button
      // simply becomes clickable again.
      setIsRedirecting(false);
    }
  };

  return (
    <button
      type="button"
      onClick={handleClick}
      disabled={isRedirecting}
      // LinkedIn's brand blue (#0A66C2) with white text, per their "Sign In with LinkedIn" button
      // guidance; the same height/radius as the Google button so the two line up.
      className="flex items-center justify-center gap-3 rounded-md border border-[#0A66C2] bg-[#0A66C2] px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-[#004182] disabled:cursor-not-allowed disabled:opacity-60"
    >
      <LinkedInLogo />
      {t("continueWith")}
    </button>
  );
}

// LinkedIn's "in" mark. Inline so it needs no image host in the CSP.
function LinkedInLogo() {
  return (
    <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
      <path d="M20.45 20.45h-3.56v-5.57c0-1.33-.02-3.04-1.85-3.04-1.85 0-2.13 1.45-2.13 2.94v5.67H9.35V9h3.41v1.56h.05c.48-.9 1.64-1.85 3.37-1.85 3.6 0 4.27 2.37 4.27 5.46v6.28zM5.34 7.43a2.06 2.06 0 1 1 0-4.13 2.06 2.06 0 0 1 0 4.13zM7.12 20.45H3.56V9h3.56v11.45zM22.22 0H1.77C.79 0 0 .77 0 1.73v20.54C0 23.23.79 24 1.77 24h20.45c.98 0 1.78-.77 1.78-1.73V1.73C24 .77 23.2 0 22.22 0z" />
    </svg>
  );
}
