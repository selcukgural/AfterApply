"use client";

import { Link } from "@/i18n/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { buttonClassName } from "@/components/ui/Button";

interface CtaButtonsProps {
  primaryLabel: string;
  secondaryLabel: string;
  secondaryHref: string;
  dashboardLabel: string;
}

// Client because the CTA target depends on client-only auth state (spec
// §33: "Get Started" goes to the dashboard for an already-authenticated
// visitor, to registration otherwise). Shared by HeroSection and
// FinalCtaSection — copy is passed in as props since each section quotes
// different strings from the spec's narrative.
export function CtaButtons({ primaryLabel, secondaryLabel, secondaryHref, dashboardLabel }: CtaButtonsProps) {
  const { isAuthenticated } = useAuth();

  if (isAuthenticated) {
    return (
      <Link href="/dashboard" className={buttonClassName("primary", "px-6 py-3 text-base")}>
        {dashboardLabel}
      </Link>
    );
  }

  return (
    <div className="flex flex-wrap items-center justify-center gap-3">
      <Link href="/register" className={buttonClassName("primary", "px-6 py-3 text-base")}>
        {primaryLabel}
      </Link>
      <a href={secondaryHref} className={buttonClassName("secondary", "px-6 py-3 text-base")}>
        {secondaryLabel}
      </a>
    </div>
  );
}
