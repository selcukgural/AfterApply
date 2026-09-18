import { defineRouting } from "next-intl/routing";

export const routing = defineRouting({
  locales: ["tr", "en"],
  defaultLocale: "tr",
  localePrefix: "always",
  // The pages emit their own hreflang set in HTML (`alternateLanguages`, x-default → /tr). The
  // middleware's Link header said x-default → `/`, and two answers to the same question is what a
  // crawler reads as neither (growth audit 2026-09-14, finding 14).
  alternateLinks: false,
});
