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
});
