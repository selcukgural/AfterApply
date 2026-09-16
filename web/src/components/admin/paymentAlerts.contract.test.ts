import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";
import { ALERT_PAGE_SIZE, alertQueryString } from "@/lib/api/payments";
import { ALERT_DAY_OPTIONS, ALERT_OUTCOMES, ALERT_SORT_KEYS, DEFAULT_ALERT_FILTERS, isFiltered } from "./PaymentAlertsSection";

describe("admin payment alerts", () => {
  it("ask the server for ten rows, newest first, over the last thirty days by default", () => {
    const params = new URLSearchParams(alertQueryString(DEFAULT_ALERT_FILTERS));
    expect(ALERT_PAGE_SIZE).toBe(10);
    expect(params.get("pageSize")).toBe("10");
    expect(params.get("page")).toBe("1");
    expect(params.get("days")).toBe("30");
    expect(params.get("sort")).toBe("receivedAt");
    expect(params.get("dir")).toBe("desc");
    expect(params.has("outcome")).toBe(false);
    expect(params.has("status")).toBe(false);
    expect(params.has("testMode")).toBe(false);
    expect(params.has("q")).toBe(false);
  });

  it("send every filter under the name the API reads, and only when set", () => {
    const params = new URLSearchParams(
      alertQueryString({ days: 7, outcome: "BadHash", status: "failed", testMode: false, q: "abc", sort: "outcome", dir: "asc", page: 3, pageSize: 25 }),
    );
    expect(Object.fromEntries(params)).toEqual({
      days: "7",
      outcome: "BadHash",
      status: "failed",
      testMode: "false",
      q: "abc",
      sort: "outcome",
      dir: "asc",
      page: "3",
      pageSize: "25",
    });
  });

  it("count a test-mode choice of 'live' (false) as a filter, not as unset", () => {
    expect(isFiltered(DEFAULT_ALERT_FILTERS)).toBe(false);
    expect(isFiltered({ ...DEFAULT_ALERT_FILTERS, testMode: false })).toBe(true);
    expect(isFiltered({ ...DEFAULT_ALERT_FILTERS, days: 7 })).toBe(true);
    // Sorting narrows nothing; a sorted default list still reads "no alerts", not "no match".
    expect(isFiltered({ ...DEFAULT_ALERT_FILTERS, sort: "outcome", dir: "asc" })).toBe(false);
  });

  it.each(["tr", "en"] as const)("have a label for every outcome, sort key and window in %s", (locale) => {
    const messages = (locale === "tr" ? tr : en).adminPayments;
    const outcomes = messages.outcome as Record<string, string>;
    const alerts = messages.alerts as Record<string, unknown>;
    expect(ALERT_OUTCOMES.filter((o) => !outcomes[o])).toEqual([]);
    expect(ALERT_SORT_KEYS.filter((k) => !alerts[`sort${k[0].toUpperCase()}${k.slice(1)}`])).toEqual([]);
    expect(alerts.daysOption).toContain("{days}");
    expect(alerts.title).toContain("{days}");
    expect(ALERT_DAY_OPTIONS).toContain(DEFAULT_ALERT_FILTERS.days);
    const pagination = (locale === "tr" ? tr : en).applications.pagination as Record<string, string>;
    expect(pagination.pageInfoAlerts).toContain("{totalCount}");
  });
});
