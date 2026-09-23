import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";
import { LANDING_MESSAGE_SCOPE, PUBLIC_MESSAGE_SCOPE, ROOT_MESSAGE_SCOPE, pickMessages } from "./messageScopes";
import { findMessageUsages } from "./messageUsage";

describe("pickMessages", () => {
  const catalogue = { a: { x: "1", y: { z: "2" } }, b: "3", c: { d: "4" } };

  it("keeps whole namespaces", () => {
    expect(pickMessages(catalogue, ["a", "b"])).toEqual({ a: { x: "1", y: { z: "2" } }, b: "3" });
  });

  it("takes one branch of a namespace by dotted path, keeping the tree shape", () => {
    expect(pickMessages(catalogue, ["a.y"])).toEqual({ a: { y: { z: "2" } } });
  });

  it("skips a path the catalogue does not have rather than throwing", () => {
    expect(pickMessages(catalogue, ["nope", "a.nope", "c"])).toEqual({ c: { d: "4" } });
  });

  it("never hands the browser the namespaces the scopes leave out", () => {
    const landing = pickMessages(tr, LANDING_MESSAGE_SCOPE);
    for (const heavy of ["privacy", "help", "adminReviews", "adminMetrics", "applications", "settings", "imports"]) {
      expect(landing, `${heavy} is not in the landing scope`).not.toHaveProperty(heavy);
    }
    // Of the dashboard only the four sample cards' strings travel, not the whole namespace.
    expect(Object.keys((landing as { dashboard: object }).dashboard).sort()).toEqual(["breakdown", "funnel", "outcome", "responseTime"]);
    expect(JSON.stringify(landing).length).toBeLessThan(JSON.stringify(tr).length / 3);
  });

  it("only names namespaces both catalogues have", () => {
    for (const scope of [ROOT_MESSAGE_SCOPE, LANDING_MESSAGE_SCOPE, PUBLIC_MESSAGE_SCOPE]) {
      for (const entry of scope) {
        expect(Object.keys(pickMessages(tr, [entry]))).toHaveLength(1);
        expect(Object.keys(pickMessages(en, [entry]))).toHaveLength(1);
      }
    }
  });
});

/**
 * The drift guard. Each layout provides a fixed slice of the catalogue to its client components
 * (messageScopes.ts); a client component under that layout asking `useTranslations("x")` for a
 * namespace outside the slice renders its raw keys in production, silently. So: walk the import
 * graph from each scope's entry points, collect every namespace a "use client" file asks for, and
 * check it against the scope.
 */
const SRC = path.join(process.cwd(), "src");

function resolveImport(fromFile: string, specifier: string): string | null {
  let base: string;
  if (specifier.startsWith("@/")) base = path.join(SRC, specifier.slice(2));
  else if (specifier.startsWith(".")) base = path.resolve(path.dirname(fromFile), specifier);
  else return null;

  for (const candidate of [base, `${base}.ts`, `${base}.tsx`, path.join(base, "index.ts"), path.join(base, "index.tsx")]) {
    if (existsSync(candidate) && statSync(candidate).isFile()) return candidate;
  }
  return null;
}

function stripComments(source: string): string {
  return source.replace(/\/\*[\s\S]*?\*\//g, "").replace(/^[ \t]*\/\/.*$/gm, "");
}

/**
 * Every full message key (`useTranslations("dashboard")` + `t("funnel.title")` → "dashboard.funnel.title")
 * a client component reachable from the entries asks for, with the files asking. Resolved through
 * the same scanner the catalogue-usage test uses, so a scope may name a branch ("help.sidebar")
 * and still be checked key by key.
 */
function clientMessageKeysFrom(entries: string[]): Map<string, string[]> {
  const found = new Map<string, string[]>();
  const seen = new Set<string>();
  const queue = [...entries];

  while (queue.length > 0) {
    const file = queue.pop()!;
    if (seen.has(file)) continue;
    seen.add(file);
    const source = stripComments(readFileSync(file, "utf8"));

    if (/^\s*["']use client["']/m.test(source)) {
      for (const usage of findMessageUsages(path.relative(SRC, file), source)) {
        found.set(usage.key, [...(found.get(usage.key) ?? []), usage.file]);
      }
    }

    for (const match of source.matchAll(/from\s+["']([^"']+)["']/g)) {
      const resolved = resolveImport(file, match[1]);
      if (resolved) queue.push(resolved);
    }
  }
  return found;
}

function filesUnder(dir: string, names: readonly string[]): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) return filesUnder(full, names);
    return names.includes(entry.name) ? [full] : [];
  });
}

/** "dashboard" covers "dashboard.funnel.title"; "dashboard.funnel" covers that too, but not
 *  "dashboard.outcome.title". A usage bound to several namespaces ("a.k | b.k") passes if any does. */
function covers(scope: readonly string[], usage: string): boolean {
  return usage.split(" | ").some((key) => scope.some((entry) => key === entry || key.startsWith(`${entry}.`)));
}

function expectCovered(reached: Map<string, string[]>, scope: readonly string[]) {
  const missing = [...reached]
    .filter(([key]) => !covers(scope, key))
    .map(([key, files]) => `${key} (${[...new Set(files)].join(", ")})`);
  expect(missing, "client components reach messages their layout does not provide").toEqual([]);
}

describe("message scopes cover what their client components ask for", () => {
  it("root layout and the 404 page", () => {
    const entries = [path.join(SRC, "app/[locale]/layout.tsx"), path.join(SRC, "app/[locale]/not-found.tsx")];
    expectCovered(clientMessageKeysFrom(entries), ROOT_MESSAGE_SCOPE);
  });

  it("landing page", () => {
    expectCovered(clientMessageKeysFrom([path.join(SRC, "app/[locale]/page.tsx")]), LANDING_MESSAGE_SCOPE);
  });

  it("every signed-out page and layout", () => {
    const entries = filesUnder(path.join(SRC, "app/[locale]/(public)"), ["page.tsx", "layout.tsx"]);
    expect(entries.length).toBeGreaterThan(20);
    expectCovered(clientMessageKeysFrom(entries), PUBLIC_MESSAGE_SCOPE);
  });

  it("actually reaches client components (the walk is not a no-op)", () => {
    const reached = [...clientMessageKeysFrom([path.join(SRC, "app/[locale]/page.tsx")]).keys()];
    expect(reached.some((key) => key.startsWith("landing.hero."))).toBe(true);
    expect(reached.some((key) => key.startsWith("siteNav."))).toBe(true);
    expect(reached.some((key) => key.startsWith("dashboard.funnel."))).toBe(true);
  });
});

describe("the layouts provide the scopes", () => {
  const read = (relative: string) => readFileSync(path.join(SRC, relative), "utf8");

  it("root, landing and public each pick their own slice; the signed-in layout takes the whole catalogue", () => {
    expect(read("app/[locale]/layout.tsx")).toContain("pickMessages(await getMessages(), ROOT_MESSAGE_SCOPE)");
    expect(read("app/[locale]/page.tsx")).toContain("pickMessages(await getMessages(), LANDING_MESSAGE_SCOPE)");
    expect(read("app/[locale]/(public)/layout.tsx")).toContain("pickMessages(await getMessages(), PUBLIC_MESSAGE_SCOPE)");
    expect(read("app/[locale]/(protected)/layout.tsx")).toMatch(/<NextIntlClientProvider>/);
  });
});
