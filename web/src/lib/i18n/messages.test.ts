import { readFileSync, readdirSync } from "node:fs";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { findMessageUsages, isResolvable } from "./messageUsage";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";

// A missing translation doesn't fail the build or the type-check — next-intl falls back to
// printing the key, so an untranslated string ships as "extensionPrivacy.gmail.dedupe" on a
// public page. Comparing the two key trees is the only thing that catches it before a reader does.
type MessageTree = { [key: string]: string | MessageTree };

function flatten(tree: MessageTree, prefix = ""): string[] {
  return Object.entries(tree).flatMap(([key, value]) => {
    const path = prefix ? `${prefix}.${key}` : key;
    return typeof value === "string" ? [path] : flatten(value, path);
  });
}

describe("message catalogues", () => {
  const trKeys = flatten(tr as MessageTree).sort();
  const enKeys = flatten(en as MessageTree).sort();

  it("has the same keys in Turkish and English", () => {
    expect(enKeys.filter((key) => !trKeys.includes(key))).toEqual([]);
    expect(trKeys.filter((key) => !enKeys.includes(key))).toEqual([]);
  });

  it("has no empty strings", () => {
    const empty = (tree: MessageTree, locale: string) =>
      flatten(tree).filter((path) => {
        const value = path.split(".").reduce<string | MessageTree>((node, key) => (node as MessageTree)[key], tree);
        return typeof value === "string" && value.trim() === "";
      }).map((path) => `${locale}:${path}`);

    expect([...empty(tr as MessageTree, "tr"), ...empty(en as MessageTree, "en")]).toEqual([]);
  });
});

describe("message usage", () => {
  const SRC = fileURLToPath(new URL("../..", import.meta.url));
  const known = new Set(flatten(en as MessageTree));

  function sourceFiles(dir: string): string[] {
    return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
      const full = join(dir, entry.name);
      if (entry.isDirectory()) return sourceFiles(full);
      if (!/\.tsx?$/.test(entry.name) || /\.test\.tsx?$/.test(entry.name)) return [];
      return [full];
    });
  }

  // A key removed from both catalogues but left in the source renders its own key path on the
  // page — next-intl prints the key rather than failing, so nothing else catches it.
  it("asks for no message that neither catalogue has", () => {
    const missing = sourceFiles(SRC)
      .flatMap((file) => findMessageUsages(relative(SRC, file), readFileSync(file, "utf8")))
      .filter((usage) => !isResolvable(usage, known))
      .map((usage) => `${usage.file}: ${usage.key}`);

    expect([...new Set(missing)]).toEqual([]);
  });
});
