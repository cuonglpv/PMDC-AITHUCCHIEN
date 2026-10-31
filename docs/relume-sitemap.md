# Relume sitemap input

Đầu vào để render [Information Architecture](04-information-architecture.md) bằng [Relume](https://www.relume.io) (Site Builder → Sitemap). Relume vẽ sitemap dạng **page → sections**. Ở đây mỗi màn kiểu *Page* trong screen inventory là một page; **mỗi tab của Claim workspace là một page con**, vì mỗi tab là một màn làm việc riêng với nhiều tác vụ. Modal và drawer là section của page mở ra chúng.

Mỗi section ghi **tác vụ** người dùng làm được trên màn đó, lấy từ use case (03), acceptance criteria (08) và screen-to-API mapping (04). Cột cuối của bảng kiểm tra cho biết mỗi tác vụ đến từ đâu.

**Bản render hiện tại:** [images/ia-sitemap-relume.png](images/ia-sitemap-relume.png), vẽ theo phong cách sitemap của Relume từ [diagrams/ia-sitemap.html](diagrams/ia-sitemap.html) (dữ liệu trong file HTML khớp bảng bên dưới). Relume AI không sinh được section cho sitemap này (lỗi "Model failed to add any pages or sections" ở cả 3 lần thử, 31/10/2026), nên ảnh được dựng lại ngoài Relume. Sửa nội dung thì sửa mảng `site` trong file HTML, rồi chụp lại:

```bash
"/c/Program Files/Google/Chrome/Application/chrome.exe" --headless=new --hide-scrollbars --force-device-scale-factor=1.5 --window-size=5400,1780 --virtual-time-budget=8000 --screenshot=docs/images/ia-sitemap-relume.png "file:///D:/PMDC-AITHUCCHIEN/docs/diagrams/ia-sitemap.html"
```

Cách dùng với Relume (nếu thử lại):
1. Relume → project **ClaimLens** → mở bảng Project → dán **Prompt** bên dưới → Number of pages **20+** → Generate sitemap.
2. So với **Cây page và section**; sửa tay chỗ lệch. Relume hay thêm section marketing (Hero, Testimonial, FAQ, CTA, Footer) — xoá đi.
3. Chụp canvas → `docs/images/ia-sitemap-relume.png`; Export → CSV → `docs/ia-sitemap-relume.csv`.

## Prompt

```text
ClaimLens is an internal staff web app (not a marketing site) for an insurer: an AI-assisted workbench for adjudicating outpatient health insurance claims. AI reads documents, a deterministic rule engine calculates the payable amount, people decide. Roles: Intake Officer, Claims Reviewer, Senior Approver, Administrator. Do not add marketing pages or sections (no hero, about, pricing, blog, testimonials, FAQ, newsletter, CTA, footer). Every page after sign-in has the app shell: left sidebar (Work, Policy, Insights, Administration) and top bar with global claim search. Use exactly this page tree; each page lists its sections and the tasks done there.

Sign in: Sign-in form (email, password, sign in); Generic error (no account state disclosed); Role routing (open the role home).

WORK
Intake: Tabs drafts / my submissions / needs information; Claims table (open draft, continue upload); Needs information list (add supplementary documents, deadline); New claim button; Empty state.
- New claim: Step 1 member & policy (search by name, ID or policy no., policy status, coverage period, remaining balance, no member found); Step 2 claim details (service date, provider, claimed total, channel, out-of-coverage warning); Step 3 documents (drag-and-drop PDF/JPG/PNG, size and page limits, upload progress, remove file); AI page check (page type and confidence, Unclassified choose type, Low quality re-capture hint, replace page); AI unavailable banner; Submit for processing.
Review queue: Queue tabs available / assigned to me / needs verification / needs information / returned; Priority table (claim no., member, amount, status, AI status, age, assignee); Filters status / priority / assignee; Start review (self-assign); Return comment preview; Needs information tab with deadline and extend deadline; Empty states.
Approval queue: Tabs pending / escalated / decided by me; Priority table (payable, high-value marker, open High flags, age); Recommendation preview (outcome, payable, reviewer); Open claim to decide; Empty state.
Search claims: Search by claim no., member or invoice no.; Filters; Results scoped to role; Open claim.
Claim workspace: Case header (claim no., member, policy, service date, status, assignee, rule-set version, payable, primary action by status); Tab bar; Document rail (pages by type, quality, AI status); Source viewer (highlighted region or rule clause); Sticky action bar (progress counters, next action); Conflict and recalculating states.
- Overview: Claim summary; Recommendation and payable; Line results summary; Overrides list; Attested AI values; Open flags; Status history snapshot.
- Documents & evidence: Field list grouped by document, lowest confidence first; Confidence High / Check / Missing; Accept / edit / mark not present; Source pin to page region; Draw region for a value without source; Reclassify page and re-run AI on this page; Remaining-fields counter; Manual entry when AI unavailable.
- Validation: Alerts by severity Blocking / Warning; Checks (required documents, name and policy match, dates, invoice total = sum of lines, exact duplicate link); Fix data at source; Dismiss with reason code and note; Re-validate after change; Clinical check not run notice; Request information modal (checklist pre-filled from alerts, add items, deadline, template letter, export PDF).
- Adjudication: Expense lines with AI benefit category and confidence; Confirm or change category (< 0.85 required); Run adjudication; Line results Covered / Partially covered / Excluded with rule code and clause; Payable formula (eligible, rate, remaining limit, payable); Remaining balance before / projected after; Calculation revisions and what changed; Override line drawer (new amount or result, reason code, note, cap by claimed amount and limit); No effective rule set blocker.
- Risk: Flag cards (type, severity, source AI or rule, evidence side by side, matching claim); Signal not a finding label; Dismiss with reason; Keep for approver with note; Add manual flag; Escalate for investigation (approver only); Escalated marker.
- Decision: AI summary draft with citation chips; Uncited sentence warning; Edit summary; Recommendation type proposed from payable, change with reason; Open High flag warning; Attestation of auto-accepted AI values; Submit recommendation (disabled with missing item named); Approver decision panel approve / partially approve / reject / return; Decision confirmation modal (claim no., outcome, final amount, reason code); Maker-checker block; Balance changed, re-run blocker.
- Timeline: Ordered events (status changes, uploads, AI runs with model version, field corrections before/after, alerts, flags, calculation revisions, overrides, decision) with actor and time; Filters by event type; Access scope notice.

POLICY
Members & policies: Member search; Member detail (policies, coverage period, benefit plan, remaining balance, claim history); Read-only seeded data notice.
Benefit plans: Plan list; Version list (draft, published, effective-from); Create draft version; Open rule set editor; Run test.
- Rule set editor: Covered categories; Exclusions; Waiting periods; Coinsurance (reimbursement rate); Per-visit and annual limits; Rule code and clause text per rule; Validation errors; Publish with effective-from date (must be later than current); Published versions read-only.
- Rule set test run: Pick synthetic sample claims; Run draft; Per-line results draft vs current; Differences highlighted; No real claim changed notice.

INSIGHTS
Operations dashboard: Date range and plan filters; KPI tiles (open claims, median handling time, approved in full, AI fields corrected, signals dismissed); Claims by status; Backlog age buckets; Reviewer workload; Outcomes over time; Drill down to claims; Export XLSX; Empty state no data.
AI quality: Low-confidence rate; Correction rate by field; Flag dismissal rate; Clinical check false-positive rate; Model version filter; Drill down; Export.
Audit log: Filters (actor, event type, date, claim no.); Claim events table (approver); Access log (sign-ins, role changes, claim opens by claim no.) for administrator; Event detail.

ADMINISTRATION
Staff accounts: Staff table (name, email, role, status); Create account modal (exactly one role); Edit role; Disable / enable account; Own account protected (no self role change or disable); Role change history.
Profile: Name, email and role; Sign out.
403 / 404: Message; Back to role home.
```

## Cây page và section (bảng kiểm tra)

| Page | Sections (tác vụ) | Role | Nguồn |
|---|---|---|---|
| **Sign in** | Sign-in form · Generic error · Role routing | all | UC01, US01 |
| **Work** (nhóm) | | | |
| Intake | Tabs drafts / submitted / needs information · Claims table · Needs information list (thêm chứng từ bổ sung, hạn) · New claim · Empty state | Intake | UC02, UC03, US19 |
| └ New claim | Step 1 member & policy (tìm theo tên / ID / số HĐ, trạng thái, kỳ bảo hiểm, số dư, không tìm thấy) · Step 2 claim details (ngày khám, cơ sở, tổng tiền, kênh, cảnh báo ngoài kỳ) · Step 3 documents (kéo thả, giới hạn, tiến độ, xoá file) · AI page check (loại trang, Unclassified, Low quality, thay trang) · AI unavailable banner · Submit | Intake | UC02, UC03, US03–US05 |
| Review queue | 5 tab · Bảng ưu tiên · Bộ lọc · Start review · Xem comment trả về · Tab needs information: hạn + gia hạn · Empty states | Reviewer | US19, US25, BR-09 |
| Approval queue | 3 tab · Bảng ưu tiên (giá trị cao, cờ High, tuổi) · Xem trước đề xuất · Mở claim · Empty state | Approver | UC10, US20, BR-09 |
| Search claims | Tìm theo số claim / member / số hoá đơn · Lọc · Kết quả theo quyền · Mở claim | Intake, Reviewer, Approver | US25, BR-11 |
| Claim workspace | Case header + CTA theo trạng thái · Tab bar · Document rail · Source viewer · Sticky action bar · Trạng thái conflict / recalculating | Reviewer, Approver | IA › Claim workspace |
| └ Overview | Tóm tắt claim · Đề xuất + payable · Tóm tắt kết quả dòng · Overrides · Giá trị AI đã xác nhận · Cờ mở · Lịch sử trạng thái | Reviewer, Approver | SCR-30, US20 |
| └ Documents & evidence | Field theo tài liệu, độ tin cậy thấp trước · High / Check / Missing · Accept / Edit / Not present · Source pin · Vẽ vùng nguồn · Đổi loại trang + chạy lại AI trang đó · Bộ đếm · Nhập tay khi AI lỗi | Reviewer | UC04, US06, US07, BR-02 |
| └ Validation | Alert Blocking / Warning · 5 nhóm kiểm tra + trùng lặp · Sửa dữ liệu · Dismiss có lý do · Kiểm tra lại · "Clinical check not run" · Modal Request information (checklist, hạn, thư mẫu, PDF) | Reviewer | UC05, UC09, US08, US09, US19 |
| └ Adjudication | Nhóm quyền lợi AI + độ tin cậy · Xác nhận / đổi nhóm · Chạy adjudication · Kết quả dòng + mã rule + điều khoản · Công thức payable · Số dư trước / sau · Revision + what changed · Drawer Override · Chặn khi không có rule set | Reviewer, Approver (đọc) | UC06, US11–US14, US17 |
| └ Risk | Thẻ cờ (loại, mức, nguồn, bằng chứng song song) · "Signal — not a finding" · Dismiss · Keep for approver · Thêm cờ tay · Escalate (approver) · Escalated marker | Reviewer, Approver | UC07, US10, US15, US20 |
| └ Decision | Bản nháp AI + citation chips · Cảnh báo câu không nguồn · Sửa tóm tắt · Loại đề xuất + lý do khi khác · Cảnh báo cờ High · Attestation · Submit (nêu điều kiện thiếu) · Panel quyết định của approver · Modal xác nhận · Chặn maker–checker · Chặn khi số dư đổi | Reviewer, Approver | UC08, UC10, US16, US18, US20 |
| └ Timeline | Sự kiện theo thứ tự (trạng thái, upload, AI run + model, sửa field trước/sau, alert, cờ, revision, override, quyết định) · Lọc theo loại · Thông báo phạm vi | Reviewer (claim của mình), Approver | UC13, US24 |
| **Policy** (nhóm) | | | |
| Members & policies | Tìm member · Chi tiết (hợp đồng, kỳ, plan, số dư, lịch sử claim) · Ghi chú dữ liệu seed | Administrator, Intake (đọc) | US03, SCR-40 |
| Benefit plans | Danh sách plan · Danh sách version · Tạo draft · Mở editor · Chạy test | Administrator; Reviewer, Approver đọc | UC11, US21 |
| └ Rule set editor | Categories · Exclusions · Waiting periods · Coinsurance · Limits · Mã rule + điều khoản · Lỗi nhập · Publish với ngày hiệu lực · Version đã publish chỉ đọc | Administrator | UC11, US21 |
| └ Rule set test run | Chọn claim mẫu synthetic · Chạy draft · So sánh draft vs hiện hành · Khác biệt · Không đổi dữ liệu thật | Administrator | UC11, US22 |
| **Insights** (nhóm) | | | |
| Operations dashboard | Bộ lọc · KPI tiles · Claims by status · Backlog age · Reviewer workload · Outcomes · Drill down · Export XLSX · Empty state | Approver | UC12, US23 |
| AI quality | Low-confidence rate · Correction rate by field · Flag dismissal rate · Clinical false-positive rate · Lọc model version · Drill down · Export | Approver | UC12, US10, US23 |
| Audit log | Bộ lọc · Claim events (approver) · Access log (administrator) · Chi tiết sự kiện | Approver, Administrator | UC13, UC14, US02, US24 |
| **Administration** (nhóm) | | | |
| Staff accounts | Bảng nhân viên · Modal tạo tài khoản (một role) · Đổi role · Khoá / mở · Không tự đổi role / tự khoá · Lịch sử đổi role | Administrator | UC14, US02 |
| Profile | Thông tin, role · Sign out | all | SCR-70 |
| 403 / 404 | Thông báo · Về role home | all | SCR-90 |

Tổng: 24 page (không tính 4 nhóm sidebar).
