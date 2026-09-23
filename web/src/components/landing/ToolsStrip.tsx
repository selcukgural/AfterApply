"use client";

import { useEffect, useId, useRef, useState, useSyncExternalStore, type KeyboardEvent, type ReactNode } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { buttonClassName } from "@/components/ui/Button";
import { BenchmarkResultMock } from "@/components/landing/BenchmarkResultMock";
import { CompanyReviewsMock } from "@/components/landing/CompanyReviewsMock";
import { CvScanResultMock } from "@/components/landing/CvScanResultMock";
import { ExtensionPopupMock } from "@/components/landing/ExtensionPopupMock";
import { OfferCompareMock } from "@/components/landing/OfferCompareMock";
import { LandingIcon, type LandingIcon as LandingIconName } from "@/components/landing/landingIcons";
import { CHROME_WEB_STORE_URL } from "@/lib/constants/chromeWebStore";
import { cvScanPath } from "@/lib/cvScan/path";
import { offerComparePath } from "@/lib/offerCompare/path";

/**
 * The three things the site offers before asking for an account, one screen under the hero:
 * the CV scan, the Chrome extension and the reply-rate benchmark. A tab group rather than three
 * flat cards — pick one and its pitch opens underneath with a demo of what it produces — because
 * until 2026-09-12 the extension was one card of six, eight sections down, and nobody scrolling
 * from the hero would meet it.
 *
 * The extension is the tab that opens first. The hero already sells the scan and holds the
 * primary button for it (2026-09-10); this strip is where the extension gets its first look, and
 * a first look that needs a click first is not one.
 *
 * Which tab is open lives in React state and nowhere else — not in the URL, not in storage. A
 * query parameter would make the landing page carry state into the traffic counter's path
 * allowlist; storage would add a key the cookie policy would then have to list. Nothing here calls
 * trackSiteTraffic either: extension installs are counted by the store, not by us.
 *
 * The strip is also the `#extension` target the navbar and footer point at (a separate extension
 * section was tried and cut on 2026-09-12 as a second copy of the same popup). Arriving by that
 * hash opens the extension tab, so the link always lands on what it names — the hash is read,
 * never written.
 *
 * Markup follows the WAI-ARIA tabs pattern: the card body is the `role="tab"` button (arrow keys
 * move between tabs, focus roves with the selection), the panel is `role="tabpanel"`, and each
 * card's call to action is a separate link beside the tab rather than inside it — a link inside
 * a button is invalid HTML and unreachable by keyboard.
 */
type Tool = "cv" | "extension" | "benchmark" | "companies" | "offer";

// Extension first, scan last (2026-09-12 review): the hero already carries the scan. Companies
// second (2026-09-13): the newest tool, and the only one you browse rather than run.
// The offer comparison (2026-09-24) sits between the benchmark and the scan: it is the second
// calculator, and the scan keeps the last place the hero already gives it the button for.
const TOOLS: readonly Tool[] = ["extension", "companies", "benchmark", "offer", "cv"];

const ICON: Record<Tool, LandingIconName> = {
  cv: "cv",
  extension: "extension",
  benchmark: "analytics",
  companies: "companies",
  offer: "offer",
};

/**
 * Five cards as three over two (strip canvas S2, 2026-09-24): five across left each card four or
 * five lines of copy, a scrolling row hid the fifth. The two wide cards are the newest and the
 * scan; on a two-column tablet the scan, left alone on its row, takes the row.
 */
const CARD_SPAN: Record<Tool, string> = {
  extension: "lg:col-span-2",
  companies: "lg:col-span-2",
  benchmark: "lg:col-span-2",
  offer: "lg:col-span-3",
  cv: "sm:col-span-2 lg:col-span-3",
};

/** The offer card's "New" badge comes off by itself after this. The static page renders without
 *  it (the server snapshot) and the browser adds it, so the two never disagree during hydration. */
const OFFER_NEW_UNTIL = Date.UTC(2026, 10, 1);
const noSubscription = () => () => {};
const offerIsNewNow = () => Date.now() < OFFER_NEW_UNTIL;
const offerIsNewOnServer = () => false;

export function ToolsStrip() {
  const t = useTranslations("landing.tools");
  const locale = useLocale();
  const [active, setActive] = useState<Tool>("extension");
  const offerIsNew = useSyncExternalStore(noSubscription, offerIsNewNow, offerIsNewOnServer);
  const baseId = useId();
  const tabRefs = useRef<Partial<Record<Tool, HTMLButtonElement | null>>>({});

  useEffect(() => {
    const openFromHash = () => {
      if (window.location.hash === "#extension") setActive("extension");
    };
    openFromHash();
    window.addEventListener("hashchange", openFromHash);
    return () => window.removeEventListener("hashchange", openFromHash);
  }, []);

  const tabId = (tool: Tool) => `${baseId}-tab-${tool}`;
  const panelId = (tool: Tool) => `${baseId}-panel-${tool}`;

  const onTabKeyDown = (event: KeyboardEvent<HTMLButtonElement>, tool: Tool) => {
    const index = TOOLS.indexOf(tool);
    let next: Tool | undefined;
    if (event.key === "ArrowRight" || event.key === "ArrowDown") next = TOOLS[(index + 1) % TOOLS.length];
    else if (event.key === "ArrowLeft" || event.key === "ArrowUp") next = TOOLS[(index - 1 + TOOLS.length) % TOOLS.length];
    else if (event.key === "Home") next = TOOLS[0];
    else if (event.key === "End") next = TOOLS[TOOLS.length - 1];
    if (!next) return;

    event.preventDefault();
    setActive(next);
    tabRefs.current[next]?.focus();
  };

  const cards: { tool: Tool; pill?: string; badge?: string; title: string; body: string; cta: ReactNode }[] = [
    {
      tool: "extension",
      pill: t("extensionPill"),
      title: t("extensionTitle"),
      body: t("extensionBody"),
      cta: (
        <a
          href={CHROME_WEB_STORE_URL}
          target="_blank"
          rel="noopener noreferrer"
          className={buttonClassName("primary", "mt-auto inline-flex w-fit items-center gap-1")}
        >
          {t("extensionCta")}
          <span aria-hidden="true">→</span>
        </a>
      ),
    },
    {
      tool: "companies",
      title: t("companiesTitle"),
      body: t("companiesBody"),
      cta: (
        <Link href="/companies" className={buttonClassName("outline", "mt-auto w-fit")}>
          {t("companiesCta")}
        </Link>
      ),
    },
    {
      tool: "benchmark",
      title: t("benchmarkTitle"),
      body: t("benchmarkBody"),
      cta: (
        <Link href="/benchmark" className={buttonClassName("outline", "mt-auto w-fit")}>
          {t("benchmarkCta")}
        </Link>
      ),
    },
    {
      tool: "offer",
      badge: offerIsNew ? t("newBadge") : undefined,
      title: t("offerTitle"),
      body: t("offerBody"),
      cta: (
        <Link href={offerComparePath(locale)} className={buttonClassName("outline", "mt-auto w-fit")}>
          {t("offerCta")}
        </Link>
      ),
    },
    {
      tool: "cv",
      title: t("cvTitle"),
      body: t("cvBody"),
      cta: (
        <Link href={cvScanPath(locale)} className={buttonClassName("outline", "mt-auto w-fit")}>
          {t("cvCta")}
        </Link>
      ),
    },
  ];

  return (
    <section id="extension" className="scroll-mt-20 border-t border-gray-200 py-12 dark:border-gray-800">
      <div className="mx-auto flex max-w-6xl flex-col gap-5 px-4">
        {/* Visible since 2026-09-22 (report item 0.4). It was sr-only, so the only thing a
            sighted reader had for "you can try these without an account" was the same pill printed
            on three of the four cards — the page's most-repeated sentence, said three times inside
            one strip. The heading says it once, where it covers all four. */}
        <h2 className="text-sm font-medium text-accent-ink">{t("title")}</h2>

        <div role="tablist" aria-label={t("tabsLabel")} className="grid gap-4 sm:grid-cols-2 lg:grid-cols-6">
          {cards.map((card) => {
            const selected = card.tool === active;
            return (
              // The whole card is the click target so the tab is as big as it looks; the button
              // inside is what carries the role, the focus and the keyboard handling.
              <div
                key={card.tool}
                // The wrapper sits between the tablist and its tab; without this the tree reads
                // "a tablist whose children are not tabs" (growth audit 2026-09-14, finding 14).
                role="presentation"
                onClick={() => setActive(card.tool)}
                className={`relative flex cursor-pointer flex-col gap-3 rounded-xl border bg-white p-6 transition-shadow dark:bg-gray-900 ${CARD_SPAN[card.tool]} ${
                  selected
                    ? "border-accent/60 ring-[3px] ring-accent-wash"
                    : "border-gray-200 hover:border-gray-300 dark:border-gray-800 dark:hover:border-gray-700"
                }`}
              >
                <div className="flex items-center justify-between">
                  <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-accent-wash text-accent-ink">
                    <LandingIcon name={ICON[card.tool]} />
                  </span>
                  {/* Only where it says something the card does not: the extension card names
                      the two sites it has its own code for. */}
                  {card.pill ? (
                    <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-600 dark:bg-gray-800 dark:text-gray-300">
                      {card.pill}
                    </span>
                  ) : card.badge ? (
                    <span className="rounded-full bg-accent-wash px-2 py-0.5 text-xs font-semibold text-accent-ink">{card.badge}</span>
                  ) : null}
                </div>

                <button
                  type="button"
                  role="tab"
                  id={tabId(card.tool)}
                  aria-selected={selected}
                  aria-controls={panelId(card.tool)}
                  tabIndex={selected ? 0 : -1}
                  ref={(node) => {
                    tabRefs.current[card.tool] = node;
                  }}
                  onKeyDown={(event) => onTabKeyDown(event, card.tool)}
                  className="flex flex-col items-start gap-1 rounded-md text-left focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-accent"
                >
                  <span className="text-lg font-semibold text-gray-900 dark:text-gray-100">{card.title}</span>
                  <span className="text-sm text-gray-600 dark:text-gray-400">{card.body}</span>
                </button>

                {card.cta}

                {selected ? (
                  <span
                    aria-hidden="true"
                    className="absolute -bottom-2 left-1/2 h-3.5 w-3.5 -translate-x-1/2 rotate-45 border-r border-b border-accent/60 bg-white dark:bg-gray-900"
                  />
                ) : null}
              </div>
            );
          })}
        </div>

        <div
          role="tabpanel"
          id={panelId(active)}
          aria-labelledby={tabId(active)}
          // grid-cols-1 on a phone, not the implicit track: an implicit column sizes to its widest
          // child, and the extension demo's address bar made it ~400px on a 390px screen (the page
          // scrolled sideways; found 2026-09-24).
          className="grid grid-cols-1 gap-10 rounded-xl border border-gray-200 bg-white p-6 sm:p-10 lg:grid-cols-2 lg:items-center dark:border-gray-800 dark:bg-gray-900"
        >
          {active === "extension" ? (
            <ExtensionPanel />
          ) : active === "companies" ? (
            <CompaniesPanel />
          ) : active === "cv" ? (
            <CvPanel />
          ) : active === "offer" ? (
            <OfferPanel />
          ) : (
            <BenchmarkPanel />
          )}
        </div>
      </div>
    </section>
  );
}

function PanelCopy({
  eyebrow,
  title,
  body,
  bullets,
  children,
}: {
  eyebrow: string;
  title: string;
  body: string;
  bullets: string[];
  children: ReactNode;
}) {
  return (
    <div className="flex flex-col gap-4">
      <span className="text-sm font-medium text-accent-ink">{eyebrow}</span>
      <h3 className="text-2xl font-semibold text-gray-900 sm:text-[28px] sm:leading-9 dark:text-gray-100">{title}</h3>
      <p className="text-base text-gray-600 dark:text-gray-400">{body}</p>
      <ul className="flex flex-col gap-2.5 text-sm text-gray-800 dark:text-gray-200">
        {bullets.map((bullet) => (
          <li key={bullet} className="flex items-start gap-2">
            <LandingIcon name="check" className="mt-0.5 h-[18px] w-[18px] shrink-0 text-good-ink" />
            <span>{bullet}</span>
          </li>
        ))}
      </ul>
      <div className="flex flex-wrap items-center gap-4 pt-1">{children}</div>
    </div>
  );
}

function ExtensionPanel() {
  const t = useTranslations("landing.tools.panels.extension");

  return (
    <>
      <PanelCopy
        eyebrow={t("eyebrow")}
        title={t("title")}
        body={t("body")}
        bullets={[t("bullet1"), t("bullet2"), t("bullet3")]}
      >
        <a
          href={CHROME_WEB_STORE_URL}
          target="_blank"
          rel="noopener noreferrer"
          className={buttonClassName("primary", "inline-flex items-center gap-1 px-6 py-3 text-base")}
        >
          {t("cta")}
          <span aria-hidden="true">→</span>
        </a>
        <Link
          href="/help/chrome-extension"
          className="text-sm text-gray-600 underline underline-offset-2 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100"
        >
          {t("help")}
        </Link>
      </PanelCopy>
      <ExtensionPopupMock />
    </>
  );
}

function CvPanel() {
  const t = useTranslations("landing.tools.panels.cv");
  const locale = useLocale();

  return (
    <>
      <PanelCopy
        eyebrow={t("eyebrow")}
        title={t("title")}
        body={t("body")}
        bullets={[t("bullet1"), t("bullet2"), t("bullet3")]}
      >
        <Link href={cvScanPath(locale)} className={buttonClassName("primary", "px-6 py-3 text-base")}>
          {t("cta")}
        </Link>
      </PanelCopy>
      <CvScanResultMock />
    </>
  );
}

function CompaniesPanel() {
  const t = useTranslations("landing.tools.panels.companies");

  return (
    <>
      <PanelCopy
        eyebrow={t("eyebrow")}
        title={t("title")}
        body={t("body")}
        bullets={[t("bullet1"), t("bullet2"), t("bullet3")]}
      >
        <Link href="/companies" className={buttonClassName("primary", "px-6 py-3 text-base")}>
          {t("cta")}
        </Link>
        <Link
          href="/companies/scoring"
          className="text-sm text-gray-600 underline underline-offset-2 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100"
        >
          {t("scoringLink")}
        </Link>
      </PanelCopy>
      <CompanyReviewsMock />
    </>
  );
}

function BenchmarkPanel() {
  const t = useTranslations("landing.tools.panels.benchmark");

  return (
    <>
      <PanelCopy
        eyebrow={t("eyebrow")}
        title={t("title")}
        body={t("body")}
        bullets={[t("bullet1"), t("bullet2"), t("bullet3")]}
      >
        <Link href="/benchmark" className={buttonClassName("primary", "px-6 py-3 text-base")}>
          {t("cta")}
        </Link>
      </PanelCopy>
      <BenchmarkResultMock />
    </>
  );
}

function OfferPanel() {
  const t = useTranslations("landing.tools.panels.offer");
  const locale = useLocale();

  return (
    <>
      <PanelCopy
        eyebrow={t("eyebrow")}
        title={t("title")}
        body={t("body")}
        bullets={[t("bullet1"), t("bullet2"), t("bullet3")]}
      >
        <Link href={offerComparePath(locale)} className={buttonClassName("primary", "px-6 py-3 text-base")}>
          {t("cta")}
        </Link>
      </PanelCopy>
      <OfferCompareMock />
    </>
  );
}
