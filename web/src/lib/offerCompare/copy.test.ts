import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";

// The offer comparison computes someone's pay. What keeps that from reading as a payslip is the
// copy around the number (2026-09-24): an estimate note beside the verdict, the steps written out,
// a disclaimer that sends to the terms, and a clause in the terms themselves. Each of those is a
// string someone could "tidy" away; this is the tripwire.
describe.each([
  ["tr", tr, /tahmin/i, /(bordro|vergi).*(danışmanlığı|değildir)/i],
  ["en", en, /estimate/i, /not (a payslip|payroll)/i],
] as const)("offer comparison disclaimers (%s)", (_, messages, estimate, notAdvice) => {
  const page = messages.offerCompare;

  it("says it is an estimate right next to the verdict, and links to the method", () => {
    expect(page.verdict.note).toMatch(estimate);
    expect(page.verdict.note).toMatch(notAdvice);
    expect(page.verdict.note).toMatch(/<link>.+<\/link>/);
  });

  it("writes the calculation out step by step", () => {
    expect(Object.keys(page.method.steps)).toEqual(["sgk", "base", "exemption", "stamp", "net"]);
  });

  it("disclaims responsibility on the page and points to the terms", () => {
    expect(page.method.disclaimer).toMatch(estimate);
    expect(page.method.disclaimer).toMatch(/e-kariyerim/);
    expect(page.method.disclaimer).toMatch(/<terms>.+<\/terms>/);
  });

  it("is named in the terms, in the service list and under liability", () => {
    expect(messages.terms.service.tools).toMatch(/(teklifi? karşılaştırma|offer comparison)/i);
    expect(messages.terms.liability.offerCompareDisclaimer).toMatch(/(teyit|confirm)/i);
  });
});
