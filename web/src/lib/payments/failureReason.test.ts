import { describe, expect, it } from "vitest";
import { explainFailure } from "./failureReason";

describe("explainFailure", () => {
  it("maps the documented PayTR codes to message keys", () => {
    for (const code of [1, 2, 3, 6, 8, 9, 10, 11, 99]) {
      expect(explainFailure({ failedReasonCode: code, failedReasonMsg: null }).messageKey).toBe(String(code));
    }
  });

  it("shows the bank's own text for code 0 and nothing else", () => {
    const bank = explainFailure({ failedReasonCode: 0, failedReasonMsg: "  Kartın limiti yetersiz " });
    expect(bank.messageKey).toBe("bank");
    expect(bank.bankMessage).toBe("Kartın limiti yetersiz");
    expect(explainFailure({ failedReasonCode: 6, failedReasonMsg: "ignored" }).bankMessage).toBeNull();
  });

  it("treats our own provider rejection and unknown codes distinctly", () => {
    expect(explainFailure({ failedReasonCode: -1, failedReasonMsg: "Zorunlu alan" }).messageKey).toBe("providerRejected");
    expect(explainFailure({ failedReasonCode: 42, failedReasonMsg: null }).messageKey).toBe("unknown");
    expect(explainFailure({ failedReasonCode: null, failedReasonMsg: null }).messageKey).toBe("unknown");
  });

  it("does not suggest retrying after a fraud flag", () => {
    expect(explainFailure({ failedReasonCode: 11, failedReasonMsg: null }).retryable).toBe(false);
    expect(explainFailure({ failedReasonCode: 2, failedReasonMsg: null }).retryable).toBe(true);
  });
});
