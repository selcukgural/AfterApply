import { describe, expect, it } from "vitest";
import type { SectorResponseRateRow } from "@/types/api";
import { splitSectorRows } from "./rows";

const figures = (applications: number) => ({
  applications,
  contributors: 5,
  responseRate: 50,
  ghostingRate: 20,
  interviewRate: 10,
  offerRate: 2,
  postInterviewSilenceRate: null,
  medianFirstReplyDays: 7,
  closureRate: 30,
});

describe("splitSectorRows", () => {
  it("puts the visible rows first, largest sample leading, and keeps hidden rows in server order", () => {
    const rows: SectorResponseRateRow[] = [
      { sector: "SoftwareAndIt", figures: figures(40) },
      { sector: "FinanceAndInsurance", figures: null },
      { sector: "EcommerceAndRetail", figures: figures(90) },
      { sector: "Telecom", figures: null },
    ];

    const { visible, hidden } = splitSectorRows(rows);

    expect(visible.map((row) => row.sector)).toEqual(["EcommerceAndRetail", "SoftwareAndIt"]);
    expect(hidden.map((row) => row.sector)).toEqual(["FinanceAndInsurance", "Telecom"]);
  });

  it("does not mutate the server's list", () => {
    const rows: SectorResponseRateRow[] = [
      { sector: "SoftwareAndIt", figures: figures(1) },
      { sector: "Telecom", figures: figures(9) },
    ];
    splitSectorRows(rows);
    expect(rows[0].sector).toBe("SoftwareAndIt");
  });
});
