import { describe, expect, it } from "vitest";
import { firstInvalidFieldId } from "./firstInvalidField";

describe("firstInvalidFieldId", () => {
  const ids = ["companyName", "jobTitle", "jobUrl", "application-hrEmail", "notes"];

  it("picks the first field on screen, not the first key in the error object", () => {
    expect(firstInvalidFieldId(ids, ["notes", "jobUrl"])).toBe("jobUrl");
  });

  it("matches a prefixed id from a shared field group", () => {
    expect(firstInvalidFieldId(ids, ["hrEmail"])).toBe("application-hrEmail");
  });

  it("does not match a key that is only a suffix of a longer name", () => {
    expect(firstInvalidFieldId(["application-hrEmail"], ["Email"])).toBeNull();
  });

  it("returns null when no field has an error", () => {
    expect(firstInvalidFieldId(ids, [])).toBeNull();
    expect(firstInvalidFieldId(ids, [""])).toBeNull();
  });
});
