import { readFileSync, readdirSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";

// We publish a Cookie Policy (/cookies) that makes a strong claim: everything we put on a
// visitor's device is strictly necessary or functional, there are no third-party or tracking
// cookies, and therefore no consent banner is required (KVKK's cookie guidance asks for
// disclosure, not consent, in exactly that case — see DECISIONS.md).
//
// That claim is true today and nothing in the type system keeps it true. One `<Script
// src="googletagmanager...">` or one new cookie makes the published page a false statement to
// every visitor, silently. This test is the tripwire: it fails when the inventory changes, so
// whoever changes it has to decide — update the policy, or turn on a consent flow.

const WEB_ROOT = fileURLToPath(new URL("../../..", import.meta.url));
const SRC = path.join(WEB_ROOT, "src");

// The whole inventory, as published on /cookies. `NEXT_LOCALE` is written server-side by
// next-intl's proxy (src/proxy.ts); `theme` by src/lib/theme/theme.ts.
const COOKIES = ["NEXT_LOCALE", "theme"];

const STORAGE_KEYS = [
  "aa_access_token",
  "aa_access_token_expires_at",
  "aa_refresh_token",
  "aa_refresh_token_expires_at",
  "aa_user",
  "aa_google_oauth",
  "aa_linkedin_oauth",
  "aa_github_oauth",
];

// The subset the policy names verbatim rather than describing as a group ("their expiry times").
const KEYS_NAMED_IN_POLICY = [
  "aa_access_token",
  "aa_refresh_token",
  "aa_user",
  "aa_google_oauth",
  "aa_linkedin_oauth",
  "aa_github_oauth",
];

const COOKIE_WRITERS = ["lib/theme/theme.ts"];
const STORAGE_WRITERS = [
  "lib/api/tokenStorage.ts",
  "lib/auth/githubOAuth.ts",
  "lib/auth/googleOAuth.ts",
  "lib/auth/linkedinOAuth.ts",
];

// Names that only appear in a codebase because something is being measured, tagged or replayed.
const TRACKERS = [
  "googletagmanager",
  "google-analytics",
  "gtag(",
  "fbq(",
  "hotjar",
  "clarity.ms",
  "mixpanel",
  "amplitude",
  "posthog",
  "plausible",
  "matomo",
  "@vercel/analytics",
  "@next/third-parties",
];

interface SourceFile {
  relativePath: string;
  content: string;
}

function readSourceFiles(dir: string): SourceFile[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) return readSourceFiles(full);
    if (!/\.tsx?$/.test(entry.name) || entry.name.endsWith(".test.ts")) return [];
    return [{ relativePath: path.relative(SRC, full), content: readFileSync(full, "utf8") }];
  });
}

const sources = readSourceFiles(SRC);

function filesMatching(pattern: RegExp): string[] {
  return sources
    .filter((file) => pattern.test(file.content))
    .map((file) => file.relativePath)
    .sort();
}

describe("browser storage inventory", () => {
  it("is written from the files the cookie policy accounts for", () => {
    expect(filesMatching(/document\.cookie\s*=/)).toEqual(COOKIE_WRITERS);
    expect(filesMatching(/(local|session)Storage\.(setItem|removeItem|clear)/)).toEqual(STORAGE_WRITERS);
  });

  it("uses no storage key beyond the published ones", () => {
    const used = new Set(sources.flatMap((file) => file.content.match(/"aa_[a-z_]+"/g) ?? []).map((key) => key.slice(1, -1)));
    expect([...used].sort()).toEqual([...STORAGE_KEYS].sort());
  });

  it("names every cookie on the cookie policy page", () => {
    const page = readFileSync(path.join(SRC, "app/[locale]/(public)/cookies/page.tsx"), "utf8");
    for (const name of COOKIES) {
      expect(page, `${name} is set but not listed on /cookies`).toContain(name);
    }
  });

  it("names the storage keys the policy claims to name, in both languages", () => {
    const catalogues = { tr: JSON.stringify(tr), en: JSON.stringify(en) };
    for (const [locale, catalogue] of Object.entries(catalogues)) {
      for (const key of KEYS_NAMED_IN_POLICY) {
        expect(catalogue, `${key} is missing from the ${locale} cookie policy`).toContain(key);
      }
    }
  });
});

describe("no tracking", () => {
  it("depends on no analytics or tag-manager package", () => {
    const manifest = JSON.parse(readFileSync(path.join(WEB_ROOT, "package.json"), "utf8"));
    const dependencies = Object.keys({ ...manifest.dependencies, ...manifest.devDependencies });
    expect(dependencies.filter((name) => TRACKERS.some((tracker) => name.includes(tracker)))).toEqual([]);
  });

  it("references no tracker in the source", () => {
    const offenders = sources
      .filter((file) => TRACKERS.some((tracker) => file.content.includes(tracker)))
      .map((file) => file.relativePath);
    expect(offenders).toEqual([]);
  });

  it("keeps Sentry's session replay and tracing off", () => {
    // Both would start recording browsing behaviour, which is the line between "error monitoring
    // on legitimate interest" (what /privacy#error-monitoring discloses) and something that needs
    // its own consent.
    const client = readFileSync(path.join(SRC, "instrumentation-client.ts"), "utf8");
    for (const option of [
      "replaysSessionSampleRate",
      "replaysOnErrorSampleRate",
      "replayIntegration",
      "tracesSampleRate",
      "browserTracingIntegration",
    ]) {
      expect(client, `Sentry ${option} would need a consent decision first`).not.toContain(option);
    }
  });
});

// The visit counter (V0) is first-party and cookieless, which is why it does not trip any of the
// checks above — and exactly why it needs its own. Nothing in the type system stops someone
// mounting the reporter on a signed-in page, routing it through apiFetch (which would attach the
// visitor's Authorization header), or adding a field to the payload. Each of those would quietly
// turn "we count visits" into "we track people", against a published page that says otherwise.
describe("visit counter", () => {
  const TRACKER = "lib/analytics/siteTraffic.ts";

  // Every file allowed to report a visit. All three are public-side; the reporter component is
  // mounted in the (public) layout, which is what keeps signed-in pages out structurally rather
  // than by a second copy of the API's route allowlist.
  const CALLERS = [
    "app/[locale]/(public)/register/page.tsx",
    "components/analytics/SiteTrafficReporter.tsx",
    "components/landing/CtaButtons.tsx",
  ];

  it("is reported from public pages only", () => {
    const callers = filesMatching(/trackSiteTraffic\(/).filter((file) => file !== TRACKER);

    expect(callers).toEqual([...CALLERS].sort());
    for (const caller of callers) {
      expect(caller, `${caller} is a signed-in page and must not report visits`).not.toContain("(protected)");
    }
  });

  it("never sends the visitor's credentials", () => {
    const tracker = sources.find((file) => file.relativePath === TRACKER);
    expect(tracker, "the visit counter moved; this guard needs its new path").toBeDefined();

    // apiFetch attaches the access token and refreshes it on 401 — either would tie a page view
    // to an account. The counter must use a bare fetch that omits credentials. Matched as a call
    // and as an import rather than as a word, because the file explains in prose why it does not
    // use apiFetch, and that explanation is worth keeping.
    expect(tracker!.content).not.toMatch(/\bapiFetch\s*[(<]/);
    expect(tracker!.content).not.toMatch(/import\s*\{[^}]*\bapiFetch\b/);
    expect(tracker!.content).toContain('credentials: "omit"');
  });

  it("describes itself on the cookie policy and the privacy policy, in both languages", () => {
    for (const [locale, catalogue] of Object.entries({ tr, en })) {
      expect(catalogue.cookies, `the ${locale} cookie policy does not mention the visit counter`)
        .toHaveProperty("visitCounter");
      expect(catalogue.privacy, `the ${locale} privacy policy does not mention the visit counter`)
        .toHaveProperty("visitCounter");
    }

    const cookiePage = readFileSync(path.join(SRC, "app/[locale]/(public)/cookies/page.tsx"), "utf8");
    expect(cookiePage, "the section exists in the catalogue but nothing renders it").toContain("visitCounter.title");
  });
});
