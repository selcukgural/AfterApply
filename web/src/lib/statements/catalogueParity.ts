import { readFileSync } from "node:fs";

// Test-only helper: the keys a C# statement catalogue declares, rebuilt from its
// `..Liked(Enum.X, "a", "b")` / `..Improve(...)` calls. Both catalogues keep that call shape so
// this one regex reads them; a category the prefix map does not know is reported by name.

export function keysFromCSharp(path: string, enumName: string, prefix: Record<string, string>): string[] {
  const source = readFileSync(path, "utf8");
  const calls = source.matchAll(new RegExp(`\\.\\.(Liked|Improve)\\(${enumName}\\.(\\w+),([^)]*)\\)`, "g"));
  return [...calls].flatMap(([, kind, category, slugs]) => {
    const categoryPrefix = prefix[category];
    if (categoryPrefix === undefined) throw new Error(`unknown category ${category} in the C# catalogue`);
    const segment = kind === "Liked" ? "pos" : "imp";
    return [...slugs.matchAll(/"([a-z0-9_]+)"/g)].map(([, slug]) => `${categoryPrefix}.${segment}.${slug}`);
  });
}

export type MessageTree = { [key: string]: string | MessageTree };

export function lookup(tree: MessageTree, path: string): string | MessageTree | undefined {
  return path.split(".").reduce<string | MessageTree | undefined>((node, key) => {
    return node && typeof node !== "string" ? node[key] : undefined;
  }, tree);
}

export function leaves(tree: MessageTree, prefix = ""): string[] {
  return Object.entries(tree).flatMap(([key, value]) => {
    const path = prefix ? `${prefix}.${key}` : key;
    return typeof value === "string" ? [path] : leaves(value, path);
  });
}
