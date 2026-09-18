/**
 * The product's own accounts — the footer's icons, the about page's contact line and the
 * Organization JSON-LD's `sameAs` all read this one list, so the three cannot disagree (growth
 * audit 2026-09-14, findings 07 and 11). No personal accounts: the product speaks as a team.
 */
export type SocialNetwork = "linkedin" | "x" | "instagram";

export const SOCIAL_LINKS: readonly { network: SocialNetwork; label: string; href: string }[] = [
  { network: "linkedin", label: "LinkedIn", href: "https://www.linkedin.com/company/ekariyerim" },
  { network: "x", label: "X", href: "https://x.com/ekariyerim" },
  { network: "instagram", label: "Instagram", href: "https://www.instagram.com/ekariyerim" },
];

/** Where a person writes to the team. The same address the refund policy and the help centre name. */
export const CONTACT_EMAIL = "destek@ekariyerim.com";
