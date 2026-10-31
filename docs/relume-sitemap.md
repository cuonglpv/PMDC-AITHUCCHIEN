# Relume sitemap input

Đầu vào để render [Information Architecture](04-information-architecture.md) bằng [Relume](https://www.relume.io) (Site Builder → Sitemap). Relume vẽ sitemap dạng **page → sections**. Ở đây mỗi màn kiểu *Page* trong screen inventory là một page; **mỗi tab của Claim workspace là một page con**, vì mỗi tab là một màn làm việc riêng với nhiều tác vụ. Modal và drawer là section của page mở ra chúng.

Mỗi section ghi **tác vụ** người dùng làm được trên màn đó, lấy từ use case (03), acceptance criteria (08) và screen-to-API mapping (04). Cột cuối của bảng kiểm tra cho biết mỗi tác vụ đến từ đâu.

**Bản render hiện tại:** [images/ia-sitemap-relume.png](images/ia-sitemap-relume.png), vẽ theo phong cách sitemap của Relume từ [diagrams/ia-sitemap.html](diagrams/ia-sitemap.html) (dữ liệu trong file HTML khớp bảng bên dưới). Relume AI không sinh được section cho sitemap này (lỗi "Model failed to add any pages or sections" ở cả 3 lần thử, 31/10/2026), nên ảnh được dựng lại ngoài Relume. Sửa nội dung thì sửa mảng `site` trong file HTML, rồi chụp lại:

```bash
"/c/Program Files/Google/Chrome/Application/chrome.exe" --headless=new --hide-scrollbars --force-device-scale-factor=1.5 --window-size=5400,1800 --virtual-time-budget=8000 --screenshot=docs/images/ia-sitemap-relume.png "file:///D:/PMDC-AITHUCCHIEN/docs/diagrams/ia-sitemap.html"
```

Cách dùng với Relume (nếu thử lại):
1. Relume → project **ClaimLens** → mở bảng Project → dán **Prompt** bên dưới → Number of pages **20+** → Generate sitemap.
2. So với **Cây page và section**; sửa tay chỗ lệch. Relume hay thêm section marketing (Hero, Testimonial, FAQ, CTA, Footer) — xoá đi.
3. Chụp canvas → `docs/images/ia-sitemap-relume.png`; Export → CSV → `docs/ia-sitemap-relume.csv`.

## Prompt

```text
ClaimLens: internal staff web app for an insurer, not a marketing site. AI-assisted workbench for outpatient health insurance claims: AI reads documents, a rule engine calculates, people decide. Roles: Intake Officer, Claims Reviewer, Senior Approver, Administrator. App shell on every page: sidebar (Work, Policy, Insights) + top-bar claim lookup. Use exactly this page tree; sections list the tasks on each page.

Sign in: Sign-in form; Generic error; Role routing.

WORK
Intake: Tabs drafts/submitted; Claims table; New claim; Empty state.
- New claim: Member & policy search (name, ID, policy no., status, coverage, balance); Claim details (service date, provider, total, channel, coverage warning); Document upload (PDF/JPG/PNG, limits, progress, remove); AI page check (type, confidence, choose type, low quality hint, replace); AI unavailable banner; Submit.
Review queue: Tabs available/assigned/needs verification/returned; Priority table; Filters; Start review (self-assign); Return comment; Empty states.
Approval queue: Tabs pending/decided by me; Priority table (payable, high value, High flags, age); Recommendation preview; Open claim.
Claim workspace: Case header with action by status; Tab bar; Document rail (type, quality, AI status); Source viewer; Sticky action bar; Conflict/recalculating states.
- Overview: Claim summary; Recommendation and payable; Line results; Overrides; Attested AI values; Kept flags.
- Documents & evidence: Fields by document, lowest confidence first; High/Check/Missing; Accept/edit/not present; Source pin; Draw source region; Reclassify page, re-run AI on page; Remaining counter; Manual entry fallback.
- Validation: Blocking/Warning alerts; Checks (documents, identity, dates, totals, exact duplicate); Fix data; Dismiss with reason; Re-validate; Clinical check not run.
- Adjudication: Lines with AI category and confidence; Confirm category; Run adjudication; Covered/Partial/Excluded with rule code and clause; Payable formula; Balance before/after; Revisions and what changed; Override drawer (amount, reason, note, caps); No rule set blocker.
- Risk: AI flag cards (type, severity, reason); Evidence (line, diagnosis, documents); Signal not a finding; Dismiss with reason; Keep for approver; Approver read-only view.
- Decision: AI summary with citation chips; Uncited warning; Edit summary; Recommendation type with reason; High flag warning; Attest AI values; Submit (missing items named); Approve/partial/reject/return; Confirmation modal (claim, outcome, amount, reason); Maker-checker block; Balance changed blocker.
- Timeline: Events (status, uploads, AI runs, corrections before/after, alerts, flags, revisions, overrides, decision) with actor and time; Filters.

POLICY
Benefit plans: Plan list; Versions (draft, published, effective-from); Create draft; Run test.
- Rule set editor: Categories; Exclusions; Waiting periods; Reimbursement rate; Per-visit and annual limits; Rule code and clause; Publish with effective-from; Published read-only.
- Rule set test run: Pick synthetic claims; Run draft; Draft vs current per line; Differences.

INSIGHTS
AI quality dashboard: Date and model filters; KPI tiles; Low confidence by document type; Corrections by field; Mapping auto-accept by category; Flag dismissal by signal type; Drill down; Export XLSX.

Profile: Details; Sign out.
403 / 404: Message; Back to role home.
```

## Cây page và section (bảng kiểm tra)

| Page | Sections (tác vụ) | Role | Nguồn |
|---|---|---|---|
| **Sign in** | Sign-in form · Generic error · Role routing | all | UC01, US01 |
| **Work** (nhóm) | | | |
| Intake | Tabs drafts / submitted · Claims table · New claim · Empty state | Intake | UC02, US02, US03 |
| └ New claim | Step 1 member & policy (tìm theo tên / ID / số HĐ, trạng thái, kỳ bảo hiểm, số dư, không tìm thấy) · Step 2 claim details (ngày khám, cơ sở, tổng tiền, kênh, cảnh báo ngoài kỳ) · Step 3 documents (kéo thả, giới hạn, tiến độ, xoá file) · AI page check (loại trang, Unclassified, Low quality, thay trang) · AI unavailable banner · Submit | Intake | UC02, US02–US04 |
| Review queue | 4 tab · Bảng ưu tiên · Bộ lọc · Start review · Xem comment trả về · Empty states | Reviewer | US23, BR-09 |
| Approval queue | 2 tab · Bảng ưu tiên (giá trị cao, cờ High, tuổi) · Xem trước đề xuất · Mở claim · Empty state | Approver | UC08, US18, BR-09 |
| Claim workspace | Case header + CTA theo trạng thái · Tab bar · Document rail · Source viewer · Sticky action bar · Trạng thái conflict / recalculating | Reviewer, Approver | IA › Claim workspace |
| └ Overview | Tóm tắt claim · Đề xuất + payable · Tóm tắt kết quả dòng · Overrides · Giá trị AI đã xác nhận · Cờ được giữ | Reviewer, Approver | SCR-30, US18 |
| └ Documents & evidence | Field theo tài liệu, độ tin cậy thấp trước · High / Check / Missing · Accept / Edit / Not present · Source pin · Vẽ vùng nguồn · Đổi loại trang + chạy lại AI trang đó · Bộ đếm · Nhập tay khi AI lỗi | Reviewer | UC03, US05, US06, BR-02 |
| └ Validation | Alert Blocking / Warning · Các nhóm kiểm tra + trùng hoá đơn · Sửa dữ liệu · Dismiss có lý do · Kiểm tra lại · "Clinical check not run" | Reviewer | UC04, US07, US08, US09 |
| └ Adjudication | Nhóm quyền lợi AI + độ tin cậy · Xác nhận / đổi nhóm · Chạy adjudication · Kết quả dòng + mã rule + điều khoản · Công thức payable · Số dư trước / sau · Revision + what changed · Drawer Override · Chặn khi không có rule set | Reviewer, Approver (đọc) | UC05, US10–US13, US16 |
| └ Risk | Thẻ cờ AI (loại, mức, lý do) · Bằng chứng (dòng chi phí, chẩn đoán, chứng từ) · "Signal — not a finding" · Dismiss · Keep for approver · Approver xem (chỉ đọc) | Reviewer, Approver (đọc) | UC06, US09, US14, US18 |
| └ Decision | Bản nháp AI + citation chips · Cảnh báo câu không nguồn · Sửa tóm tắt · Loại đề xuất + lý do khi khác · Cảnh báo cờ High · Attestation · Submit (nêu điều kiện thiếu) · Panel quyết định của approver · Modal xác nhận · Chặn maker–checker · Chặn khi số dư đổi | Reviewer, Approver | UC07, UC08, US15, US17, US18 |
| └ Timeline | Sự kiện theo thứ tự (trạng thái, upload, AI run + model, sửa field trước/sau, alert, cờ, revision, override, quyết định) · Lọc theo loại | Reviewer (claim của mình), Approver | UC11, US22 |
| **Policy** (nhóm) | | | |
| Benefit plans | Danh sách plan · Danh sách version · Tạo draft · Mở editor · Chạy test | Administrator; Reviewer, Approver đọc | UC09, US19 |
| └ Rule set editor | Categories · Exclusions · Waiting periods · Reimbursement rate · Limits · Mã rule + điều khoản · Publish với ngày hiệu lực · Version đã publish chỉ đọc | Administrator | UC09, US19 |
| └ Rule set test run | Chọn claim mẫu synthetic · Chạy draft · So sánh draft vs hiện hành · Khác biệt | Administrator | UC09, US20 |
| **Insights** (nhóm) | | | |
| AI quality dashboard | Bộ lọc ngày + model · KPI tiles · Low confidence theo loại tài liệu · Corrections theo field · Mapping auto-accept theo nhóm · Flag dismissal theo loại tín hiệu · Drill down · Export XLSX | Approver | UC10, US21 |
| Profile | Thông tin, role · Sign out | all | SCR-70 |
| 403 / 404 | Thông báo · Về role home | all | SCR-90 |

Tổng: 19 page (không tính 3 nhóm sidebar).
