import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { ApiError } from "@/lib/api/httpClient";
import {
  FEATURE_FLAGS,
  FLAG_GROUPS,
  canReset,
  formatCountdown,
  isBlocked,
  isOvertaken,
  localDeadline,
  mustStartAgain,
  phraseMatches,
  problemCode,
  secondsLeft,
  targetOf,
  toggleKind,
} from "./featureFlags";

describe("the flag list", () => {
  it("matches the API's FeatureFlag enum member for member", () => {
    const source = readFileSync(path.join(process.cwd(), "..", "src/AfterApply.Application/FeatureFlags/FeatureFlag.cs"), "utf8");
    const body = source.slice(source.indexOf("public enum FeatureFlag"), source.indexOf("}", source.indexOf("public enum FeatureFlag")));
    const members = [...body.matchAll(/^\s{4}([A-Z][A-Za-z]+),?\s*$/gm)].map((match) => match[1]);
    expect(members.length).toBe(17);
    expect([...FEATURE_FLAGS].sort()).toEqual([...members].sort());
  });

  it("puts every flag in exactly one group, in the list's order", () => {
    expect(FLAG_GROUPS.flatMap((group) => group.flags)).toEqual([...FEATURE_FLAGS]);
  });
});

describe("a change", () => {
  it("sends the opposite state, or null to drop the panel's value", () => {
    expect(targetOf("turnOn")).toBe(true);
    expect(targetOf("turnOff")).toBe(false);
    expect(targetOf("reset")).toBeNull();
    expect(toggleKind({ enabled: true })).toBe("turnOff");
    expect(toggleKind({ enabled: false })).toBe("turnOn");
  });

  it("offers a reset only when the panel has a value", () => {
    expect(canReset({ override: null })).toBe(false);
    expect(canReset({ override: false })).toBe(true);
  });

  it("blocks switching on, never off, while configuration is missing", () => {
    expect(isBlocked({ missingPrerequisite: "PAYTR_NOT_CONFIGURED" }, "turnOn")).toBe(true);
    expect(isBlocked({ missingPrerequisite: "PAYTR_NOT_CONFIGURED" }, "turnOff")).toBe(false);
    expect(isBlocked({ missingPrerequisite: "PAYTR_NOT_CONFIGURED" }, "reset")).toBe(false);
    expect(isBlocked({ missingPrerequisite: null }, "turnOn")).toBe(false);
  });
});

describe("a change someone else already made", () => {
  it("is recognised for every kind", () => {
    expect(isOvertaken("turnOff", { enabled: false, override: false })).toBe(true);
    expect(isOvertaken("turnOff", { enabled: true, override: null })).toBe(false);
    expect(isOvertaken("turnOn", { enabled: true, override: true })).toBe(true);
    expect(isOvertaken("turnOn", { enabled: false, override: null })).toBe(false);
    expect(isOvertaken("reset", { enabled: true, override: null })).toBe(true);
    expect(isOvertaken("reset", { enabled: true, override: true })).toBe(false);
  });
});

describe("the second step", () => {
  it("accepts the name exactly, surrounding spaces aside", () => {
    expect(phraseMatches("EmailSignals", "EmailSignals")).toBe(true);
    expect(phraseMatches("  EmailSignals ", "EmailSignals")).toBe(true);
    expect(phraseMatches("emailsignals", "EmailSignals")).toBe(false);
    expect(phraseMatches("Email Signals", "EmailSignals")).toBe(false);
    expect(phraseMatches("", "EmailSignals")).toBe(false);
  });

  it("counts down from when the answer arrived, a margin short, whatever the local clock says", () => {
    // An admin whose clock is an hour off: only the difference between two local readings matters.
    const receivedAt = Date.parse("2026-09-27T13:00:00Z");
    const deadline = localDeadline(receivedAt, 300);
    expect(secondsLeft(deadline, receivedAt)).toBe(295);
    expect(secondsLeft(deadline, receivedAt + 295_000)).toBe(0);
    expect(secondsLeft(deadline, receivedAt + 400_000)).toBe(0);
    expect(localDeadline(receivedAt, 0)).toBe(receivedAt);
  });

  it("counts down in whole seconds and never below zero", () => {
    const now = Date.parse("2026-09-27T12:00:00Z");
    expect(secondsLeft(now + 272_900, now)).toBe(272);
    expect(secondsLeft(now - 60_000, now)).toBe(0);
    expect(formatCountdown(272)).toBe("4:32");
    expect(formatCountdown(5)).toBe("0:05");
    expect(formatCountdown(-3)).toBe("0:00");
  });

  it("starts again after a spent, expired or overtaken token — and only then", () => {
    const conflict = (code: string) => new ApiError(409, "x", { code });
    expect(mustStartAgain(conflict("FEATURE_FLAG_CHANGED_SINCE_PREPARE"))).toBe(true);
    expect(mustStartAgain(conflict("FEATURE_FLAG_CONFIRMATION_INVALID"))).toBe(true);
    expect(mustStartAgain(conflict("FEATURE_FLAG_PREREQUISITE_MISSING"))).toBe(true);
    expect(mustStartAgain(conflict("FEATURE_FLAG_UNCHANGED"))).toBe(false);
    expect(mustStartAgain(new ApiError(400, "x", { errors: {} }))).toBe(false);
    expect(mustStartAgain(new Error("network"))).toBe(false);
    expect(problemCode(conflict("FEATURE_FLAG_UNCHANGED"))).toBe("FEATURE_FLAG_UNCHANGED");
  });
});
