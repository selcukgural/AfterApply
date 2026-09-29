import type { ReactNode } from "react";
import { LandingIcon, type LandingIcon as LandingIconName } from "@/components/landing/landingIcons";

interface ToolMenuLabelProps {
  icon: LandingIconName;
  title: ReactNode;
  description: string;
}

/**
 * One row of a "Tools" menu: an icon tile, the tool's name and a line saying what it does. The
 * signed-out header and the signed-in navbar both draw their Tools menu with it, so signing in no
 * longer turns the same menu into a bare list of links (2026-09-29).
 */
export function ToolMenuLabel({ icon, title, description }: ToolMenuLabelProps) {
  return (
    <span className="flex items-start gap-3 py-0.5">
      <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md bg-accent-wash text-accent-ink">
        <LandingIcon name={icon} className="h-[18px] w-[18px]" />
      </span>
      <span className="flex flex-col gap-0.5">
        <span className="flex items-center gap-1.5 font-semibold text-gray-900 dark:text-gray-100">{title}</span>
        <span className="text-[13px] leading-snug font-normal text-gray-600 dark:text-gray-400">{description}</span>
      </span>
    </span>
  );
}
