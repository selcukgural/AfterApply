#!/usr/bin/env python3
"""Builds the two application-tracker spreadsheets offered by the guide article.

    python3 -m venv .venv && .venv/bin/pip install openpyxl
    .venv/bin/python web/scripts/build_guide_spreadsheet.py

Writes web/public/guide/*.xlsx. Kept as a script rather than a checked-in binary edited by hand so
a column change is a diff rather than an archaeology exercise.

Two things here are not cosmetic:

  * The header names are the ones ImportService's CsvColumnMapper already recognises (see
    CsvColumnMapper.cs), in both languages. Someone who outgrows the sheet can save it as CSV and
    import it with no column mapping at all.
  * The Durum/Status dropdown offers the values ImportRowParser.StatusAliases accepts — note
    "Ön Eleme", which is the alias, not the app's own "Ön Değerlendirme" label.
"""

from datetime import date
from pathlib import Path

from openpyxl import Workbook
from openpyxl.styles import Alignment, Font, PatternFill
from openpyxl.utils import get_column_letter
from openpyxl.worksheet.datavalidation import DataValidation

ROWS = 300

HEADER_FILL = PatternFill("solid", fgColor="1D4ED8")
HEADER_FONT = Font(color="FFFFFF", bold=True)
TITLE_FONT = Font(bold=True, size=12)

TR = {
    "file": "is-basvuru-takip-sablonu.xlsx",
    "sheet": "Başvurular",
    "summary_sheet": "Özet",
    "headers": [
        ("Şirket", 26),
        ("Pozisyon", 30),
        ("Başvuru Tarihi", 15),
        ("Durum", 16),
        ("Konum", 18),
        ("İlan Linki", 38),
        ("Son Hareket", 14),
        ("Bekleme (gün)", 14),
        ("Not", 40),
    ],
    "statuses": [
        "Başvuruldu",
        "Ön Eleme",
        "Mülakat",
        "Teknik Mülakat",
        "Final Mülakat",
        "Teklif",
        "Kabul Edildi",
        "Reddedildi",
        "Geri Çekildi",
        "Kayboldu",
    ],
    "open_statuses": ["Başvuruldu", "Ön Eleme"],
    "replied_statuses": ["Mülakat", "Teknik Mülakat", "Final Mülakat", "Teklif", "Kabul Edildi", "Reddedildi"],
    "summary_title": "Özet",
    "summary_note": "Bu sayfadaki her şey Başvurular sekmesinden kendiliğinden hesaplanır. Elle bir şey yazma.",
    "labels": {
        "total": "Toplam başvuru",
        "open": "Hâlâ bekleyen",
        "replied": "Geri dönüş alan",
        "rejected": "Reddedilen",
        "ghosted": "Kayboldu (cevapsız kapattığın)",
        "reply_rate": "Geri dönüş oranı",
        "avg_wait": "Bekleyenlerin ortalama bekleme süresi (gün)",
    },
    "rejected_status": "Reddedildi",
    "ghosted_status": "Kayboldu",
    "example": ["Örnek A.Ş.", "Backend Developer", date(2026, 9, 1), "Başvuruldu", "İstanbul", None, None, None, "örnek satır — silebilirsin"],
    "help": "Bekleme (gün) kolonu kendiliğinden dolar: son hareket tarihini yazarsan ondan, yazmazsan başvuru tarihinden sayar.",
}

EN = {
    "file": "job-application-tracker.xlsx",
    "sheet": "Applications",
    "summary_sheet": "Summary",
    "headers": [
        ("Company", 26),
        ("Job Title", 30),
        ("Applied At", 15),
        ("Status", 16),
        ("Location", 18),
        ("Job URL", 38),
        ("Last Activity", 14),
        ("Waiting (days)", 14),
        ("Notes", 40),
    ],
    "statuses": [
        "Applied",
        "Screening",
        "Interview",
        "Technical Interview",
        "Final Interview",
        "Offer",
        "Accepted",
        "Rejected",
        "Withdrawn",
        "Ghosted",
    ],
    "open_statuses": ["Applied", "Screening"],
    "replied_statuses": ["Interview", "Technical Interview", "Final Interview", "Offer", "Accepted", "Rejected"],
    "summary_title": "Summary",
    "summary_note": "Everything here is calculated from the Applications tab. Nothing to fill in by hand.",
    "labels": {
        "total": "Total applications",
        "open": "Still waiting",
        "replied": "Got a reply",
        "rejected": "Rejected",
        "ghosted": "Ghosted (closed with no reply)",
        "reply_rate": "Reply rate",
        "avg_wait": "Average wait of open applications (days)",
    },
    "rejected_status": "Rejected",
    "ghosted_status": "Ghosted",
    "example": ["Example Ltd", "Backend Developer", date(2026, 9, 1), "Applied", "Amsterdam", None, None, None, "example row — delete me"],
    "help": "The waiting column fills itself: it counts from the last activity date if you set one, otherwise from the application date.",
}


def build(spec: dict, out_dir: Path) -> Path:
    wb = Workbook()
    ws = wb.active
    ws.title = spec["sheet"]

    for index, (title, width) in enumerate(spec["headers"], start=1):
        cell = ws.cell(row=1, column=index, value=title)
        cell.fill = HEADER_FILL
        cell.font = HEADER_FONT
        cell.alignment = Alignment(vertical="center")
        ws.column_dimensions[get_column_letter(index)].width = width
    ws.row_dimensions[1].height = 22
    ws.freeze_panes = "A2"

    for index, value in enumerate(spec["example"], start=1):
        if value is not None:
            ws.cell(row=2, column=index, value=value)

    # Waiting days: blank until the row has a company, then counted from the last activity date if
    # there is one and the application date otherwise.
    for row in range(2, ROWS + 2):
        ws.cell(row=row, column=8, value=f'=IF($A{row}="","",TODAY()-IF($G{row}="",$C{row},$G{row}))')
        ws.cell(row=row, column=3).number_format = "yyyy-mm-dd"
        ws.cell(row=row, column=7).number_format = "yyyy-mm-dd"
        ws.cell(row=row, column=8).number_format = "0"

    validation = DataValidation(type="list", formula1='"' + ",".join(spec["statuses"]) + '"', allow_blank=True)
    ws.add_data_validation(validation)
    validation.add(f"D2:D{ROWS + 1}")

    ws.auto_filter.ref = f"A1:I{ROWS + 1}"

    summary = wb.create_sheet(spec["summary_sheet"])
    summary.column_dimensions["A"].width = 44
    summary.column_dimensions["B"].width = 14
    summary["A1"] = spec["summary_title"]
    summary["A1"].font = TITLE_FONT
    summary["A2"] = spec["summary_note"]

    sheet = f"'{spec['sheet']}'"
    status_range = f"{sheet}!$D$2:$D${ROWS + 1}"
    company_range = f"{sheet}!$A$2:$A${ROWS + 1}"
    wait_range = f"{sheet}!$H$2:$H${ROWS + 1}"

    def count_of(values: list[str]) -> str:
        return "+".join(f'COUNTIF({status_range},"{value}")' for value in values)

    rows = [
        (spec["labels"]["total"], f"=COUNTA({company_range})", "0"),
        (spec["labels"]["open"], f"={count_of(spec['open_statuses'])}", "0"),
        (spec["labels"]["replied"], f"={count_of(spec['replied_statuses'])}", "0"),
        (spec["labels"]["rejected"], f'=COUNTIF({status_range},"{spec["rejected_status"]}")', "0"),
        (spec["labels"]["ghosted"], f'=COUNTIF({status_range},"{spec["ghosted_status"]}")', "0"),
        (
            spec["labels"]["reply_rate"],
            f'=IF(COUNTA({company_range})=0,"",({count_of(spec["replied_statuses"])})/COUNTA({company_range}))',
            "0%",
        ),
        (
            spec["labels"]["avg_wait"],
            # The division has to sit *inside* the IF: with it outside, an empty sheet divides ""
            # by a number and every cell on this tab shows #VALUE!.
            f'=IF({count_of(spec["open_statuses"])}=0,"",'
            f'(SUMIF({status_range},"{spec["open_statuses"][0]}",{wait_range})'
            f'+SUMIF({status_range},"{spec["open_statuses"][1]}",{wait_range}))'
            f'/({count_of(spec["open_statuses"])}))',
            "0",
        ),
    ]
    for offset, (label, formula, number_format) in enumerate(rows):
        row = 4 + offset
        summary.cell(row=row, column=1, value=label)
        cell = summary.cell(row=row, column=2, value=formula)
        cell.number_format = number_format

    summary.cell(row=4 + len(rows) + 1, column=1, value=spec["help"])

    out_dir.mkdir(parents=True, exist_ok=True)
    path = out_dir / spec["file"]
    wb.save(path)
    return path


if __name__ == "__main__":
    out = Path(__file__).resolve().parents[1] / "public" / "guide"
    for spec in (TR, EN):
        print("wrote", build(spec, out))
