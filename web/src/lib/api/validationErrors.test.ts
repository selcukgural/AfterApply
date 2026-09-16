import { describe, expect, it } from "vitest";
import { ApiError } from "./httpClient";
import { fieldErrorsOf } from "./validationErrors";

describe("fieldErrorsOf", () => {
  it("maps each field's first message under the camelCased name", () => {
    const error = new ApiError(400, "One or more validation errors occurred.", {
      errors: { BillingAddress: ["Adres girin.", "İkinci mesaj"], BillingPhone: ["Telefon girin."] },
    });
    expect(fieldErrorsOf(error)).toEqual({ billingAddress: "Adres girin.", billingPhone: "Telefon girin." });
  });

  it("is empty for a problem without field errors, a plain error, or no body", () => {
    expect(fieldErrorsOf(new ApiError(400, "Ödeme sağlayıcısına ulaşılamadı.", { detail: "x" }))).toEqual({});
    expect(fieldErrorsOf(new ApiError(500, "boom"))).toEqual({});
    expect(fieldErrorsOf(new Error("network"))).toEqual({});
  });

  it("ignores fields whose messages are not strings", () => {
    expect(fieldErrorsOf(new ApiError(400, "", { errors: { Plan: [42], Empty: [] } }))).toEqual({});
  });
});
