import { readFileSync, readdirSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

/**
 * Source scan, like the other contract tests. Server-side renders name the visitor they render
 * for, proven with a secret (2026-09-24, renderHeaders.server.ts): the secret must stay a
 * server-only variable, only per-request fetches may read the request's headers, and nothing that
 * runs in a browser may import the helper.
 */
const SRC = path.join(process.cwd(), "src");
const read = (relative: string) => readFileSync(path.join(SRC, relative), "utf8");

function sourceFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) return sourceFiles(full);
    return /\.tsx?$/.test(entry.name) && !entry.name.includes(".test.") ? [full] : [];
  });
}

describe("render headers", () => {
  it("read the key from a server-only variable", () => {
    const helper = read("lib/api/renderHeaders.server.ts");
    expect(helper).toContain("process.env.API_SERVER_RENDER_KEY");
    expect(helper).not.toContain("process.env.NEXT_PUBLIC_");
  });

  it("are sent by the per-request fetches of the company and blog pages only", () => {
    for (const file of ["lib/companies/publicApi.server.ts", "lib/blog/publicApi.server.ts"]) {
      expect(read(file)).toContain('const visitor = freshness === "fresh" ? await renderHeaders() : {};');
    }
  });

  it("are never imported by a client component", () => {
    const importers = sourceFiles(SRC).filter((file) => readFileSync(file, "utf8").includes("renderHeaders.server"));
    for (const file of importers) {
      expect(readFileSync(file, "utf8"), file).not.toMatch(/^["']use client["']/);
    }
  });
});
