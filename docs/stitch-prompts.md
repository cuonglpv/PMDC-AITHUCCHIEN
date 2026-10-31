# Stitch prompts — ClaimLens

> Nguồn: [DESIGN.md](DESIGN.md) · [05-ui-brief-stitch.md](05-ui-brief-stitch.md) · [04-information-architecture.md](04-information-architecture.md) · [08-invest-requirements.md](08-invest-requirements.md)
>
> Prompt viết bằng tiếng Anh vì giao diện là English. Hướng dẫn trong file viết bằng tiếng Việt.

## Cách dùng (cách a: bạn tự dán prompt)

1. Mở **stitch.withgoogle.com**, đăng nhập bằng tài khoản Google, tạo project mới và chọn **Web**.
2. **Nạp [DESIGN.md](DESIGN.md) vào project trước khi sinh bất kỳ màn nào.** Mở bảng *Design system / Theme* của project, chọn tab **DESIGN.md**, bấm biểu tượng bút chì (edit), xoá nội dung có sẵn và dán **toàn bộ** file (kể cả phần YAML giữa hai dòng `---`), rồi lưu.
   - Nếu không nạp, Stitch sẽ tự sinh một DESIGN.md riêng từ prompt đầu tiên, và mọi màn sau đều theo bản tự sinh đó. Đây là lý do SCR-31 v1 bị lệch.
   - Nếu project đã có màn được sinh trước, thay DESIGN.md xong thì sinh lại màn đó.
   - Tên nút trong Stitch có thể hơi khác. Nếu không tìm thấy, chụp màn hình gửi mình.
3. Dán **Prompt 0 (app-level)**. Bước này tuỳ chọn khi đã nạp DESIGN.md, nhưng vẫn giúp Stitch hiểu sản phẩm.
4. Dán lần lượt prompt từng màn theo thứ tự bên dưới, **mỗi lần một màn**. Mỗi prompt đã có khối `DESIGN SYSTEM (REQUIRED)` nên dán độc lập được.
5. Nếu màn sinh ra lệch, dùng **prompt chỉnh sửa** ở cuối mỗi màn: mỗi lần chỉ 1–2 thay đổi.
6. Gửi lại cho mình **ảnh chụp** (hoặc file HTML export) của từng màn. Mình review theo checklist cuối file rồi đưa prompt chỉnh sửa tiếp.

Thứ tự khuyến nghị: `SCR-20` (chốt shell: sidebar + top bar) → `SCR-31` → `SCR-33` → `SCR-35` → `SCR-13` → `SCR-50` → `SCR-01`.

## Sửa các màn đã sinh trước khi nạp DESIGN.md

Theo tài liệu Stitch, DESIGN.md được áp dụng khi màn được sinh hoặc chỉnh sửa. Các màn đã có **không tự đổi theo**, nên phải sửa từng màn.

| Màn đã có | Nên làm | Lý do |
|---|---|---|
| Màn sinh từ Prompt 0 | Xoá, hoặc bỏ qua | Prompt 0 chỉ để đặt phong cách, không phải một màn trong IA |
| `SCR-20` Review queue | **Thử sửa trước** bằng U1 rồi U2 (bên dưới). Nếu vẫn rối thì sinh lại bằng **SCR-20 v2** | Bố cục bảng thường đã ổn, chủ yếu sai font và thừa chữ |
| `SCR-31` Evidence | **Sinh lại** bằng **SCR-31 v2** | Bản v1 lệch quá xa: nhồi nội dung 3 tab, số liệu bịa thêm |

Các bước:
1. Nạp DESIGN.md vào project (bước 2 ở trên).
2. Mở từng màn cũ, dán **U1**, chờ Stitch sửa xong, rồi dán **U2**. Mỗi lần chỉ một prompt.
3. Chụp màn hình gửi mình. Nếu vẫn chưa ổn, mình đưa prompt R-x cụ thể cho màn đó.

**Prompt sửa dùng chung cho màn cũ:**
- **U1 (áp lại design system)** `Restyle this screen with the project's DESIGN.md: Be Vietnam Pro sentence case for all labels, names, buttons and tabs; IBM Plex Mono only for money, codes and dates; only one red (#EB0029) filled button. Keep the current layout.`
- **U2 (bớt chữ)** `Declutter this screen: remove every metric, ID, chip, keyboard hint or panel that is not needed for this screen's single job; keep labels to 4 words or fewer and add more white space between groups.`
- **U3 (dữ liệu, nếu sai)** `Replace the sample data with the golden case: CLM-2026-000184, Nguyễn Văn An, Gold Outpatient, service 12/10/2026, claimed 1,400,000 ₫, payable 800,000 ₫.`

## Dữ liệu mẫu dùng chung (golden case)

Giữ đúng các số này ở mọi màn để các màn khớp nhau.

| Mục | Giá trị |
|---|---|
| Claim | `CLM-2026-000184` |
| Member | Nguyễn Văn An · member ID `MB-0007731` |
| Policy / plan | `GOLD-OP` Gold Outpatient · policy no. `PL-2026-00731` |
| Provider | Phòng khám Đa khoa Hồng Ngọc |
| Service date | 12/10/2026 |
| Diagnosis | J02.9 Acute pharyngitis (Viêm họng cấp) |
| Rule set | v3 · effective 01/01/2026 |
| Reviewer / Approver | Trần Thu Hà (TH) / Lê Minh Quân (LQ) |
| Remaining balance before claim | 800,000 ₫ (annual limit 5,000,000 ₫, used 4,200,000 ₫; con số hạn mức năm là giả định) |

Expense lines (tổng 1,400,000 ₫):

| # | Line (as printed) | AI category | Claimed | Line result | Rule |
|---|---|---|---|---|---|
| 1 | Khám nội tổng quát | Consultation | 300,000 | Covered | `COV-01` |
| 2 | Xét nghiệm công thức máu | Laboratory tests | 250,000 | Covered | `COV-03` |
| 3 | Amoxicillin 500mg × 20 viên | Prescription drugs | 150,000 | Covered | `COV-05` |
| 4 | Kem dưỡng phục hồi da Obagi | Cosmetic products | 300,000 | Excluded | `EX-04` |
| 5 | Siêu âm ổ bụng | Diagnostic imaging | 400,000 | Covered | `COV-04` |

Formula: eligible 1,400,000 − 300,000 = **1,100,000** → coinsurance 80% (`COINS-80`) = **880,000** → capped by remaining balance (`LIM-ANNUAL`) → **payable 800,000 ₫**. Remaining after claim: **0 ₫**.

Risk signal (1): "Abdominal ultrasound may not match diagnosis J02.9", AI, Medium.
Validation (2): ✓ Invoice total equals sum of lines (resolved) · ⚠ Prescription date 13/10/2026 differs from invoice date 12/10/2026 (Warning).

---

## Prompt 0 — App-level

```text
ClaimLens is a desktop web workbench where health insurance claims staff verify AI-extracted evidence from outpatient claim documents, read deterministic rule-engine payment results, and record accountable two-person decisions. Vibe: precise, accountable, editorial, high-contrast, calm — a Swiss editorial ledger, not a consumer app; dense and keyboard-first like Linear.

**DESIGN SYSTEM (REQUIRED):**
- RESTRAINT (most important): quiet, uncluttered, few words, generous white space. Show ONLY the elements listed in Page Structure; do not invent extra metrics, IDs, chips, keyboard hints, panels or content from other tabs. Be Vietnam Pro sentence case for labels, names and sentences; IBM Plex Mono only for money, codes and dates; at most one small uppercase kicker per panel; text never wraps inside buttons or tabs.
- Platform: Web, desktop-first, 1440px wide
- Theme: Light working canvas inside a near-black application shell
- Background: Warm Paper (#F4F3EF); panels and tables Pure White (#FFFFFF); hairline dividers (#D8D6D1); control borders (#8C8984)
- Text: Ink Black (#090909) primary, Muted Graphite (#5C5A57) secondary
- Shell: left sidebar Ink Black (#090909), text #ECE7E4 / #A9A6A1, group labels Shell Pink (#FF7B91) mono uppercase, active item #1D1C1C with a 2px Signal Red left bar
- Brand signal: Signal Red (#EB0029) ONLY for the single primary button per screen, active nav bar and focus ring
- Negative outcomes: Decline Crimson (#A40022) on #FBEAEC, always with an icon — never Signal Red
- Semantic: green #11703C on #E7F4EC, amber #8A4B00 on #FDF3DE, blue #1E4FB5 on #E9EFFB
- AI content ONLY: Insight Violet (#6D28D9) / Deep Violet (#5B21B6) on Violet Wash (#F3EEFD), sparkle icon + "AI" label
- Risk signals: Signal Rust (#9A3412) on #FDEDE4, flag icon
- Fonts: Lexend Exa for short UPPERCASE headings and KPI numbers; Be Vietnam Pro for all UI text (sentence case); IBM Plex Mono for money, codes, dates and small UPPERCASE kickers like "01 / EVIDENCE"
- Shapes: 2px radius on buttons, inputs, cards; 0px on drawers, modals, banners; full pill ONLY for claim status pills
- Depth: flat, 1px hairlines, no shadows except drawers/modals
- Icons: 1.5px line icons (Lucide style); no emoji, no gradients, no illustrations or photos
- Money: integer VND with commas and "₫" (800,000 ₫), right-aligned, mono; dates dd/MM/yyyy
- Every status, confidence and severity = icon + text, never colour alone (WCAG AA)

**Page Structure:**
1. Global shell: 240px black left sidebar with "ClaimLens" wordmark (small magnifying-lens mark + wordmark), groups WORK / POLICY / INSIGHTS, line icons + labels; 56px white top bar with global search "Search claim no., member or invoice no.", notification bell, avatar.
2. Content area on Warm Paper with a page title in Lexend Exa uppercase and a mono kicker above it.
3. Key screens of the product: Review queue (data table of claims), Claim workspace (case header, tabs, three columns: document rail, working panel, document viewer, sticky action bar), Upload with AI page check, Operations dashboard, Sign in.
4. Sample data is Vietnamese (names with diacritics, e.g. Nguyễn Văn An, Phòng khám Đa khoa Hồng Ngọc); UI labels are English.
```

---

## SCR-20 — Review queue

> Đã nạp DESIGN.md thì dùng **SCR-20 v2** (bản gọn) ở cuối mục này. Bản bên dưới là bản đầu, chi tiết hơn và dễ bị rối.

```text
Review queue for a Claims Reviewer: a dense, prioritised data table of outpatient claims assigned to them, so they always open the most urgent claim next. Vibe: precise, calm, keyboard-first.

**DESIGN SYSTEM (REQUIRED):**
- RESTRAINT (most important): quiet, uncluttered, few words, generous white space. Show ONLY the elements listed in Page Structure; do not invent extra metrics, IDs, chips, keyboard hints, panels or content from other tabs. Be Vietnam Pro sentence case for labels, names and sentences; IBM Plex Mono only for money, codes and dates; at most one small uppercase kicker per panel; text never wraps inside buttons or tabs.
- Platform: Web, desktop 1440px; light canvas Warm Paper (#F4F3EF), white table (#FFFFFF), hairlines #D8D6D1
- Shell: black sidebar #090909 (active item #1D1C1C + 2px #EB0029 left bar), white 56px top bar
- Text Ink #090909 / Muted #5C5A57; Fonts: Lexend Exa (uppercase titles), Be Vietnam Pro (UI), IBM Plex Mono (money, codes, dates, kickers)
- Signal Red #EB0029 only for the one primary button; status pills full-pill with icon + text: info blue #1E4FB5/#E9EFFB, amber #8A4B00/#FDF3DE, ink #090909/#ECE7E4, green #11703C/#E7F4EC, crimson #A40022/#FBEAEC
- AI items violet #5B21B6 on #F3EEFD; risk signals rust #9A3412 on #FDEDE4
- 2px radius, flat, 36px compact rows, line icons, no gradients/emoji

**Page Structure:**
1. Sidebar: ClaimLens wordmark; WORK group with "Review queue" active (badge 12), "Search claims"; POLICY group with "Benefit plans"; user block at bottom "Trần Thu Hà · Claims Reviewer".
2. Top bar: global search field, bell with dot, avatar "TH".
3. Page header: mono kicker "WORK / 02" in #C8001F; title "REVIEW QUEUE" in Lexend Exa; right side secondary button "Filters" and primary red button "Open next claim  N".
4. Tabs with mono count badges: Assigned to me 12 (active, 2px ink underline) · Needs verification 5 · Needs information 3 · Returned 1.
5. Filter row: chips "Priority: All", "Plan: All", "Service date: Last 30 days", search-in-table input "Filter by claim no. or member"; right side text "12 claims · sorted by priority".
6. Data table (white, mono uppercase headers on paper): Priority (icon + "High/Medium/Low") · Claim no. (mono) · Member · Plan · Service date · Claimed (right-aligned mono) · Status pill · To verify (count with violet AI sparkle) · Alerts (crimson blocking / amber warning counts with icons) · Signals (rust flag count) · Age (e.g. "2d 4h") · Assignee initials.
   Rows:
   - High · CLM-2026-000184 · Nguyễn Văn An · GOLD-OP · 12/10/2026 · 1,400,000 ₫ · In review · 0 · 1 warning · 1 signal · 1d 6h · TH  (selected row: #ECEAE4 with 2px red left bar)
   - High · CLM-2026-000191 · Phạm Thị Bích Ngọc · GOLD-OP · 14/10/2026 · 3,250,000 ₫ · Needs verification · 3 · 1 blocking · 0 · 2d 1h · TH
   - High · CLM-2026-000177 · Đỗ Quang Huy · SILVER-OP · 09/10/2026 · 2,180,000 ₫ · Returned · 0 · 0 · 1 signal · 3d 2h · TH
   - Medium · CLM-2026-000195 · Lê Thị Mai · GOLD-OP · 15/10/2026 · 640,000 ₫ · Ready for review · 0 · 0 · 0 · 6h · TH
   - Medium · CLM-2026-000188 · Vũ Đức Thắng · BASIC-OP · 13/10/2026 · 920,000 ₫ · Needs verification · 2 · 0 · 0 · 1d 2h · TH
   - Medium · CLM-2026-000172 · Hoàng Thu Trang · GOLD-OP · 07/10/2026 · 1,760,000 ₫ · Needs information · 0 · 1 warning · 0 · 4d · TH
   - Low · CLM-2026-000199 · Ngô Bảo Châu · SILVER-OP · 16/10/2026 · 350,000 ₫ · Ready for review · 0 · 0 · 0 · 2h · TH
   - Low · CLM-2026-000201 · Bùi Minh Khoa · BASIC-OP · 16/10/2026 · 480,000 ₫ · Processing · — · — · — · 25m · TH
7. Table footer: pagination "1–8 of 12", rows per page; keyboard hint in mono "↑↓ move · Enter open · N next claim".
```

**Prompt chỉnh sửa (dùng khi cần):**
- `Make the table rows exactly 36px tall and reduce cell padding to 12px; keep all 12 columns visible at 1440px.`
- `Status pills must be full pills with a line icon before the text; do not use colour alone. "Pending approval" uses an ink black pill with light text.`
- `Remove any shadows on the table and cards; separate rows with 1px #D8D6D1 hairlines only.`

#### SCR-20 v2 (bản gọn, dùng nếu U1 + U2 chưa đủ)

```text
Review queue for a Claims Reviewer: a calm, prioritised list of claims to work on next. One job: pick the next claim. Keep it minimal — few columns, few words, generous white space. Vibe: calm, precise, uncluttered.

**DESIGN SYSTEM (REQUIRED):**
- RESTRAINT (most important): show ONLY the elements listed below. No extra metrics, IDs, chips, keyboard hints or panels. Labels of at most 4 words.
- Type: Be Vietnam Pro sentence case for labels, names and buttons; IBM Plex Mono ONLY for money, claim numbers and dates; Lexend Exa only for the page title.
- Platform: Web, desktop 1440px. Canvas Warm Paper #F4F3EF, white table, 1px hairlines #D8D6D1
- Shell: 240px black sidebar #090909 (active item #1D1C1C + 2px #EB0029 left bar); white 56px top bar with only search, bell, avatar
- Signal Red #EB0029 for ONE primary button only
- Status pills: full pill, icon + text (blue #1E4FB5 on #E9EFFB, amber #8A4B00 on #FDF3DE, crimson #A40022 on #FBEAEC)
- 2px radius, flat, line icons, no gradients, no emoji

**Page Structure:**
1. Sidebar: "ClaimLens" wordmark; Review queue (active, count 12), Search claims, Benefit plans; user "Trần Thu Hà · Claims Reviewer" at the bottom.
2. Header: title "REVIEW QUEUE"; right: red primary button "Open next claim".
3. Tabs: Assigned to me 12 (active) · Needs verification 5 · Needs information 3 · Returned 1.
4. One filter row: search input "Claim no. or member" and a "Filters" button. Nothing else.
5. Table, 7 columns, 44px rows: Priority (icon + word) · Claim no. · Member · Service date · Claimed (right-aligned) · Status pill · Needs attention (small icons with counts: violet sparkle = fields to verify, crimson = blocking alert, rust flag = signal; empty if none).
   - High · CLM-2026-000184 · Nguyễn Văn An · 12/10/2026 · 1,400,000 ₫ · In review · rust flag 1
   - High · CLM-2026-000191 · Phạm Thị Bích Ngọc · 14/10/2026 · 3,250,000 ₫ · Needs verification · sparkle 3, crimson 1
   - High · CLM-2026-000177 · Đỗ Quang Huy · 09/10/2026 · 2,180,000 ₫ · Returned · rust flag 1
   - Medium · CLM-2026-000195 · Lê Thị Mai · 15/10/2026 · 640,000 ₫ · Ready for review · —
   - Medium · CLM-2026-000188 · Vũ Đức Thắng · 13/10/2026 · 920,000 ₫ · Needs verification · sparkle 2
   - Medium · CLM-2026-000172 · Hoàng Thu Trang · 07/10/2026 · 1,760,000 ₫ · Needs information · —
6. Footer: "1–6 of 12" and page arrows.
```

---

## SCR-31 — Claim workspace · Evidence

> Dùng **SCR-31 v2** (bản gọn) ở cuối mục này. Bản bên dưới là bản đầu, chính là bản đã cho ra màn v1 bị rối.

```text
Claim workspace, Evidence tab: a Claims Reviewer verifies AI-extracted fields side by side with the highlighted source region on the scanned invoice, lowest confidence first. Vibe: precise, accountable, calm, information-dense.

**DESIGN SYSTEM (REQUIRED):**
- RESTRAINT (most important): quiet, uncluttered, few words, generous white space. Show ONLY the elements listed in Page Structure; do not invent extra metrics, IDs, chips, keyboard hints, panels or content from other tabs. Be Vietnam Pro sentence case for labels, names and sentences; IBM Plex Mono only for money, codes and dates; at most one small uppercase kicker per panel; text never wraps inside buttons or tabs.
- Platform: Web, desktop 1440px; canvas Warm Paper (#F4F3EF), panels white (#FFFFFF), hairlines #D8D6D1, document viewer background #E9E7E2
- Shell: collapsed 64px black sidebar #090909 (icons only), white 56px top bar
- Fonts: Lexend Exa (short uppercase titles), Be Vietnam Pro (UI text), IBM Plex Mono (money, codes, dates, kickers, confidence scores)
- Signal Red #EB0029 only for the single primary CTA and the active indicator
- AI content: Insight Violet #6D28D9 / Deep Violet #5B21B6 on #F3EEFD with sparkle icon + "AI" label; source region = 2px #6D28D9 outline with 12% violet fill, square corners
- Confidence badges (icon + text): High = green #11703C on #E7F4EC check-circle; Check = amber #8A4B00 on #FDF3DE eye icon; Missing = crimson #A40022 on #FBEAEC dashed-circle icon
- 2px radius, flat, 1px hairlines, compact 36px rows, line icons, no gradients/emoji

**Page Structure:**
1. Case header (white, full width): left "CLM-2026-000184" in mono, "Nguyễn Văn An" (title, 16px semibold), meta line "GOLD-OP Gold Outpatient · Phòng khám Đa khoa Hồng Ngọc · Service 12/10/2026"; right: amber pill "Needs verification" with eye icon, assignee chip "TH", mono "Rule set v3 · eff. 01/01/2026", payable "— ₫" placeholder in mono 24px with caption "Pending verification", primary red button "Verify next field  N".
2. Tabs: Overview · Evidence (active, 2px ink underline) · Validation 2 · Adjudication · Risk 1 · Decision · Timeline.
3. Three columns under the tabs:
   a. Zone A — Document rail (200px, white, hairline right border): mono kicker "01 / DOCUMENTS 5 pages"; groups with small page thumbnails: "Invoice · 2 pages" (AI done, check icon), "Receipt · 1 page" (amber "Low quality" tag with camera icon), "Prescription · 1 page", "Visit summary · 1 page" (violet "Reading…" status with small progress). Selected thumbnail "Invoice p1" has a 2px ink border.
   b. Zone B — Working panel (fluid): mono kicker "02 / EXTRACTED FIELDS", title "INVOICE · HN-0045821", counter "3 fields to verify · 5 accepted" with a thin progress bar; toggle "Lowest confidence first". Field list rows (label, value, confidence badge, source pin icon, actions Accept (A) / Edit (E) / Not present):
      - Diagnosis — empty input "Needs entry" — Missing (0.54)
      - Line 4 description — "Kem dưỡng phục hồi da Obagi" with violet AI marker — Check (0.78) — SELECTED row (#ECEAE4, red left bar)
      - Total amount — "1,400,000 ₫" mono with AI marker — Check (0.91, money threshold 0.95)
      - Invoice no. — "HN-0045821" mono with AI marker — Check (0.88)
      - Insured name — "Nguyễn Văn An" — accepted, initials chip "TH" instead of AI marker — High
      - Policy no. — "PL-2026-00731" — High (accepted, TH)
      - Provider — "Phòng khám Đa khoa Hồng Ngọc" — High (accepted, TH)
      - Service date — "12/10/2026" — High (accepted, TH)
      Below the list: ghost button "Re-run AI on this page" with note "Verified fields are kept".
   c. Zone C — Context rail (480px): document viewer on #E9E7E2 showing a scanned Vietnamese clinic invoice "HÓA ĐƠN" page 1 of 2, toolbar with zoom − 100% +, rotate, page selector "p1 / 2"; the line "Kem dưỡng phục hồi da Obagi … 300,000" is enclosed by a square violet source-highlight box with a small mono tag "line_4.description · 0.78".
4. Sticky action bar (white, hairline top, 64px): left "3 fields to verify · 0 blocking alerts"; right secondary "Request information", secondary "Save draft", primary red "Accept & next  A".
```

**Prompt chỉnh sửa:**
- `Make the selected field row and its violet highlight box on the invoice clearly linked: same row stays highlighted while the box is visible on the page; add a small connector label on the box.`
- `Confidence badges must show icon + word (High / Check / Missing); show the raw score only as a tooltip-style mono number next to the badge.`
- `Show an AI fallback state variant: amber full-width banner under the tabs "AI assistance unavailable — fields require manual entry", all fields empty and editable, no confidence badges.`

### SCR-31: review bản v1 và cách làm gọn

Ảnh: [ui/scr-31-v1.webp](ui/scr-31-v1.webp). Cảm giác chung là **quá nhiều chữ**, mắt không biết nhìn vào đâu trước.

| Vấn đề | Vi phạm |
|---|---|
| Cột giữa nhồi thêm nội dung của các tab khác: bảng adjudication, thẻ "AI citation engine", "Dual verification", bảng tính chi trả | IA: mỗi tab chỉ làm một việc |
| Case header có khoảng 15 thông tin (DOB, citizen ID, channel, episode ID, corporate account…) cùng 4 ô số tiền và 2 chip | Thông tin bịa thêm, không có trong prompt |
| Nhãn, mô tả và nút đều dùng mono IN HOA, nên chữ bị xuống dòng liên tục ("Back / to / Queue") | DESIGN.md: mono chỉ dùng cho số và mã; IN HOA chỉ cho tiêu đề ngắn |
| Tên người "NGUYỄN VĂN AN" để IN HOA bằng font display | Don't: tên người không IN HOA |
| Có 2 nút đỏ ("Decline entire claim" và "Co-signature & approve"); reviewer lại có nút của approver | Mỗi màn chỉ một nút đỏ; chỉ approver mới có quyền quyết định |
| Tên tab khác IA; sidebar có thêm mục "Claim workspace" | IA › Sitemap |
| Số liệu khác golden case (3,450,000 ₫, E-14…) | Dữ liệu mẫu dùng chung |
| Cột hẹp nên số tiền bị cắt ("500,00…", "TARIFF CAP") | Layout |

**Cách nhanh nhất: sinh lại màn bằng prompt v2 bên dưới.** Prompt v2 ngắn hơn và chỉ liệt kê đúng những gì được hiện. Nếu muốn giữ bản v1, dùng chuỗi prompt chỉnh sửa R1–R7, dán lần lượt từng prompt một.

#### SCR-31 v2 (bản gọn)

```text
Claim workspace, Evidence tab. One job only: the reviewer checks AI-extracted fields against the highlighted spot on the scanned invoice. Keep it minimal and quiet — generous white space, few words, nothing extra. Vibe: calm, precise, uncluttered.

**DESIGN SYSTEM (REQUIRED):**
- RESTRAINT (most important): show ONLY the elements listed below. No extra metrics, IDs, chips, keyboard hints, signatures, totals or panels from other tabs. Labels of at most 4 words.
- Type: Be Vietnam Pro, sentence case, for labels, names and descriptions; IBM Plex Mono ONLY for money, codes and dates; at most one small uppercase kicker per column; person names never uppercase. Lexend Exa not used on this screen.
- Platform: Web, desktop 1440px. Canvas Warm Paper #F4F3EF, panels white #FFFFFF, 1px hairlines #D8D6D1, document viewer background #E9E7E2
- Shell: 64px black icon-only sidebar #090909; white 56px top bar with only a search field, a bell and an avatar
- Signal Red #EB0029 for ONE primary button only
- AI: violet #6D28D9 sparkle + "AI" on #F3EEFD; source box = 2px #6D28D9 outline, 12% violet fill, square corners
- Confidence (icon + word): High green #11703C, Check amber #8A4B00, Missing crimson #A40022
- 2px radius, flat, line icons, no gradients, no emoji

**Page Structure:**
1. Case header — one row, 64px tall: left "CLM-2026-000184" (mono, muted) · "Nguyễn Văn An" (16px semibold) · "Gold Outpatient · 12/10/2026" (muted). Right: amber pill "Needs verification". No buttons in the header. Nothing else.
2. Tabs, one row, no wrapping: Overview · Evidence (active) · Validation 2 · Adjudication · Risk 1 · Decision · Timeline.
3. Left column (200px) "Documents": four rows, each = small thumbnail + name + one status icon: Invoice (2 pages, selected) · Receipt (amber camera icon "Low quality") · Prescription · Visit summary.
4. Middle column "Invoice" with a small counter "3 to verify". Simple list, 48px rows, one line each: label (muted) · value · confidence badge. Accept / Edit buttons appear only on the selected row.
   - Diagnosis · empty input "Enter value" · Missing
   - Line 4 description · "Kem dưỡng phục hồi da Obagi" + AI mark · Check (selected row: #ECEAE4, 2px red left bar, Accept + Edit visible)
   - Total amount · 1,400,000 ₫ + AI mark · Check
   - Invoice no. · HN-0045821 + AI mark · Check
   - Collapsed row "4 accepted fields" with a chevron (closed)
5. Right column (480px): the scanned Vietnamese invoice page, with ONE violet box around the line "Kem dưỡng phục hồi da Obagi … 300,000". Controls: zoom − / +, page "1 / 2". Nothing else.
6. Bottom bar (56px, white): left "3 fields to verify"; right secondary "Request information" and red primary "Accept & next".
```

#### Chuỗi chỉnh sửa bản v1 (mỗi lần dán một dòng, theo thứ tự)

- **R1** `Simplify the middle column: remove the adjudication table, the AI citation engine card, the payable breakdown and the dual verification panel. The middle column shows only the list of extracted fields for the selected document.`
- **R2** `Simplify the case header to one row: claim number, member name, plan and service date on the left; status pill and one primary button "Verify next field" on the right. Remove the four amount tiles, DOB, citizen ID, channel, episode ID, corporate account, OCR confidence and stage chips.`
- **R3** `Typography: use Be Vietnam Pro sentence case for all labels, names and descriptions; keep IBM Plex Mono only for money, codes and dates; at most one small uppercase kicker per column. The member name is not uppercase.`
- **R4** `Left column: one row per document (thumbnail, name, one status icon). Remove the document descriptions, the engine extraction card and the extracted entities list.`
- **R5** `Bottom bar: keep only "3 fields to verify" on the left and two buttons on the right: "Request information" (secondary) and "Accept & next" (red primary). Remove keyboard chips, totals and the decline/approve buttons.`
- **R6** `Top bar: keep only the search field, notifications and avatar. Sidebar: remove the "Claim workspace" item; tabs must read Overview · Evidence · Validation · Adjudication · Risk · Decision · Timeline.`
- **R7** `Replace sample data: claim CLM-2026-000184, Nguyễn Văn An, Gold Outpatient, service 12/10/2026, invoice total 1,400,000 ₫, selected field line 4 "Kem dưỡng phục hồi da Obagi" 300,000 ₫ highlighted on the invoice.`

---

## SCR-33 — Claim workspace · Adjudication

```text
Claim workspace, Adjudication tab. One job: the reviewer reads the rule engine's result for each expense line and how the payable amount was computed. Keep it minimal — a clean ledger, few words, generous white space. Vibe: calm, precise, ledger-like.

**DESIGN SYSTEM (REQUIRED):**
- RESTRAINT (most important): show ONLY the elements listed below. No extra metrics, IDs, chips, keyboard hints, signatures or panels from other tabs. Labels of at most 4 words. Text never wraps inside buttons, tabs or table headers.
- Type: Be Vietnam Pro sentence case for labels, names and sentences; IBM Plex Mono ONLY for money, codes and dates; at most one small uppercase kicker per column; person names never uppercase.
- Platform: Web, desktop 1440px. Canvas Warm Paper #F4F3EF, panels white #FFFFFF, 1px hairlines #D8D6D1
- Shell: 64px black icon-only sidebar #090909; white 56px top bar with only search, bell, avatar
- Signal Red #EB0029 for ONE primary button only; negative results Decline Crimson #A40022 on #FBEAEC with icon
- AI content only: violet #6D28D9 sparkle + "AI" on #F3EEFD; source box = 2px #6D28D9 outline, square
- 2px radius, flat, line icons, no gradients, no emoji
- Rule engine results are NOT AI: plain ink and mono. Covered = green #11703C check icon; Excluded = crimson x icon.

**Page Structure:**
1. Case header — one row, 64px: left "CLM-2026-000184" (mono, muted) · "Nguyễn Văn An" (16px semibold) · "Gold Outpatient · 12/10/2026" (muted). Right: blue pill "In review" and payable "800,000 ₫" (mono, 20px). No buttons in the header. Nothing else.
2. Tabs, one row: Overview · Evidence · Validation 2 · Adjudication · Risk 1 · Decision · Timeline — "Adjudication" active (2px ink underline).
3. Left column (200px) "Documents": four rows, thumbnail + name + one status icon: Invoice · Receipt · Prescription · Visit summary.
4. Middle column, title "Expense lines" with a small muted "Revision #2" (nothing else on that line). Table, 4 columns that all fit, 52px rows: Line (name, with the benefit category as a small muted second line) · Claimed · Result · Covered. Amounts right-aligned, nothing cut off.
   - Khám nội tổng quát / Consultation · 300,000 · ✓ Covered · 300,000
   - Xét nghiệm công thức máu / Laboratory tests · 250,000 · ✓ Covered · 250,000
   - Amoxicillin 500mg × 20 viên / Prescription drugs · 150,000 · ✓ Covered · 150,000
   - Kem dưỡng phục hồi da Obagi / Cosmetic products + AI mark + "Confirm" link · 300,000 · ✕ Excluded + chip "EX-04" (crimson) · 0 — selected row (#ECEAE4, 2px red left bar)
   - Siêu âm ổ bụng / Diagnostic imaging · 400,000 · ✓ Covered · 400,000
5. Under the table, "Payable" as a short ledger (mono amounts right-aligned, hairline between rows):
   Claimed 1,400,000 ₫ · Excluded (EX-04) −300,000 ₫ · Eligible 1,100,000 ₫ · Coinsurance 80% 880,000 ₫ · Capped by remaining balance −80,000 ₫ · Payable 800,000 ₫ (bold, 2px ink rule above). All amounts in ink, no red. No title badge or caption.
6. Right column (480px), follows the selected line: chip "EX-04", title "Cosmetic products", one-sentence clause "Chi phí mỹ phẩm và sản phẩm dưỡng da không được chi trả.", a small crop of the invoice line with a violet box (no header above the crop). Below: "Remaining balance" bar (8px, square) with one caption "0 ₫ left after this claim (of 5,000,000 ₫)". Nothing else in this column.
7. Bottom bar (56px): left "1 category to confirm"; right secondary "Override line" and red primary "Continue to decision".
```

**Prompt chỉnh sửa:**
- `Make the payable ledger stricter: amounts in one right-aligned mono column, hairline between steps, final "Payable 800,000 ₫" bold with a 2px ink rule above.`
- `Show a recalculating state: the table and ledger dimmed to 50% with a small "Recalculating…" label next to "Revision #2"; no spinner over the numbers.`
- `Show line 3 as overridden: amber "Overridden" tag with initials "TH", new amount shown, original amount struck through but visible.`

### SCR-33: review bản v2

Ảnh: [ui/scr-33-v2.png](ui/scr-33-v2.png). Bản này đã khá hơn nhiều so với SCR-31 v1:
- Header 1 dòng, font và màu đúng DESIGN.md.
- Bảng tính chi trả rõ ràng.
- Khung tím bao đúng dòng hoá đơn; dấu AI kèm link "Confirm" đúng pattern.

Còn các lỗi sau:

| # | Vấn đề | Mức |
|---|---|---|
| 1 | **Bảng bị cắt mất cột Claimed / Result / Covered**: không thấy dòng 4 bị *Excluded*, mà đây là thông tin chính của màn. Nguyên nhân là prompt cũ yêu cầu 5 cột trong khoảng ~700 px. | Nặng |
| 2 | Cột phải có 7 khối: số mục chính sách, dòng "Matched to category…", tiêu đề vùng cắt kèm toạ độ, "Benefit limits", hộp thông tin, "Sign-off trail" | Thừa |
| 3 | Bảng tính tô đỏ các khoản trừ; có thêm "Policy T-01" và "Final approved disbursement" | Sai màu, thừa |
| 4 | Dòng "5 line items parsed · 1 flagged for manual check" cạnh Revision bị xuống dòng; thanh dưới có thêm một câu thứ hai | Thừa |
| 5 | Mã file dưới tên tài liệu, chân "Ingestion · OCR v4.2"; top bar có tên và chức danh "T. Harrison" (tên bịa) | Thừa, sai dữ liệu |
| 6 | Header có nút "Review checklist" do Stitch tự thêm | Thừa (prompt đã sửa: header không có nút) |

Đã sửa trong prompt SCR-33 ở trên: bảng còn 4 cột (category chuyển xuống dòng phụ), header không có nút, bảng tính không dùng màu đỏ, cột phải "nothing else". Có hai cách:
- **Sinh lại** bằng prompt SCR-33 mới (nhanh).
- **Sửa bản hiện tại** bằng chuỗi A1–A6, dán lần lượt từng prompt một.

- **A1** `Fix the expense table so every column fits: 4 columns — Line (benefit category as a small muted second line), Claimed, Result (icon + Covered/Excluded + rule chip), Covered. Amounts right-aligned; nothing cut off.`
- **A2** `Simplify the right column to three blocks only: the rule (EX-04 chip, title, one-sentence clause), the invoice crop with the violet box, and the remaining balance bar with one caption. Remove the policy section label, the crop header with coordinates, the "matched to category" line, the info callout and the sign-off trail.`
- **A3** `In the payable ledger show every amount in ink, deductions with a minus sign, no red; remove "Policy T-01" and the "Final approved disbursement" caption.`
- **A4** `Remove the "Review checklist" button from the header; the only red button is "Continue to decision" in the bottom bar. In the top bar show only the avatar "TH" — no name or role.`
- **A5** `Remove small meta text: file codes under document names, the "Ingestion / OCR v4.2" footer, "5 line items parsed · 1 flagged" next to the revision (keep only "Revision #2"), and the second sentence in the bottom bar.`
- **A6** `Use Be Vietnam Pro (not mono) for the status pill and section labels; keep mono only for amounts, codes and dates.`

---

## SCR-35 — Claim workspace · Decision

```text
Claim workspace, Decision tab. One job: the reviewer checks the AI-drafted summary, picks a recommendation and submits it for approval. Keep it minimal — short sentences, few controls, generous white space. Vibe: calm, accountable, editorial.

**DESIGN SYSTEM (REQUIRED):**
- RESTRAINT (most important): show ONLY the elements listed below. No extra metrics, IDs, chips, keyboard hints, signatures or panels from other tabs. Labels of at most 4 words. Text never wraps inside buttons, tabs or table headers.
- Type: Be Vietnam Pro sentence case for labels, names and sentences; IBM Plex Mono ONLY for money, codes and dates; at most one small uppercase kicker per column; person names never uppercase.
- Platform: Web, desktop 1440px. Canvas Warm Paper #F4F3EF, panels white #FFFFFF, 1px hairlines #D8D6D1
- Shell: 64px black icon-only sidebar #090909; white 56px top bar with only search, bell, avatar
- Signal Red #EB0029 for ONE primary button only; negative results Decline Crimson #A40022 on #FBEAEC with icon
- AI content only: violet #6D28D9 sparkle + "AI" on #F3EEFD; source box = 2px #6D28D9 outline, square
- 2px radius, flat, line icons, no gradients, no emoji
- Citation chips: small mono chips, #5B21B6 on #F3EEFD. Risk signal: rust #9A3412 on #FDEDE4 with flag icon.

**Page Structure:**
1. Case header — one row, 64px: left "CLM-2026-000184" (mono, muted) · "Nguyễn Văn An" (16px semibold) · "Gold Outpatient · 12/10/2026" (muted). Right: blue pill "In review" and payable "800,000 ₫" (mono, 20px). No buttons in the header. Nothing else.
2. Tabs, one row: Overview · Evidence · Validation 2 · Adjudication · Risk 1 · Decision · Timeline — "Decision" active (2px ink underline).
3. Left column (200px) "Documents": four rows, thumbnail + name + one status icon: Invoice · Receipt · Prescription · Visit summary.
4. Middle column, two sections with white space between:
   a. "Summary" with a small violet "AI draft · edited" marker. Editable text block with a 2px violet left border, four short sentences, each ending with citation chips:
      "Outpatient visit on 12/10/2026, diagnosis J02.9 acute pharyngitis. [Visit summary]
      Line 4, a skin-care cream, is excluded as a cosmetic product. [Invoice p2] [EX-04]
      Eligible 1,100,000 ₫ at 80% gives 880,000 ₫, capped to 800,000 ₫ by the remaining balance. [COINS-80] [LIM-ANNUAL]
      The abdominal ultrasound may not match the diagnosis."  ← no chip; amber dotted underline with a small note "No source".
   b. "Recommendation": three radio cards in one row — Approve full · Approve partial (selected, 2px ink border) · Reject. Below: amount "800,000 ₫" (read-only, mono) and a "Reason" select showing "Exclusion and limit applied".
5. Right column (480px):
   a. "Before you submit": 4 short lines with icons — ✓ No blocking alerts · ✓ Categories confirmed · ✓ Summary written · ⚠ 1 open signal.
   b. One rust signal card: flag icon, "Ultrasound may not match diagnosis", small label "AI signal — not a finding", ghost buttons Dismiss · Keep · Escalate.
6. Bottom bar (56px): left "1 open signal"; right secondary "Request information" and red primary "Submit recommendation".
```

**Prompt chỉnh sửa:**
- `Approver view of this tab: summary read-only; a decision card "Recommended: Approve partial · 800,000 ₫ · by Trần Thu Hà"; buttons Return, Reject (crimson outline) and red primary "Approve".`
- `Add the confirmation modal: square corners, title "Confirm decision", claim no., outcome "Partially approved", amount 800,000 ₫ in large mono, one line "This cannot be undone", red primary "Confirm".`
- `When a citation chip is hovered, show the matching invoice region with a violet box in the right column.`

---

## SCR-13 — New claim · Documents & submit

```text
New claim, step 3 of 3. One job: the Intake Officer uploads the claim documents and sees each page checked by AI before submitting. Keep it minimal — clear steps, few words, generous white space. Vibe: calm, reassuring, efficient.

**DESIGN SYSTEM (REQUIRED):**
- RESTRAINT (most important): show ONLY the elements listed below. No extra metrics, IDs, chips or panels. Labels of at most 4 words.
- Type: Be Vietnam Pro sentence case for labels, names and buttons; IBM Plex Mono ONLY for money, sizes, page counts and dates; Lexend Exa only for the page title.
- Platform: Web, desktop 1440px. Canvas Warm Paper #F4F3EF, panels white, 1px hairlines #D8D6D1, control borders #8C8984
- Shell: 240px black sidebar #090909, "Intake" active (#1D1C1C + 2px #EB0029 left bar); white top bar with only search, bell, avatar
- Signal Red #EB0029 for ONE primary button only
- AI page type: violet sparkle + "AI" on #F3EEFD; Low quality / Unclassified = amber #8A4B00 on #FDF3DE with icon
- 2px radius, flat, line icons, no gradients, no emoji, no illustrations

**Page Structure:**
1. Header: title "NEW CLAIM", muted "CLM-2026-000184 · Draft". Stepper below: 1 Member & policy ✓ · 2 Claim details ✓ · 3 Documents (active).
2. Main column:
   a. Dropzone (dashed #8C8984 border, white): upload icon, "Drop PDF, JPG or PNG files or browse", one muted line "Up to 20 MB per file · 30 pages".
   b. File list, 4 rows (name · pages · state · remove icon): hoa-don.pdf · 2 pages · ✓ — bien-lai.jpg · 1 page · ✓ — don-thuoc.png · 1 page · uploading 64% (thin bar) — tom-tat-kham.pdf · 1 page · ✓.
   c. "Page check": 5 equal cards in one row, each = thumbnail, type with AI mark, one status line:
      p1 Invoice · Readable — p2 Invoice · Readable — p3 Receipt · amber "Low quality — re-capture" — p4 amber "Choose type" select — p5 Visit summary · Readable.
3. Right panel (320px, white), five short lines: Nguyễn Văn An · Gold Outpatient · Remaining balance 800,000 ₫ · Service 12/10/2026 · Claimed 1,400,000 ₫.
4. Bottom bar: left "1 page needs a type"; right secondary "Back" and red primary "Submit" shown disabled with a small hint "Uploading…".
```

**Prompt chỉnh sửa:**
- `Show the AI fallback: amber banner "AI page check unavailable — choose page types manually"; every page card shows a "Choose type" select; Submit stays enabled.`
- `Add one rejected file row in crimson: "video.mp4 — File type not supported" with an x icon.`

---

## SCR-50 — Operations dashboard

```text
Operations dashboard for an Operations Manager. One job: see at a glance where the backlog is and whether AI is helping. Keep it minimal — four charts, a few big numbers, generous white space. Vibe: calm, editorial, data-driven.

**DESIGN SYSTEM (REQUIRED):**
- RESTRAINT (most important): show ONLY the elements listed below. No extra charts, tables, legends or annotations beyond what is listed. Labels of at most 4 words.
- Type: Lexend Exa for the page title and big numbers; Be Vietnam Pro sentence case for labels; IBM Plex Mono for small numbers, dates and axis labels.
- Platform: Web, desktop 1440px. Canvas Warm Paper #F4F3EF, white cards with 1px #D8D6D1 hairlines, no shadows, 32px gaps
- Shell: 240px black sidebar #090909, "Operations dashboard" active (#1D1C1C + 2px #EB0029 left bar)
- Charts: ink #090909 and greys only; Signal Red #EB0029 for the one bar that needs attention; Insight Violet #6D28D9 only for AI metrics; flat bars, thin gridlines, no gradients
- 2px radius, line icons, no emoji

**Page Structure:**
1. Header: title "OPERATIONS"; right: date range "01/10/2026 – 31/10/2026", "All plans" select, secondary button "Export XLSX".
2. Five number tiles in one row (label, big number, one small delta line):
   Open claims 146 (+12) · Median handling time 2.4 d (−0.6 d) · Approved in full 61% · AI fields corrected 18% (violet) · Signals dismissed 72%.
3. Two charts side by side:
   - "Claims by status": horizontal bars — Processing 9, Needs verification 31, Ready for review 22, In review 38, Needs information 14, Pending approval 32.
   - "Backlog age": four columns — <1 d 52, 1–3 d 61, 3–7 d 24, >7 d 9 (red, small label "Over SLA").
4. Two charts side by side:
   - "Workload per reviewer": 4 rows with name and a thin bar — Trần Thu Hà 12, Nguyễn Hải Yến 18, Đặng Văn Long 9, Phan Thùy Linh 21 (amber icon).
   - "AI corrections by field" (violet bars): Diagnosis 34%, Line description 21%, Total amount 9%, Invoice no. 6%, Service date 3%.
```

**Prompt chỉnh sửa:**
- `Use only ink and greys for chart bars; keep red only for the ">7 d" bar and violet only for the AI chart.`
- `Add an "Outcomes" chart below as a single stacked bar per week: Approved, Partially approved, Rejected, Returned, in ink and grey shades with a small legend.`

---

## SCR-01 — Sign in

```text
Sign-in page for ClaimLens staff: a dark, editorial entry screen with a simple form. Keep it minimal — one strong headline, one sentence, one form. Vibe: confident, precise, calm.

**DESIGN SYSTEM (REQUIRED):**
- RESTRAINT (most important): show ONLY the elements listed below.
- Type: Lexend Exa uppercase for the headline only; Be Vietnam Pro for everything else; IBM Plex Mono for the small kicker and footer.
- Platform: Web, desktop 1440px; full-bleed Ink Black #090909 background, text #ECE7E4 and #A9A6A1
- Signal Red #EB0029 for the one primary button and the focus ring; Shell Pink #FF7B91 for the small kicker
- Form panel white #FFFFFF with square corners; inputs 40px, 2px radius, #8C8984 borders
- No gradients, photos, illustrations or emoji; a faint 1px hairline grid on the black background is allowed

**Page Structure:**
1. Left half (black): "ClaimLens" wordmark top-left; small kicker "STAFF ACCESS"; headline on three lines "EVIDENCE FIRST. / EVERY LINE EXPLAINED. / EVERY DECISION OWNED." (last line in Signal Red); one sentence below in #A9A6A1 "AI reads the documents. Rules compute the money. People decide."
2. Right half: centred white panel (400px): title "Sign in"; fields "Work email" and "Password" (show/hide icon); link "Forgot password?"; full-width red primary button "Sign in".
3. Footer, small muted: "ClaimLens · synthetic data only".
```

**Prompt chỉnh sửa:**
- `Show the error state: above the fields a crimson #A40022 on #FBEAEC message with an icon "Email or password is incorrect." Do not say which one.`

---

## Prompt chỉnh sửa dùng chung

Mỗi lần chỉ dán **một** dòng.

- `Replace every rounded corner larger than 2px with 2px, except claim status pills which stay full pills.`
- `Use Signal Red (#EB0029) for only one filled button on this screen; turn all other filled buttons into white secondary buttons with #8C8984 borders.`
- `Every money value must be IBM Plex Mono, right-aligned, formatted like "1,400,000 ₫".`
- `Change all uppercase sentences back to sentence case; keep uppercase only for short section titles and mono kickers.`
- `Mark every AI-produced value with a small violet sparkle + "AI" chip; replace it with reviewer initials "TH" on values that are already accepted.`
- `Remove all shadows except on drawers and modals; separate areas with 1px #D8D6D1 hairlines.`
- `Show the empty state of this screen: icon in a 48px hairline square, title "No claims assigned to you", one sentence, and the primary action "Open search".`

## Review đợt 2 (export `stitch_claimlens_workbench.zip`, 31/10/2026)

Stitch đã nhận DESIGN.md của mình. Stitch chuyển phần token sang bộ màu riêng của nó (primary `#BC001F`, primary-container `#EB0029`) nhưng giữ nguyên phần chữ; bản chụp lưu ở [ui/stitch-design-system-snapshot.md](ui/stitch-design-system-snapshot.md). Ảnh và HTML của các màn đạt yêu cầu lưu trong `docs/ui/`.

| Màn | File | Đánh giá | Còn lỗi |
|---|---|---|---|
| SCR-01 Sign in | [png](ui/scr-01-sign-in.png) · [html](ui/scr-01-sign-in.html) | Tốt | Chữ bịa ở góc: "V2.4 · STAGING", "SECURE GATEWAY · NODE-SGN-04" |
| SCR-13 Upload | [png](ui/scr-13-upload.png) · [html](ui/scr-13-upload.html) | Tốt | Thẻ trang bị cắt chữ ("Low qualit…", "Choose typ"); nút "SUBMIT" IN HOA; người dùng ghi "Trần Thu Hà · Intake officer" nhưng Hà là reviewer; sidebar có mục của role khác |
| SCR-20 Review queue | [png](ui/scr-20-review-queue.png) · [html](ui/scr-20-review-queue.html) | Rất tốt | Màu pill bị đảo: *In review* đang amber, *Needs verification* đang xanh (phải ngược lại); *Returned* đang đỏ, trông như bị từ chối (nên dùng amber) |
| SCR-31 Evidence | [png](ui/scr-31-evidence.png) · [html](ui/scr-31-evidence.html) | Tốt | Pill "Check needed" xuống dòng và đè lên nút Accept; trường đã duyệt hiện "Verified + ổ khoá" thay vì chữ viết tắt "TH"; giá trị *Check* thiếu dấu ✦ AI; "3 fields to verify" chữ đỏ; top bar có "T. Harrison" |
| SCR-33 Adjudication | — | **Chưa sinh lại** | File export vẫn là bản cũ (còn "Review checklist", "T. Harrison"). Sinh lại bằng prompt SCR-33 mới |
| SCR-35 Decision | [png](ui/scr-35-decision.png) · [html](ui/scr-35-decision.html) | Rất tốt | "Approve partial" bị viền của thẻ đè lên; ô Reason bị cắt chữ ("…limit appl") |
| SCR-50 Operations | [png](ui/scr-50-operations.png) · [html](ui/scr-50-operations.html) | Rất tốt | Top bar thừa ("STATUS: STREAM LIVE", ⌘K, 2 avatar); ô tìm kiếm dùng mono; người dùng ghi "Intake Officer · Operations Lead"; sidebar có Review queue/Intake |

**Lỗi chung: shell không đồng nhất.** Mỗi màn một kiểu logo ("ClaimLens.", "■ CLAIMLENS", ô "CL"), một kiểu avatar, và sidebar không theo role. DESIGN.md đã bổ sung quy tắc shell (logo cố định, top bar chỉ có avatar chữ viết tắt, mục sidebar theo role). Nạp lại DESIGN.md rồi dùng prompt **SH** dưới đây cho từng màn.

**Nên xoá trong Stitch** (bản cũ, tránh Stitch học theo): `review_queue_claims_reviewer_priority_triage`, `review_queue_outpatient_adjudication`, `claim_workspace_adjudication_evidence_verification` (SCR-31 v1), `claim_workspace_adjudication_tab` (SCR-33 cũ, sau khi sinh lại).

### Prompt sửa đợt 2 (mỗi lần dán một dòng)

**Shell, dùng cho mọi màn (thay `<ROLE ITEMS>` và `<USER>` theo bảng dưới):**
- **SH** `Make the shell match the design system exactly: wordmark = small red square + "ClaimLens" (sentence case, Be Vietnam Pro 600); top bar = search field, bell, and one ink avatar circle with initials only — remove any name, role, version, environment or status chips; sidebar items only: <ROLE ITEMS>; bottom user block "<USER>".`

| Màn | `<ROLE ITEMS>` | `<USER>` |
|---|---|---|
| SCR-13 | Intake, Search claims, Members | Phạm Gia Bảo · Intake officer |
| SCR-20, 31, 33, 35 | Review queue, Search claims, Benefit plans | Trần Thu Hà · Claims reviewer |
| SCR-50 | Operations dashboard, AI quality, Audit log, Search claims | Lê Thanh Tâm · Operations manager |

**Theo từng màn:**
- **SCR-01 · F1** `Remove the corner texts "V2.4 · STAGING" and "SECURE GATEWAY · NODE-SGN-04"; keep only the wordmark top-left and "ClaimLens · synthetic data only" in the footer.`
- **SCR-13 · F1** `Page check cards: let the status line wrap to two lines so nothing is cut off ("Low quality — re-capture", "Choose type"); the Submit button label is sentence case "Submit".`
- **SCR-20 · F1** `Fix status pill colours: "In review" and "Ready for review" use blue #1E4FB5 on #E9EFFB; "Needs verification" uses amber #8A4B00 on #FDF3DE with an eye icon; "Returned" uses amber with an undo icon (not red).`
- **SCR-31 · F1** `In the status column use short badges that never wrap: "High", "Check", "Missing" (icon + word). On the selected row show the Accept and Edit buttons after the badge without overlapping.`
- **SCR-31 · F2** `Accepted fields show the reviewer initials chip "TH" instead of "Verified" and the lock icon; values with status Check show a small violet "AI" sparkle chip next to the value. "3 fields to verify" is ink, not red.`
- **SCR-35 · F1** `Make the three recommendation cards wide enough that "Approve partial" fits on one line inside its border; shorten the reason to "Exclusion + limit applied" so the select is not cut off.`
- **SCR-50 · F1** `Search placeholder uses Be Vietnam Pro (not mono); remove the "ADJUDICATION DESK" label and the "OPS" chip in the sidebar.`

## Review đợt 3 (export `stitch_claimlens_workbench (1).zip`)

| Màn | Bản trong Stitch | Kết quả | Việc còn lại |
|---|---|---|---|
| SCR-01 Sign in | `sign_in_staff_access_2` | ✅ **Chốt** | — |
| SCR-13 Upload | `new_claim_step_3_documents_2` | ✅ **Chốt** (shell, người dùng, nút "Submit" đã đúng) | Nhỏ: select "Choose typ" ở p4 vẫn bị cắt (prompt G3) |
| SCR-20 Review queue | `review_queue_decluttered_triage_2` | ✅ **Chốt** (màu pill đã đúng) | — |
| SCR-50 Operations | `operations_dashboard_2` | ✅ **Chốt** (sidebar theo role, avatar "LT") | — |
| SCR-35 Decision | `claim_workspace_decision_tab_3` | ⚠ Gần xong | "Approve partial" và ô Reason vẫn bị cắt chữ: đổi bố cục bằng prompt G2 |
| SCR-31 Evidence | `…_decluttered_2` | ❌ **Lỗi ngược** | Stitch thay thumbnail tài liệu và ảnh hoá đơn bằng avatar "TH", vì prompt gộp yêu cầu "initials" ở quá nhiều chỗ. Xoá bản `_2`, sửa bản `_1` (prompt G1a, G1b) |
| SCR-33 Adjudication | `claim_workspace_adjudication_tab` | ❌ **Chưa sinh lại** | Mã băm HTML trùng với lần trước. Tạo màn **mới** từ prompt SCR-33 (không sửa bản cũ), rồi xoá bản cũ |

`docs/ui/` đã cập nhật với các bản chốt (01, 13, 20, 50) và bản SCR-35 mới nhất. SCR-31 trong `docs/ui/` vẫn là bản `_1`, bản tốt hơn.

**Bài học:** prompt gộp nhiều màn tiết kiệm thời gian nhưng dễ làm hỏng màn phức tạp. Với SCR-31, 33, 35, mỗi lần chỉ gửi **một** prompt cho **một** màn.

### Prompt đợt 3 (mỗi lần một prompt, một màn)

- **G1a: "Claim Workspace – Evidence Verification Decluttered" bản `_1`** `In the status column use short badges that never wrap: "High", "Check", "Missing" (icon + word). On the selected row show Accept and Edit after the badge without overlapping. Do not change the document rail or the invoice viewer.`
- **G1b: cùng màn, sau G1a** `In the status column only: replace "Verified" + lock with a small green check and the initials "TH". Add a small violet "AI" chip next to values whose status is Check. Do not change the document rail, thumbnails or the invoice viewer.`
- **G2: "Claim Workspace – Decision Tab" bản `_3`** `Change the recommendation choice from three side-by-side cards to a vertical list of three full-width radio rows (Approve full, Approve partial, Reject). Put Approved amount and Reason each on its own full-width row so no text is cut off.`
- **G3: "New Claim – Step 3 Documents" bản `_2`** `On page card p4, make the "Choose type" select span the full card width so its label is not cut off.`
- **SCR-33:** tạo màn mới bằng khối `text` dưới tiêu đề "## SCR-33 — Claim workspace · Adjudication".

## Checklist review (mình dùng khi bạn gửi ảnh chụp)

| # | Kiểm tra | Nguồn |
|---|---|---|
| 1 | Mỗi màn tối đa một nút đỏ; từ chối/cảnh báo chặn dùng crimson kèm icon | DESIGN.md › Colors |
| 2 | Giá trị do AI tạo có marker ✦ AI; giá trị đã chấp nhận hiện chữ viết tắt tên người duyệt | IA › AI suggestion marker |
| 3 | Độ tin cậy có đủ 3 mức High / Check / Missing, mỗi mức có icon và chữ | IA › Confidence level |
| 4 | Chọn dòng ở vùng B thì vùng C nhảy tới vùng nguồn hoặc điều khoản tương ứng | IA › Linking rule |
| 5 | Cờ rủi ro dùng lời trung tính, ghi "Signal — not a finding", không có chữ "fraud" | IA › Signal, not finding |
| 6 | Mỗi câu trong bản tóm tắt AI có chip trích dẫn; câu không có nguồn bị đánh dấu | IA › Citation chips |
| 7 | Case header và action bar luôn hiển thị; nút chính (CTA) đổi đúng theo trạng thái hồ sơ | IA › Primary CTA by status |
| 8 | Có thể hiện các trạng thái: loading, AI processing, AI failed, empty, recalculating | IA › UI state contract |
| 9 | Số tiền dạng `800,000 ₫` font mono căn phải; ngày dạng dd/MM/yyyy | IA › Responsive & a11y |
| 10 | Tương phản ≥ 4.5:1; tên tiếng Việt hiển thị đúng dấu | WCAG AA |
| 11 | Số liệu khớp golden case (1,400,000 → 1,100,000 → 880,000 → 800,000) | 08 › US13 |
