import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { PAYTR_ORIGIN, buildCsp, createNonce } from "./contentSecurityPolicy";

const ORIGINS = { apiOrigin: "https://api.ekariyerim.com", sentryOrigin: "https://o1.ingest.de.sentry.io" };

function directives(csp: string): Record<string, string> {
  return Object.fromEntries(
    csp.split("; ").map((part) => {
      const [name, ...values] = part.split(" ");
      return [name, values.join(" ")];
    }),
  );
}

function policy(pathname = "/tr", isDev = false, nonce = "abc123") {
  return directives(buildCsp({ nonce, pathname, origins: ORIGINS, isDev }));
}

describe("buildCsp", () => {
  it("lets only scripts carrying this response's nonce run — no 'unsafe-inline', no eval", () => {
    const scriptSrc = policy()["script-src"];

    expect(scriptSrc).toContain("'nonce-abc123'");
    expect(scriptSrc).toContain("'strict-dynamic'");
    expect(scriptSrc).not.toContain("'unsafe-inline'");
    expect(scriptSrc).not.toContain("'unsafe-eval'");
  });

  it("allows eval in development only, where React uses it for error stacks", () => {
    expect(policy("/tr", true)["script-src"]).toContain("'unsafe-eval'");
  });

  it("connects only to our own origin, the API (https and wss) and Sentry", () => {
    expect(policy()["connect-src"]).toBe(
      "'self' https://api.ekariyerim.com wss://api.ekariyerim.com https://o1.ingest.de.sentry.io",
    );
  });

  it("forbids framing everywhere except the PayTR return page, which PayTR may frame", () => {
    expect(policy("/tr/dashboard")["frame-ancestors"]).toBe("'none'");
    expect(policy("/tr/pro/checkout")["frame-ancestors"]).toBe("'none'");
    expect(policy("/en/pro/return/abc")["frame-ancestors"]).toBe(`'self' ${PAYTR_ORIGIN}`);
  });

  it("keeps the legacy escape hatches closed", () => {
    const csp = policy();
    expect(csp["object-src"]).toBe("'none'");
    expect(csp["base-uri"]).toBe("'self'");
    expect(csp["form-action"]).toBe("'self'");
    expect(csp["default-src"]).toBe("'self'");
  });
});

describe("createNonce", () => {
  it("is 128 random bits and new every time", () => {
    const nonces = new Set(Array.from({ length: 50 }, () => createNonce()));

    expect(nonces.size).toBe(50);
    for (const nonce of nonces) {
      expect(atob(nonce)).toHaveLength(16);
    }
  });
});

// The nonce only protects anything if every rendered page gets it; these pin the three places
// that make that true, since each one alone would pass a build and only fail in a browser.
describe("wiring", () => {
  const root = path.resolve(__dirname, "../../..");
  const read = (file: string) => readFileSync(path.join(root, file), "utf8");

  it("sets the CSP per request in the proxy, not statically in next.config", () => {
    expect(read("next.config.ts")).not.toContain('key: "Content-Security-Policy"');
    const proxy = read("src/proxy.ts");
    expect(proxy).toContain('headers.set("Content-Security-Policy", csp)');
    expect(proxy).toContain('response.headers.set("Content-Security-Policy", csp)');
    expect(proxy).toContain("withLocale(new NextRequest(request, { headers }))");
  });

  it("stamps the nonce on the root layout's own inline script", () => {
    const layout = read("src/app/[locale]/layout.tsx");
    expect(layout).toContain('(await headers()).get("x-nonce")');
    expect(layout).toContain("<script nonce={nonce} dangerouslySetInnerHTML={{ __html: THEME_BOOT_SCRIPT }} />");
  });
});
