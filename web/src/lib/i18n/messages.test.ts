import { describe, expect, it } from "vitest";
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
