import { describe, expect, it } from "vitest";
import { scrubSentryBreadcrumb, scrubSentryEvent } from "./sentryScrub";
import { scrubQueryString, scrubUrl } from "./urlSecrets";

describe("scrubUrl", () => {
  it("filters the reset link's token and address and keeps everything else", () => {
    expect(scrubUrl("https://ekariyerim.com/tr/reset-password?email=a%40b.com&token=abc&utm_source=mail")).toBe(
      "https://ekariyerim.com/tr/reset-password?email=[Filtered]&token=[Filtered]&utm_source=mail",
    );
  });

  it("filters an OAuth callback's code and state", () => {
    expect(scrubUrl("/tr/auth/google/callback?code=4/0Ab&state=xyz")).toBe(
      "/tr/auth/google/callback?code=[Filtered]&state=[Filtered]",
    );
  });

  it("filters a hub connection's access_token, whatever its case", () => {
    expect(scrubUrl("wss://api.example/hubs/import-progress?id=1&Access_Token=eyJ")).toBe(
      "wss://api.example/hubs/import-progress?id=1&Access_Token=[Filtered]",
    );
  });

  it("filters tokens in the fragment too", () => {
    expect(scrubUrl("/cb#access_token=eyJ&token_type=bearer")).toBe("/cb#access_token=[Filtered]&token_type=bearer");
  });

  it("leaves a URL without a query alone, and a plain fragment alone", () => {
    expect(scrubUrl("/tr/applications")).toBe("/tr/applications");
    expect(scrubUrl("/tr/guide#section-2")).toBe("/tr/guide#section-2");
  });

  it("passes non-strings through", () => {
    expect(scrubUrl(undefined)).toBeUndefined();
  });

  it("works on a bare query string", () => {
    expect(scrubQueryString("token=x&page=2")).toBe("token=[Filtered]&page=2");
    expect(scrubQueryString("?code=x")).toBe("?code=[Filtered]");
  });
});

describe("scrubSentryEvent", () => {
  it("scrubs the request, its referer, the transaction and the breadcrumbs", () => {
    const event = scrubSentryEvent({
      type: undefined,
      transaction: "/tr/reset-password?token=abc",
      request: {
        url: "https://ekariyerim.com/tr/reset-password?token=abc",
        query_string: "token=abc&x=1",
        headers: { Referer: "https://ekariyerim.com/tr/pair?code=ABCD1234" },
      },
      breadcrumbs: [{ category: "navigation", data: { from: "/a?state=s", to: "/b?code=c" } }],
    });

    expect(event.request?.url).toBe("https://ekariyerim.com/tr/reset-password?token=[Filtered]");
    expect(event.request?.query_string).toBe("token=[Filtered]&x=1");
    expect(event.request?.headers?.Referer).toBe("https://ekariyerim.com/tr/pair?code=[Filtered]");
    expect(event.transaction).toBe("/tr/reset-password?token=[Filtered]");
    expect(event.breadcrumbs?.[0].data).toEqual({ from: "/a?state=[Filtered]", to: "/b?code=[Filtered]" });
  });

  it("scrubs a query string given as pairs", () => {
    const event = scrubSentryEvent({ type: undefined, request: { query_string: [["token", "abc"], ["page", "2"]] } });

    expect(event.request?.query_string).toBe("token=[Filtered]&page=2");
  });
});

describe("scrubSentryBreadcrumb", () => {
  it("scrubs a fetch crumb's url", () => {
    expect(scrubSentryBreadcrumb({ category: "fetch", data: { url: "/api/x?ticket=t", method: "GET" } }).data).toEqual({
      url: "/api/x?ticket=[Filtered]",
      method: "GET",
    });
  });
});
