"use client";

import type { ReactNode } from "react";
import { Link } from "@/i18n/navigation";
import { trackSiteTraffic } from "@/lib/analytics/siteTraffic";

/**
 * The sign-up link inside a free tool — the CV scan and benchmark results, the response-rate
 * table, the "no reply" report's thank-you. Counted as `cta_tool_sign_up` under the tool's own
 * page, so /admin/metrics can put "got a result" next to "reached for an account" per tool
 * (2026-10-09). A client island so a server-rendered page can use it too.
 */
export function ToolSignUpLink({
  href = "/register",
  className,
  children,
}: {
  href?: string;
  className?: string;
  children: ReactNode;
}) {
  return (
    <Link href={href} className={className} onClick={() => trackSiteTraffic("cta_tool_sign_up")}>
      {children}
    </Link>
  );
}
