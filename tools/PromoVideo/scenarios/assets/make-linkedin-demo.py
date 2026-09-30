"""Builds the made-up LinkedIn export the feature videos import (Basic_LinkedInDataExport_09-29-2026.zip).

Every company is invented and was checked against web search on 2026-09-30 so none names a real
business: the videos show who "ghosted" or rejected the candidate, which must not be pinned on a
real company. Job Url stays empty on purpose — the importer fetches LinkedIn job pages it is given,
and invented job ids could land on real postings. Deterministic: same file every run.

    python3 tools/PromoVideo/scenarios/assets/make-linkedin-demo.py
"""
import csv
import io
import os
import random
import zipfile
from datetime import datetime, timedelta

COMPANIES = [
    "Nordika Fintek", "Taravel Lojistik", "Ostrin Yazılım", "Kalvera Sağlık", "Lumeta Yazılım",
    "Dravia Oyun", "Solvenk Enerji", "Orvanta Telekom", "Pelisa Teknoloji", "Isparo Ödeme",
    "Alvona Analitik", "Frenia Sigorta", "Brenza Perakende", "Tesvora Mobilite", "Quarel Medya",
    "Elvanta Eğitim", "Rodena Seyahat", "Kovira Bulut",
]
TITLES = [
    "Backend Developer", "Senior Backend Developer", ".NET Developer", "Backend Engineer (C#)",
    "Software Engineer", "Senior .NET Developer", "Platform Engineer", "Backend Engineer",
]
HEADERS = ["Application Date", "Contact Email", "Contact Phone Number", "Company Name", "Job Title",
           "Job Url", "Resume Name", "Question And Answers"]
COUNT = 40
FIRST_DAY = datetime(2026, 7, 6, 9, 0)
SPAN_DAYS = 80

here = os.path.dirname(os.path.abspath(__file__))
rng = random.Random(20260930)
rows = []
for i in range(COUNT):
    company = COMPANIES[i % len(COMPANIES)]
    title = TITLES[rng.randrange(len(TITLES))]
    day = FIRST_DAY + timedelta(days=round(i * SPAN_DAYS / COUNT) + rng.randrange(2),
                                hours=rng.randrange(9), minutes=rng.randrange(60))
    rows.append((day, company, title))
rows.sort(key=lambda r: r[0], reverse=True)  # LinkedIn lists the newest first

out = io.StringIO()
writer = csv.writer(out, lineterminator="\n")
writer.writerow(HEADERS)
for day, company, title in rows:
    stamp = f"{day.month}/{day.day}/{day:%y}, {day.hour % 12 or 12}:{day:%M} {'AM' if day.hour < 12 else 'PM'}"
    writer.writerow([stamp, "", "", company, title, "", "Deniz_Aydin_CV.pdf", ""])

target = os.path.join(here, "Basic_LinkedInDataExport_09-29-2026.zip")  # the name LinkedIn gives it
with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as archive:
    info = zipfile.ZipInfo("Jobs/Job Applications.csv", date_time=(2026, 9, 29, 10, 0, 0))
    info.compress_type = zipfile.ZIP_DEFLATED
    archive.writestr(info, out.getvalue().encode("utf-8"))
print(f"{target}: {len(rows)} applications, {len(COMPANIES)} companies")
