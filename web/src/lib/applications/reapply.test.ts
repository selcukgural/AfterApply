import { describe, expect, it } from "vitest";
import { reapplyOpeningsLink } from "./reapply";

const none = { companyLinkedInUrl: null, companyWebsite: null, companySlug: null };

describe("reapplyOpeningsLink", () => {
  it("prefers the company's LinkedIn jobs tab", () => {
    expect(reapplyOpeningsLink({ ...none, companyLinkedInUrl: "https://www.linkedin.com/company/lodos/?trk=x", companyWebsite: "https://lodos.example" }))
      .toEqual({ kind: "external", href: "https://www.linkedin.com/company/lodos/jobs/" });
  });

  it("falls back to the website, then to the company's page here", () => {
    expect(reapplyOpeningsLink({ ...none, companyWebsite: "https://lodos.example/" })).toEqual({ kind: "external", href: "https://lodos.example/" });
    expect(reapplyOpeningsLink({ ...none, companySlug: "lodos" })).toEqual({ kind: "internal", href: "/companies/lodos" });
    expect(reapplyOpeningsLink(none)).toBeNull();
  });

  it("never links out to anything but http(s)", () => {
    expect(reapplyOpeningsLink({ ...none, companyLinkedInUrl: "javascript:alert(1)", companyWebsite: "//evil.example", companySlug: "lodos" }))
      .toEqual({ kind: "internal", href: "/companies/lodos" });
  });

  it("only treats a real linkedin.com company page as LinkedIn", () => {
    for (const url of [
      "https://evil.example/linkedin.com/company/lodos",
      "https://evil.example/?u=linkedin.com/company/lodos",
      "https://linkedin.com.evil.example/company/lodos",
      "https://www.linkedin.com/in/someone",
    ]) {
      expect(reapplyOpeningsLink({ ...none, companyLinkedInUrl: url, companySlug: "lodos" }))
        .toEqual({ kind: "internal", href: "/companies/lodos" });
    }
  });

  it("points at the company's jobs tab even from a deeper company page", () => {
    expect(reapplyOpeningsLink({ ...none, companyLinkedInUrl: "https://tr.linkedin.com/company/lodos/about/#top" }))
      .toEqual({ kind: "external", href: "https://tr.linkedin.com/company/lodos/jobs/" });
  });
});
