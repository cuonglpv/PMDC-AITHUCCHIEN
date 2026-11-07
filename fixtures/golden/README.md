# Golden scenarios

The seven golden scenarios are the Definition of Done for every test suite: clean, blurry, missing document,
duplicate, conflicting, excluded line and limit-capped ([08](../../docs/08-invest-requirements.md),
[10 §10](../../docs/10-code-structure.md#10-tests)). Each scenario folder holds `documents/` (synthetic files, as
uploaded), `ai-responses/` (recorded Document AI answers, from the extraction slice, US04/US05) and `expected.json`.

Everything here is synthetic. The data is the golden case of [06 §9](../../docs/06-data-model.md#9-the-golden-case-as-rows)
and the seeded member in `db/seed/dev/040_members_and_policies.sql`. Every document carries the line
"Tài liệu tổng hợp cho kiểm thử ClaimLens, không có giá trị pháp lý." The street "phố Mẫu Thử" and the phone number
024 0000 0000 do not exist.

## What exists

| Path | What it is | Made by |
|---|---|---|
| `rule-set-gold-op-v3.json` | The `GOLD-OP` v3 rule set the Rule Engine tests load | Hand |
| `excluded-line/expected.json`, `limit-capped/expected.json` | Rule Engine expectations (US11, US12) | Hand |
| `clean/documents/` | The five documents of the golden claim, below | `tools/golden/generate.mjs` |
| `blurry/documents/receipt-blurry.jpg` | The clean receipt, upright, blurred with CSS `blur(3px)`, no EXIF | `tools/golden/generate.mjs` |
| `invalid/not-a-docx.docx` | Plain UTF-8 text with a `.docx` name | `tools/golden/generate.mjs` |
| `clean/ai-responses/pages.json`, `blurry/ai-responses/pages.json` | Recorded Document AI answers for each golden page, replayed by the fake gateway | Hand, page hashes from `document_ai.golden.record` |

`missing-document`, `duplicate` and `conflicting` have no files yet, and no scenario has a full `expected.json` yet. `invalid/` is not one of the seven scenarios: it holds files that must be refused at upload.

## The clean documents

Invoice `HN-0045821` from Phòng khám Đa khoa Hồng Ngọc (tax code `0101234567`) to Nguyễn Văn An (`MB-0007731`,
policy `PL-2026-00731`), service date 12/10/2026, diagnosis `J02.9`, five lines adding up to 1,400,000 VND:

| # | Line, as printed | Quantity × price | Amount |
|---|---|---|---|
| 1 | Khám nội tổng quát | 1 × 300.000 | 300.000 |
| 2 | Xét nghiệm công thức máu | 1 × 250.000 | 250.000 |
| 3 | Amoxicillin 500mg × 20 viên | 20 × 7.500 | 150.000 |
| 4 | Kem dưỡng phục hồi da Obagi | 1 × 300.000 | 300.000 |
| 5 | Siêu âm ổ bụng | 1 × 400.000 | 400.000 |

Amounts on the documents use the Vietnamese thousands separator (`1.400.000`); the XLSX keeps plain numbers.

| File | Pages | What it is | Used by |
|---|---|---|---|
| `invoice.pdf` | 2 | VAT invoice. Page 1: seller, buyer, member code, policy number and the five lines. Page 2: totals (KCT, no VAT), amount in words, e-signature box, lookup code | US03 upload of a multi-page PDF (one thumbnail per page); US05 invoice number, tax code, lines and total; `INVOICE_ARITHMETIC` resolved |
| `receipt.jpg` | 1 | Cash receipt (phiếu thu) for the invoice, photographed on a desk. Stored 1080×760, turned 90° counter-clockwise, with EXIF Orientation 6 (rotate 90° clockwise to view), so it is 760×1080 once rotated | US03 JPG upload and the EXIF rotation before page images are written ([10 §4, Background work](../../docs/10-code-structure.md#background-work)); a page image left unrotated is landscape |
| `prescription.docx` | 1 | Prescription (đơn thuốc) for Amoxicillin 500mg, 20 tablets, dated **13/10/2026** | US03 DOCX upload and PDF rendition; US05 medications and diagnosis; the `DATE_CONSISTENCY` warning (prescription 13/10/2026, invoice 12/10/2026) |
| `itemised-bill.xlsx` | 1 | Itemised bill (bảng kê chi phí) of the same five lines; line amounts and the total are formulas with cached values | US03 XLSX upload and PDF rendition (one worksheet page) |
| `visit-summary.pdf` | 1 | Outpatient visit summary: complaint, examination, tests done, diagnosis `J02.9`, treatment. It gives no reason for the abdominal ultrasound | US05 diagnosis, national ID, date of birth; the `SERVICE_DIAGNOSIS` risk flag on line 5 (US09) |

The packet is 5 files and 6 pages, well under the US03 limits of 20 MB a file and 30 pages a claim. [06 §9](../../docs/06-data-model.md#9-the-golden-case-as-rows)
still counts 4 files and 5 pages: the itemised bill was added so the clean claim also covers XLSX.

Fields for US05 and where they are printed: insured name on all five; address on all but the itemised bill; national ID and date of birth on the
prescription and the visit summary; member code and policy number on the invoice, prescription, itemised bill and
visit summary; provider and tax code on every letterhead; service date on the invoice, receipt, bill and visit
summary; invoice number on the invoice, receipt and bill; lines, quantities, unit prices and amounts on the invoice
and bill; total on the invoice, receipt and bill; diagnosis on the prescription, bill and visit summary; medication on
the prescription and visit summary.

## The other files

- `blurry/documents/receipt-blurry.jpg`: for Slice 2, US04 "Low quality" (quality score below the threshold). It is
  upright and has no EXIF, so only the blur differs from the clean receipt.
- `invalid/not-a-docx.docx`: for US03 "any other type is rejected with the reason". The Claims API must refuse it by its
  first bytes (not `PK\x03\x04`). Gotenberg does not refuse it: on 07/11/2026 `gotenberg/gotenberg:8.39.0` turned it
  into a one-page PDF with HTTP 200, so a check that waits for the conversion to fail would let it through.

## Recorded AI answers (golden replay)

No AI provider is chosen yet, so the Document AI Service answers through a fake gateway. Its usual answer is the
careful one: every value is left for a person. With `DOCUMENT_AI_REPLAY_DIR` set (the full stack in
`infra/compose.yaml` mounts this folder at `/golden`), a page whose image is a golden page gets the answer recorded in
`<scenario>/ai-responses/pages.json` instead, so the stack and the e2e tests show real-looking AI results.

A request carries only ids and image keys, so the service knows a page by its image alone:

- **Hash**: a 64-bit difference hash (dHash) of the page in grey, shrunk to 9 × 8 pixels. A recording within 6 bits
  matches.
- **Sharpness**: the root mean square of the differences between neighbouring pixels, on the page scaled to 600 pixels
  wide. A blurred capture of a page has nearly the same hash as the sharp one (the clean and blurry receipts are 2 bits
  apart), so a recording also has to be within a factor of 1.5 in sharpness. The two receipts are 6 times apart.
- No image can match two recordings: every pair is more than 12 bits or more than 1.5² in sharpness apart
  (`tests/test_golden_replay.py`). The closest two documents are 15 bits apart.

`pages.json` holds one entry per page:

| Key | What it is |
|---|---|
| `document`, `pageNo` | The file name as uploaded and the page inside the file |
| `pageHash`, `pageSharpness` | The page's fingerprint, as `document_ai.golden.record` prints it |
| `pageCheck` | `pageType`, `typeConfidence`, `qualityScore` for `PAGE_CHECK` |
| `fields` | `fieldKey`, `itemNo` (0 for the header, n for line or item n), `value` as printed, `confidence`, `bbox` (0–1, relative to the page image) for `FIELD_EXTRACTION`. Only the fields in the request's catalogue come back |

Values are written as printed in Vietnamese documents: money with `.` between thousands (`1.400.000`), dates as
`dd/MM/yyyy` (`12/10/2026`). The Claims API parses them. Boxes were measured on the page images the pipeline drew.

What the recordings say, following the golden case (BR-03 thresholds: page type 0.80; 0.95 for `insured_name`,
`national_id`, `policy_number`, `invoice_total`, `line_amount`; 0.90 for the rest; below 0.70 left empty):

| Page | Type | Fields |
|---|---|---|
| `clean/invoice.pdf` p1 | INVOICE 0.98 | Insured name, provider, tax code `0101234567`, invoice `HN-0045821`, service date, and the five lines (description, quantity, unit price, amount 300.000 · 250.000 · 150.000 · 300.000 · 400.000), all at or above their threshold |
| `clean/invoice.pdf` p2 | INVOICE 0.93 | Total `1.400.000` at 0.98 |
| `clean/receipt.jpg` | RECEIPT 0.96 | Total, provider, invoice number, service date |
| `clean/prescription.docx` | PRESCRIPTION 0.95 | Medication 1 `Amoxicillin 500mg`, insured name, date `13/10/2026`; diagnosis `J02.9` at 0.58 |
| `clean/itemised-bill.xlsx` | OTHER 0.88 | None |
| `clean/visit-summary.pdf` | VISIT_SUMMARY 0.94 | Insured name, service date, national ID, policy number; diagnosis `J02.9` at 0.62 |
| `blurry/receipt-blurry.jpg` | RECEIPT 0.64 (Unclassified) | Quality 0.31 (Low quality); the receipt's four fields at 0.48–0.61 |

So the clean claim ends in `NEEDS_VERIFICATION` with only the visit summary's diagnosis to enter.

### Re-recording the hashes

Record again whenever the documents or the page pipeline change (PDFium, LibreOffice, the 150 DPI, the PNG encoding):
the hashes must come from pages the Claims API drew, never from the HTML.

1. Start the stack: `docker compose -f infra/compose.yaml --profile full up -d --build`.
2. At `http://localhost:8088`, sign in as `intake@claimlens.test` with `SEED_DEV_PASSWORD` from `infra/.env.example`,
   create a claim for Nguyễn Văn An (search `PL-2026-00731`) and upload `clean/documents/*` and
   `blurry/documents/*`. Wait until every file is Ready. The claim id is in the address bar.
3. Get an access token (it lasts 15 minutes) and run the recorder from `apps/document-ai`. It lists the claim's
   documents through the Claims API and reads each page from storage with the Document AI Service's own read-only
   identity, which may read `pages/` but not list it:

   ```bash
   export CLAIMLENS_TOKEN=$(curl -s http://localhost:8088/api/v1/auth/login -H 'Content-Type: application/json'      -d '{"email":"intake@claimlens.test","password":"<SEED_DEV_PASSWORD>"}' | python -c "import json,sys; print(json.load(sys.stdin)['accessToken'])")
   DOCUMENT_AI_STORAGE_ACCESS_KEY=local-document-ai DOCUMENT_AI_STORAGE_SECRET_KEY=local-document-ai-secret    DOCUMENT_AI_REPLAY_DIR=../../fixtures/golden uv run python -m document_ai.golden.record --claim <claimId>
   ```

   It prints one line per page with `document`, `pageNo`, `pageHash`, `pageSharpness` and, with
   `DOCUMENT_AI_REPLAY_DIR` set, the recording it matches now (`"clean/invoice.pdf p1, 0 bits"`, or `null`).
4. Copy `pageHash` and `pageSharpness` into the entry with the same `document` and `pageNo`, run the recorder again
   until every page matches its own entry, then `uv run pytest` (it checks that no image can match two recordings).
5. `docker compose -f infra/compose.yaml --profile full down`.

Recorded on 08/11/2026 from claims `01a1b668-0779-7568-8def-457eba8e6e7f` and `01a1b738-dac0-7d5e-89d5-7b7762565f12`
(the second a fresh upload, which gave the same hashes) with `claimlens/claims-api:local` built from `112be68`
(PDFium through PDFtoImage, `gotenberg/gotenberg:8.39.0`).

## Regenerating

```bash
npx playwright install chromium
npm run golden:generate
```

The script renders HTML with the Chromium of `@playwright/test` (PDF with `page.pdf`, JPG with a screenshot) and builds
the DOCX and XLSX itself as ZIP archives of stored entries with the smallest OOXML parts: `[Content_Types].xml`,
`_rels/.rels` and `word/document.xml` for the DOCX; the workbook, its relationships and one sheet with inline strings
for the XLSX. It writes the receipt's EXIF APP1 segment itself (IFD0 with Orientation, the resolution tags,
YCbCrPositioning, Software and DateTime) in place of Chromium's JFIF segment. No dependency is added.

Every date inside the files is fixed: the PDF creation and modification dates, the ZIP entry dates and the EXIF date.
Two runs on the same machine give byte-identical files; `node tools/golden/generate.mjs --out <dir>` writes the tree
elsewhere so you can compare. Text is drawn with the machine's fonts (Arial on Windows, Liberation Sans or DejaVu Sans on
Linux), so another operating system or Chromium build gives other bytes. Commit regenerated files only when the
content was meant to change; tests that replay AI answers by page-image hash depend on these bytes.

The committed files were made on Windows 11 with Chromium 156 (Playwright 1.64.1). Checks done then:

- two runs gave the same SHA-256 for every file;
- pypdf opens both PDFs in strict mode: `invoice.pdf` has 2 pages, `visit-summary.pdf` 1;
- Gotenberg 8.39.0 (`/forms/libreoffice/convert`) turned `prescription.docx` and `itemised-bill.xlsx` into 1-page PDFs
  whose text, read with `pdftotext`, keeps every diacritic (`Nguyễn Văn An`, `Phòng khám Đa khoa Hồng Ngọc`, `Đỗ Thị Mai`);
- exiftool reads Orientation 6 ("Rotate 90 CW") from `receipt.jpg` and `-validate` reports OK; Pillow's
  `exif_transpose` turns 1080×760 into an upright 760×1080.
