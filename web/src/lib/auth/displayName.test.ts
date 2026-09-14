import { describe, expect, it } from "vitest";
import { displayName } from "./displayName";

describe("displayName", () => {
  it("uses the name when there is one", () => {
    expect(displayName({ firstName: "Ada", lastName: "Lovelace", email: "ada@example.com" })).toEqual({
      name: "Ada Lovelace",
      initials: "AL",
    });
  });

  it("copes with only one of the two names", () => {
    expect(displayName({ firstName: "Ada", lastName: "", email: "ada@example.com" })).toEqual({ name: "Ada", initials: "A" });
    expect(displayName({ firstName: "", lastName: "Lovelace", email: "ada@example.com" })).toEqual({ name: "Lovelace", initials: "L" });
  });

  it("falls back to the e-mail's local part for an account registered without a name", () => {
    expect(displayName({ firstName: "", lastName: "", email: "ada.lovelace@example.com" })).toEqual({
      name: "ada.lovelace",
      initials: "A",
    });
  });

  it("treats whitespace-only names as absent", () => {
    expect(displayName({ firstName: "  ", lastName: " ", email: "ada@example.com" }).name).toBe("ada");
  });

  it("answers empty strings for no user", () => {
    expect(displayName(null)).toEqual({ name: "", initials: "" });
  });
});
