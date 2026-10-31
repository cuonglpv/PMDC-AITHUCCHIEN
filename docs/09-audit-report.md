# Audit Report — Requirements Analysis (ClaimLens)

> Ngày audit: 31/10/2026 · Phạm vi: `docs/03-use-cases.md`, `docs/diagrams/use-case.puml`, `docs/08-invest-requirements.md`, `docs/04-information-architecture.md`, đối chiếu `01_Product_Research.xmind`, `02_Feature_Map.xmind`.
>
> Ngoài phạm vi: `docs/05-ui-brief-stitch.md`, `docs/DESIGN.md`, `docs/stitch-prompts.md`, `docs/ui/` (chỉ đọc để xác nhận quyết định ngôn ngữ UI = English).

## 1. Tóm tắt

| Mức độ | Số lượng | Ý nghĩa |
|---|---|---|
| **Critical** | 1 | Mâu thuẫn trực tiếp với một quy tắc bất khả xâm phạm (bảo vệ dữ liệu y tế). |
| **Major** | 22 | Sai/thiếu làm lệch hành vi hệ thống, lifecycle, phân quyền, tiền, hoặc phá traceability. |
| **Minor** | 15 | Đặt tên, trình bày, chi tiết AC, tài liệu cũ. |

**Điểm làm tốt (đã kiểm tra, không cần sửa):**

- Chiều mũi tên UML đúng chuẩn: `«include»` base → included (UC03→AI-01, UC05→AI-03, UC06→AI-02/SYS-01, UC08→AI-04, UC10→SYS-02, UC11→SYS-01); `«extend»` extension → base (AI-01→UC04, UC09→UC08). Generalization trong `.puml` đúng chiều con → cha (`Intake --|> Staff`).
- Hai bản diagram (Mermaid và PlantUML) có **cùng tập quan hệ** — 18 association, 9 include/extend, 4 liên kết secondary actor.
- Ngưỡng confidence nhất quán giữa 03 và 08: page < 0.80 → Unclassified (03:155, 08:86); field < 0.70 để trống, 0.70–0.90 verify (03:157, 08:93–94); money/identity 0.95 (03:162, 08:94); mapping < 0.85 xác nhận (03:158, 08:118).
- Golden case đúng toán học: 1.400.000 − 300.000 = 1.100.000 → × 80% = 880.000 → min(880.000; 800.000) = **800.000 ₫** (03:279, 08:128), khớp research map mục 09.
- Tổng điểm đúng: **Must = 18 story / 85 pts**, **Should = 6 story / 24 pts** (tổng 109). Không story Must nào phụ thuộc trực tiếp (cột *Depends on*) vào story Should; không có vòng phụ thuộc.
- Role code nhất quán ở 03:23–28 và 08:208; tên trạng thái lifecycle viết giống nhau ở 03, 08 và 04.
- Mọi UC có ít nhất một story; mọi story thuộc một UC; mọi BR-01…BR-11 được ít nhất một UC tham chiếu.
- Ranh giới AI ↔ Rule Engine ↔ Human được nêu rõ (03:9–17, 03:149) và AI không xuất hiện ở tính tiền, eligibility, duyệt, phân quyền.

## 2. Findings

### 2.1 Critical

| ID | File:dòng | Mô tả | Đề xuất sửa |
|---|---|---|---|
| AUD-01 | 03:180, 03:225, 03:327; 04:33, 04:62, 04:87; 08:59 | **SYSTEM_ADMIN (và OPS_MANAGER) xem được lịch sử quyết định của claim** — timeline gồm field corrections before/after (chẩn đoán, thuốc, tên, số CCCD), mâu thuẫn trực tiếp BR-11 *"Medical documents and identity data are visible only to roles working on the claim"*. UC13 còn ghi Sys Admin "search a claim" trong khi sidebar không cho Sys Admin dùng Search claims (04:82). Ops được "read" Claim workspace (04:33). | (a) Bỏ Sys Admin khỏi UC13; Sys Admin chỉ xem **access/activity log** (ai đăng nhập, ai đổi quyền, ai mở claim nào) thuộc UC14 — không có nội dung y tế. (b) Ops Manager xem timeline/audit **ở dạng che (masked)** cho dữ liệu y tế/danh tính; drill-down từ dashboard chỉ tới metadata. (c) Viết lại BR-11 có khái niệm *masked view*. (d) Cập nhật role matrix, 2 diagram, US24, SCR-36/SCR-52. |

### 2.2 Major

| ID | File:dòng | Mô tả | Đề xuất sửa |
|---|---|---|---|
| AUD-02 | 03:251–252; 08:40, 08:87; 04:230–232 | **Thời điểm chạy AI-01 mâu thuẫn.** UC03 main flow: officer submit → `Processing` → AI-01 phân loại + kiểm tra độ rõ. Nhưng UC03 alt, US05 và Intake flow nói phân loại/độ rõ được báo **trước khi submit** để officer chụp lại. | Tách AI-01 thành 2 giai đoạn trong spec: **(1) Page check** khi upload (classify + quality, trước submit); **(2) Field extraction** sau submit (trong `Processing`). Sửa main flow UC03 và nhãn lifecycle. |
| AUD-03 | 03:35, 03:80, 03:112, 03:144, 03:261; puml:76 | **AI-01 «extend» UC04 nhưng chữ thì nói "included".** Chiều mũi tên đúng, nhưng: nhóm diagram ghi "AI-assisted (included only)", câu 03:35 nói AI use case "are «include»d", bảng 03:144 ghi "Included by … UC04 (re-run)". UC04 không khai báo extension point có tên; nhãn `re-run page` là tên chứ không phải điều kiện. | Giữ quan hệ `AI-01 «extend» UC04`. Đổi nhãn nhóm → "AI-assisted (included / extending)"; bảng 03:144 → "Extends UC04 (re-run one page)". Khai báo trong UC04: *Extension point "Page re-read"*, điều kiện `[page misclassified or unreadable]`. Trong `.puml` dùng usecase có compartment extension points. |
| AUD-04 | 03:119, 03:317; 08:57 | **UC11 «include» SYS-01 (test run) là bắt buộc**, nhưng US22 (test rule set) là **Should**. Nhãn "test run" đặt trên quan hệ include (include không có điều kiện). | Khuyến nghị **nâng US22 lên Must** (3 pts → Must 88 / Should 21): research map yêu cầu "Test benefit rules using a sample claim" và đây là lớp bảo vệ duy nhất trước khi rule sai ảnh hưởng tiền. Phương án B: giữ Should, đổi quan hệ thành extension (tách "Test draft rule set" làm bước tuỳ chọn) và sửa UC11 main flow. |
| AUD-05 | 03:91, 03:118, 03:309; puml:53, 82; 08:211 | **SYS-02 "Record audit event" không phải use case** (không có actor/mục tiêu, là bước kỹ thuật) và chỉ được UC10 include, trong khi audit xảy ra ở UC02, UC04, UC05, UC07, UC08, UC11, UC14. 08:211 gán "mọi state change ghi audit" cho **BR-07** — sai, BR-07 là reason code. | Xoá SYS-02 khỏi diagram; thêm **BR-12**: *"Every state change, field correction, override, dismissal, decision and access to sensitive data writes an immutable audit event."* Tham chiếu BR-12 trong spec và 08:211. |
| AUD-06 | 03:176, 03:195–202, 03:309; 04:242 | **Approver yêu cầu bổ sung thông tin không có đường đi.** Role matrix và IA (Approver flow "evidence missing → Request information") cho Approver dùng UC09, nhưng UC09 chỉ extend UC08, UC10 không nhắc, lifecycle không có `PendingApproval → NeedsInformation`. | Khuyến nghị **phương án B**: bỏ Approver khỏi UC09 — Approver dùng **Return** kèm comment ("thiếu giấy ra viện"), reviewer phát hành yêu cầu. Giữ một chủ sở hữu duy nhất của `NeedsInformation`. Sửa matrix, diagram, IA approver flow, SCR-37 role. (Phương án A: thêm `UC09 «extend» UC10` + transition mới.) |
| AUD-07 | 03:147, 03:302; 08:54, 08:153–155; 04:255 | **UC09 dùng AI-04 soạn thư** ("AI-04 wording") nhưng diagram không có quan hệ UC09–AI-04, bảng AI placement chỉ ghi UC08, US19 không gắn 🟣 và không có AC/fallback cho AI; IA lại có citation chips cho "request letter". | Khuyến nghị **bỏ AI khỏi UC09 trong MVP**: thư sinh từ checklist + template (deterministic). Sửa 03:302 và 04:255. (Phương án khác: thêm `UC09 «include» AI-04`, gắn 🟣 cho US19 với AC confidence/fallback.) |
| AUD-08 | 03:269; 08:44–45, 08:109–113, 08:136; 04:143, 04:254, 04:284–285 | **Alert vs Risk flag lẫn lộn.** Duplicate là "Blocking alert" trong Validation (US09) nhưng IA lấy "Possible duplicate" làm ví dụ Risk flag; clinical inconsistency (US10) được gọi là "flag" nhưng SCR-32 Validation chứa US10; US15 phụ thuộc US09, US10. | Chốt quy tắc: **Deterministic → Alert** (tab Validation, có Blocking/Warning) gồm exact duplicate; **AI signal + near-duplicate → Risk flag** (tab Risk). Chuyển US10 sang SCR-34; US09 near-duplicate → Risk flag; ghi rõ "AI-03 chạy trong UC05, kết quả xử lý ở UC07". |
| AUD-09 | 03:146, 03:269 | **Phát hiện chỉnh sửa chứng từ (document alteration)** được dùng làm lý do đặt AI-03 và nằm trong UC05 main flow, nhưng **không có story nào**. | Khuyến nghị đưa ra ngoài MVP (ghi "later phase" ở 03:149) và bỏ khỏi UC05 main flow; hoặc thêm story Could. |
| AUD-10 | 03:286–287; 08:50, 08:137; 04:218 | **Ai được escalate?** UC07: reviewer chỉ dismiss/keep/add, **Approver** escalate. US15 và IA reviewer flow: reviewer cũng "escalate". | Reviewer: *Dismiss* hoặc *Keep for approver*; chỉ Approver: *Escalate for investigation* (BR-08). Sửa US15 AC, IA flow 04:218, IA modal "Dismiss / escalate flag". |
| AUD-11 | 03:199–200, 03:215; 08:53, 08:159 | **Không định nghĩa Approved vs PartiallyApproved.** Golden case (trả 800.000/1.400.000) là Approved hay Partial? Recommendation type có ràng buộc với số tiền không? | Thêm **BR-13**: *Approved* khi payable = claimed total; *PartiallyApproved* khi 0 < payable < claimed; *Rejected* khi payable = 0 (có reason code). Golden case → PartiallyApproved. Recommendation type được hệ thống gợi ý theo số tiền; lệch phải có reason. |
| AUD-12 | 03:224; 08:129, 08:161 | **Nhiều claim pending cùng "projected" một số dư** → duyệt lần lượt có thể vượt hạn mức (research: "prevent overpayment"). US20 trừ số dư khi duyệt nhưng không kiểm lại. | Mở rộng BR-10: khi duyệt, hệ thống kiểm lại remaining balance; nếu đã thay đổi so với calculation revision → chặn duyệt, yêu cầu tính lại (revision mới). Thêm AC vào US20. |
| AUD-13 | 03:202; 08:155 | **Trạng thái `Closed` không có chủ.** Không UC/story/AC nào mô tả ai/cái gì chuyển sang `Closed`, có mở lại được không, có ảnh hưởng số dư không. Research map không có trạng thái này. | Thêm vào UC09 alt + AC US19: *"Given deadline passed without new documents, then a scheduled job moves the claim to `Closed`, records an audit event; reviewer can extend the deadline before it passes."* Nhãn lifecycle: "deadline passed (scheduled job)". |
| AUD-14 | 03:171, 03:196, 03:248; 03:104 & puml:57–58; 04:134–137, 04:307 | **Bổ sung chứng từ (`NeedsInformation → Processing`) không có người, màn hình, story.** Matrix cho Reviewer UC03 (re-upload) nhưng cả 2 diagram thiếu Reviewer–UC03; SCR-13 chỉ là stepper tạo claim mới; Intake home không có tab "Needs information"; API `uploadDocument` chỉ nằm ở Intake. | Chốt: **Intake Officer** nhận chứng từ bổ sung qua tab mới "Needs information" ở SCR-10 (route `/intake?tab=needs-info`) → upload vào claim có sẵn; *hoặc* Reviewer "Add documents" ở SCR-31. Thêm AC vào US04, association trong diagram tương ứng. |
| AUD-15 | 03:194; 04:27–28, 04:308; 08:36 | **Phân công claim và Review queue không có story.** Queue "Assigned to me" phụ thuộc cơ chế gán (Feature map: assign/reassign, priority) nhưng không UC/story nào định nghĩa; trigger `ReadyForReview → InReview: reviewer opens case` mơ hồ (mở để xem có phải là nhận việc?). | Thêm **US25** (Must, 3 pts): *"As a Claims Reviewer, I want to pick up the next claim from the review queue sorted by priority so that work is not duplicated"* — AC: tabs, sort, self-assign ("Start review") → `InReview`, một claim một assignee. Trigger lifecycle → "UC04/UC05 reviewer starts review (assign)". Must thành 88 (hoặc 91 nếu nâng cả US22). |
| AUD-16 | 03:233; 04:86, 04:90; 08:36, 08:58 | **Home của OPS_MANAGER là Operations dashboard (US23 – Should)**, nhưng US01 (Must) đưa Ops về home đó → bản release Must-only không có màn hình home cho Ops. | Phương án A: tách US23 → *US23a Operations dashboard cơ bản* (Must, 3 pts: counts by status, backlog age) + *US23b AI quality & export* (Should). Phương án B: home tạm của Ops trong MVP = Audit log. |
| AUD-17 | 03:17, 03:157; 04:252; 08:93–94, 08:118 | **"Tự động chấp nhận" chưa được định nghĩa chặt** — field ≥ 0.90 và mapping ≥ 0.85 đi thẳng vào tính tiền mà không người nào thao tác. Thuật ngữ không thống nhất: "suggested-accepted" (03:157), "accepted automatically" (03:17), "High (accepted)" (04:252). US06 **không có AC cho nhánh ≥ 0.90**. | Định nghĩa một thuật ngữ: **"Auto-accepted suggestion"** — đủ điều kiện cho gating, *vẫn giữ AI marker* tới khi người chạm vào, audit ghi `acceptedBy = threshold`. Thêm AC US06 (≥ 0.90, money/identity ≥ 0.95). Thêm AC US18: khi submit, reviewer xác nhận đã xem các giá trị auto-accepted dùng trong tính tiền (attestation). |
| AUD-18 | 03:155–162, 03:217 | **Ngưỡng không gom về một chỗ.** BR-03 chỉ nêu 0.70/0.90/0.95, thiếu 0.80 (page) và 0.85 (mapping); "0.70–0.90" không rõ biên; danh sách field money/identity chỉ ở 03:162; không nói ai cấu hình ngưỡng. | Biến BR-03 thành **bảng threshold registry**: page < 0.80 → Unclassified; field ≥ 0.90 auto-accepted, [0.70; 0.90) Needs verification, < 0.70 empty; money/identity (insured name, ID no., policy no., invoice total, line amount) ≥ 0.95; mapping < 0.85 confirm. Cấu hình seed, không có UI trong MVP. |
| AUD-19 | 03:219; 08:124; 03:317 | **BR-05 chỉ có Covered/Excluded** nhưng US12 có thêm **Partially covered**; công thức chỉ dùng "remaining limit" trong khi UC11 cấu hình per-visit **và** annual limit; "80% coinsurance" dễ hiểu ngược (công ty trả 80% hay khách trả 80%). | BR-05: line result ∈ {Covered, Partially covered, Excluded} + rule code; *remaining limit = min(per-visit limit, annual remaining)*; ghi rõ "reimbursement rate 80% (insured co-pays 20%)". |
| AUD-20 | 03:229–333 | **Template đặc tả UC không đồng nhất.** Thiếu Precondition ở UC01, UC03–UC14; thiếu Postcondition ở UC01, UC03, UC07, UC11–UC14; thiếu Goal ở UC12–UC14; không UC nào có Trigger; Exception gần như vắng. Thiếu exception quan trọng: UC06 *không có rule-set version hiệu lực vào service date*; UC02 *member không tìm thấy / policy hết hiệu lực*; UC03 *file sai định dạng/quá 20 MB*; UC05 *AI-03 lỗi*; UC08 *còn High flag*. | Áp một template cho mọi UC: ID · Actor · Secondary · Goal · Trigger · Pre · Post (success / failure) · Main · Alt · Exception · Rules · Stories · Screens. Bổ sung các exception nêu trên. |
| AUD-21 | 08:34–60, 08:211 | **Story không tham chiếu BR** → không truy được BR ↔ story; BR-09 chỉ được UC10 dùng, BR-11 chỉ UC01 (sign-in không phải nơi BR-11 cần thực thi). | Thêm cột **BR** vào backlog; gắn BR-11 cho UC04, UC12, UC13; ma trận ở mục 3 là bản đề xuất. |
| AUD-22 | 08:16; 08:70–175 | **AC phần lớn không ở dạng Given/When/Then** dù 08:16 khẳng định vậy (chỉ US01, US03, US05, US06, US07, US09, US10, US13, US15, US20 có G/W/T, và cũng chỉ một phần). Một số AC **không đo được**: US05 "blurry or cropped", US10 "low clinical relevance", US14 "within the same screen", US23 "AI correction rate" (chưa định nghĩa công thức). | Viết lại toàn bộ AC theo G/W/T; thay cụm mơ hồ bằng tiêu chí đo: quality score < ngưỡng cấu hình; clinical relevance score < ngưỡng + đo false-positive trên golden set; định nghĩa *correction rate = field bị sửa / field AI đề xuất*. |
| AUD-23 | 08:177–204 | **Bảng tự chấm INVEST toàn ✓**, không phản ánh thực tế (US06/US21 quá lớn, US15 gộp 2 role, US10 khó test, US20 thiếu định nghĩa partial…). | Thay bằng bảng chấm ở mục 4 (✓/⚠/✗ + lý do). Cân nhắc tách US21 (configure draft 5 pts / publish & versioning 3 pts). |

### 2.3 Minor

| ID | File:dòng | Mô tả | Đề xuất sửa |
|---|---|---|---|
| AUD-24 | 03:39–45, 03:95–103 | Mermaid **không vẽ generalization** về Staff (chỉ có câu chữ 03:138); actor dùng cùng hình stadium như UC; Policy Admin/Ops/Sys Admin (primary) đứng chung cột phải với secondary actor. | Thêm cạnh `intake & reviewer & … -->|«is a»| staff`; actor dùng hình khác (ví dụ `[["👤 …"]]`); primary bên trái, chỉ AI service bên phải. |
| AUD-25 | 03:61–62, 03:74, 03:77 | Tên UC: "Validate claim" là hành động **của hệ thống**; "Adjudicate claim" — rule engine mới adjudicate; "Manage …" là dạng CRUD. UC01 Sign in là subfunction. | Đề xuất: UC05 → *Resolve validation alerts*; UC06 → *Review benefit adjudication*; UC11 → *Publish benefit rule-set version*; giữ UC14 và UC01 (ghi chú UC01 là subfunction/precondition). |
| AUD-26 | 03:243 | "will be **rejected by rules**" mâu thuẫn BR-01 (rule chỉ cho line result Excluded). | "→ rule engine marks all lines Excluded (rule code COV-01); rejection still requires a Senior Approver." |
| AUD-27 | 03:242; 08:76 | UC02 nhập *claimed total, channel* và hiển thị *remaining outpatient balance*, US03 AC không có. | Bổ sung vào AC US03. |
| AUD-28 | 03:269; 08:109 | Khoá duplicate khác nhau: UC05 "invoice number not used", US09 "invoice no. + provider + amount". | Dùng một khoá: *invoice no. + provider tax code* = exact duplicate; *provider + date + amount* = near-duplicate. |
| AUD-29 | 03:155; 08:86 | Trang Unclassified: US05 bắt Intake chọn loại; 03 nói Reviewer chọn ở UC04. | Intake *có thể* chọn; nếu bỏ trống, Reviewer bắt buộc chọn ở UC04; không chặn submit. |
| AUD-30 | 04:292 | "UI display language … is a pending decision" — đã chốt **English** cho nhãn (05:25). | Sửa thành "UI labels: English; sample data stays Vietnamese". |
| AUD-31 | 03:26; 04:50, 04:151 | Actor table nói Policy Admin "maintains … member policy data" nhưng không UC/story nào; SCR-40 gán US03 (story của Intake), cột Role thiếu Intake (read). | Ghi rõ: member/policy là **seed data** trong MVP; SCR-40 read-only; sửa role "Policy Admin, Intake (read)". |
| AUD-32 | 04:33, 04:141–147 | Sitemap cho Intake/Ops đọc Claim workspace nhưng SCR-30…36 và role matrix không phản ánh. | Đồng bộ theo AUD-01 (Ops masked; Intake chỉ xem Overview claim mình tạo hoặc bỏ). |
| AUD-33 | 04:136, 04:159; 02_Feature_Map | SCR-22 Search và SCR-70 Profile không có story riêng; một số chức năng trong Feature map không được nhắc: change password, decision letter, đối chiếu đơn thuốc ↔ dòng thuốc, demo benefit ledger. | Thêm mục "Out of MVP" liệt kê rõ; gắn SCR-22 vào US25 (queue & search). |
| AUD-34 | 03:279; 08:128–129 | Ví dụ 03 không ghi rõ remaining limit = 800.000; US13 thiếu breakdown từng dòng (200k/500k/400k/300k) và số dư sau dự kiến = 0. | Bổ sung cho golden case đủ để test từng dòng. |
| AUD-35 | 03:191, 03:193–196; 04:28 | Nhãn `Processing → NeedsVerification` thiếu nhánh "AI unavailable"; không có đường `ReadyForReview → NeedsInformation`; tab "Returned" không phải trạng thái. | Bổ sung nhãn; định nghĩa "Returned" = `InReview` có cờ returned. |
| AUD-36 | 03:223 | BR-09 nhắc "high-value threshold" chưa có giá trị và trùng nghĩa BR-01 (MVP mọi claim đều qua Approver). | Đổi BR-09 thành quy tắc **priority** (amount ≥ X, open High flag, age) và cho X một giá trị seed. |
| AUD-37 | 08:146 | Override không có giới hạn → có thể vượt claimed amount hoặc remaining limit (bỏ qua BR-10). | AC: override amount ≤ claimed line amount; payable sau override vẫn bị cap bởi remaining limit. |
| AUD-38 | puml:22, puml:31–54 | `.puml` không khai báo extension points cho UC04/UC08; AISvc nằm chung phía actor chính. Không render được tại máy (không có Java/PlantUML) — chỉ kiểm tra cú pháp bằng mắt. | Thêm extension points, đặt AISvc sau rectangle để nằm bên phải. |

## 3. Traceability matrix

### 3.1 Use case ↔ story ↔ BR ↔ màn hình

| Use case | Stories | BR (hiện tại) | Màn hình / route | Lỗ hổng |
|---|---|---|---|---|
| UC01 Sign in | US01 | BR-11 | SCR-01 `/sign-in`, SCR-90 `/403` | Ops home phụ thuộc story Should (AUD-16) |
| UC02 Register claim | US03 | — | SCR-10 `/intake`, SCR-11/12 `/intake/new`, SCR-40 `/members` | Không BR; thiếu exception (AUD-20, AUD-27) |
| UC03 Upload documents | US04, US05, US06 | BR-02, BR-03 | SCR-13 `/intake/new` | Không màn hình bổ sung chứng từ (AUD-14); timing AI-01 (AUD-02) |
| UC04 Verify evidence | US06, US07 | BR-02, BR-03, BR-07 | SCR-31 `/claims/:id/evidence` | Extension point chưa khai báo (AUD-03) |
| UC05 Validate claim | US08, US09, US10 | BR-07, BR-08 | SCR-32 `/claims/:id/validation` | Alteration không story (AUD-09); alert vs flag (AUD-08) |
| UC06 Adjudicate claim | US11, US12, US13, US14 | BR-04, BR-05, BR-10 | SCR-33 `/claims/:id/adjudication` | Partially covered (AUD-19) |
| UC07 Review risk flags | US15 | BR-07, BR-08 | SCR-34 `/claims/:id/risk` | Quyền escalate (AUD-10) |
| UC08 Submit recommendation | US16, US17, US18 | BR-06, BR-07 | SCR-35 `/claims/:id/decision`, SCR-38 drawer, SCR-30 | Attestation auto-accept (AUD-17) |
| UC09 Request information | US19 | — | SCR-37 modal | `Closed` (AUD-13); AI-04 (AUD-07); Approver (AUD-06) |
| UC10 Decide claim | US20 | BR-01, BR-06, BR-09, BR-10 | SCR-21 `/approvals`, SCR-39 modal | Partial (AUD-11); kiểm lại số dư (AUD-12) |
| UC11 Benefit rules | US21, US22 | BR-04 | SCR-41 `/plans`, SCR-42 `/plans/:id/versions/:v`, SCR-43 `…/test` | Include vs Should (AUD-04) |
| UC12 Monitor ops & AI | US23 | — | SCR-50 `/insights/operations`, SCR-51 `/insights/ai-quality` | Chỉ có story Should (AUD-16) |
| UC13 Trace history | US24 | — | SCR-36 `/claims/:id/timeline`, SCR-52 `/insights/audit`, SCR-22 `/claims` | BR-11 (AUD-01) |
| UC14 Staff access | US02 | — | SCR-60 `/admin/staff` | Nên gắn BR-11, BR-12 |
| AI-01 | US05, US06, US07 (re-run) | BR-02, BR-03 | SCR-13, SCR-31 | — |
| AI-02 | US11 | (nên BR-03) | SCR-33 | — |
| AI-03 | US10 | BR-08 | SCR-32 → nên SCR-34 | Alteration (AUD-09) |
| AI-04 | US16 | — | SCR-35 | UC09 không include (AUD-07) |
| SYS-01 | US12, US13, US22 | BR-04, BR-05 | SCR-33, SCR-43 | — |
| SYS-02 | — | (08:211 gán sai BR-07) | — | Không phải UC (AUD-05) |
| *(không UC)* | — | — | SCR-20 `/queue` (gán US01, US18) | Queue & assignment không story (AUD-15) |
| *(không UC)* | — | — | SCR-70 `/profile` | Ngoài MVP hay không (AUD-33) |

### 3.2 BR → nơi tham chiếu

| BR | UC tham chiếu | Story tham chiếu | Nhận xét |
|---|---|---|---|
| BR-01 | UC10 | — (US20 ngầm) | Thêm UC02 alt (AUD-26) |
| BR-02 | UC03, UC04 | — (US06 ngầm) | |
| BR-03 | UC03, UC04 | — (US05, US06, US11 ngầm) | Thiếu 0.80, 0.85 (AUD-18) |
| BR-04 | UC06, UC11 | — (US12 ngầm) | |
| BR-05 | UC06 | — (US12, US13 ngầm) | Thiếu Partially covered (AUD-19) |
| BR-06 | UC08, UC10 | — (US20 ngầm) | |
| BR-07 | UC04, UC05, UC07, UC08 | 08:211 (dùng sai nghĩa) | AUD-05 |
| BR-08 | UC05, UC07 | — (US15 ngầm) | |
| BR-09 | UC10 | — (US18 ngầm) | Ngưỡng chưa định nghĩa (AUD-36) |
| BR-10 | UC06, UC10 | — (US13, US20 ngầm) | Kiểm lại khi duyệt (AUD-12) |
| BR-11 | UC01 | — | Cần ở UC04, UC12, UC13 (AUD-01) |
| *BR-12 (đề xuất)* | Audit cho mọi thay đổi | | AUD-05 |
| *BR-13 (đề xuất)* | Định nghĩa outcome | | AUD-11 |

### 3.3 Lifecycle: trạng thái ↔ UC ↔ story

| Trạng thái | Vào từ (UC) | Story có AC | Màn hình | Ghi chú |
|---|---|---|---|---|
| Draft | UC02 | US03 | SCR-10/11/12 | ✓ |
| Processing | UC03; NeedsInformation | US04, US19 | SCR-13 | Re-upload thiếu (AUD-14) |
| NeedsVerification | AI-01 | US06 | SCR-31, SCR-20 tab | Nhánh AI lỗi thiếu nhãn (AUD-35) |
| ReadyForReview | UC04 / AI-01 | US07 | SCR-20 | |
| InReview | "reviewer opens case"; UC10 return | US20 (return) | SCR-30…35 | Trigger mơ hồ (AUD-15) |
| NeedsInformation | UC09 | US19 | SCR-37, SCR-20 tab | Approver path (AUD-06) |
| PendingApproval | UC08 | US18 | SCR-21 | |
| Approved / PartiallyApproved / Rejected | UC10 | US20 | SCR-39 | Định nghĩa (AUD-11) |
| Closed | "no response by deadline" | **không có** | — | AUD-13 |

## 4. Chấm INVEST từng story

Ký hiệu: ✓ đạt · ⚠ đạt một phần · ✗ không đạt.

| Story | UC | I | N | V | E | S | T | Format | Nhận xét |
|---|---|---|---|---|---|---|---|---|---|
| US01 | UC01 | ⚠ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Role home của Ops chưa có trong Must (AUD-16). |
| US02 | UC14 | ✓ | ✓ | ✓ | ✓ | ✓ | ⚠ | ✓ | AC không G/W/T; thiếu tham chiếu audit BR-12. |
| US03 | UC02 | ✓ | ✓ | ✓ | ✓ | ✓ | ⚠ | ✓ | Thiếu claimed total, channel, balance (AUD-27). |
| US04 | UC03 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Chưa có AC bổ sung chứng từ (AUD-14). |
| US05 | UC03/AI-01 | ⚠ | ✓ | ✓ | ✓ | ✓ | ⚠ | ✓ | Thời điểm chạy (AUD-02); "blurry" không đo được. |
| US06 | UC03/AI-01 | ✓ | ✓ | ✓ | ⚠ | ⚠ | ⚠ | ✓ | 8 pts cho 4 loại chứng từ; thiếu AC ≥ 0.90 (AUD-17). Có thể tách invoice/receipt vs prescription/diagnosis. |
| US07 | UC04 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Re-run AI nằm trong story không gắn 🟣; thiếu fallback khi AI tắt. |
| US08 | UC05 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Tốt; tolerance 0 VND rõ. |
| US09 | UC05 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Khoá trùng lệch với UC05 (AUD-28); alert vs flag (AUD-08). |
| US10 | UC05/AI-03 | ✓ | ✓ | ✓ | ⚠ | ✓ | ⚠ | ✓ | "low clinical relevance" không đo được (AUD-22). |
| US11 | UC06/AI-02 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Mapping ≥ 0.85 tự áp dụng — cần attestation (AUD-17). |
| US12 | UC06/SYS-01 | ✓ | ✓ | ✓ | ✓ | ⚠ | ✓ | ✓ | 8 pts; "Partially covered" chưa có trong BR-05 (AUD-19). |
| US13 | UC06/SYS-01 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Golden case đúng; nên thêm breakdown dòng (AUD-34). |
| US14 | UC05+UC06 | ⚠ | ✓ | ✓ | ⚠ | ✓ | ⚠ | ✓ | Cắt ngang 2 UC; "within the same screen" mơ hồ. |
| US15 | UC07 | ✓ | ✓ | ✓ | ⚠ | ⚠ | ⚠ | ⚠ | Gộp 2 role trong 1 story; mâu thuẫn quyền escalate (AUD-10). |
| US16 | UC08/AI-04 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Tốt: citation bắt buộc + fallback. |
| US17 | UC08 | ✓ | ✓ | ✓ | ✓ | ✓ | ⚠ | ✓ | Thiếu giới hạn override (AUD-37). |
| US18 | UC08 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Nên thêm attestation (AUD-17). |
| US19 | UC09 | ✓ | ✓ | ✓ | ✓ | ✓ | ⚠ | ✓ | Thiếu `Closed`, AI-04 (AUD-13, AUD-07). |
| US20 | UC10 | ✓ | ✓ | ✓ | ✓ | ✓ | ⚠ | ✓ | Thiếu định nghĩa partial, kiểm lại số dư (AUD-11, AUD-12). |
| US21 | UC11 | ✓ | ✓ | ✓ | ⚠ | ✗ | ✓ | ✓ | 5 loại rule + clause + versioning + publish trong 8 pts — nên tách. |
| US22 | UC11/SYS-01 | ✓ | ✓ | ✓ | ✓ | ✓ | ⚠ | ✓ | Ưu tiên lệch với include (AUD-04); AC không G/W/T. |
| US23 | UC12 | ✓ | ✓ | ✓ | ⚠ | ⚠ | ⚠ | ✓ | 2 màn hình + export trong 5 pts; metric chưa định nghĩa. |
| US24 | UC13 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ⚠ | Persona "Ops Manager or Senior Approver" khác UC13 (AUD-01). |

**Format "As a … I want … so that …":** cả 24 story đúng cấu trúc. US15 và US24 gộp 2 persona bằng "or" — nên tách hoặc chọn persona chính.

**Điểm & ưu tiên:** Must 85 / Should 24 — **đúng**. Nếu chấp nhận đề xuất: + US25 (Must 3), US22 → Must (+3), US23 tách (Must 3 / Should 2) → Must ≈ 94, Should ≈ 18.

## 5. Kết luận

Bộ tài liệu có **xương sống tốt**: ranh giới AI / Rule Engine / Human rõ, AI đặt đúng vào các bài toán perception & ngôn ngữ (phân loại, trích xuất, mapping thuật ngữ, phát hiện bất thường, soạn tóm tắt), tiền và eligibility hoàn toàn deterministic, maker–checker có mặt; ngưỡng confidence và golden case nhất quán giữa 03 và 08; chiều `«include»`/`«extend»` đúng chuẩn UML; tổng điểm chính xác.

Các vấn đề chính tập trung ở **ba nhóm**:

1. **Phân quyền & dữ liệu nhạy cảm** — Sys Admin/Ops xem nội dung y tế (AUD-01, Critical).
2. **Lifecycle chưa khép kín** — re-upload, `Closed`, approver request info, phân công/`InReview`, định nghĩa Partial, kiểm lại số dư khi duyệt (AUD-06, 11–16).
3. **Mô hình UML & đặc tả** — SYS-02 không phải UC, include bắt buộc vs story Should, extension point chưa khai báo, alert vs flag, template UC thiếu pre/post/exception, AC chưa G/W/T (AUD-03–05, 08, 20–23).

Sau khi sửa nhóm Critical + Major, tài liệu đủ điều kiện làm đầu vào cho ERD và thiết kế API.

## 6. Kết quả xử lý (31/10/2026)

**Quyết định của user:** sửa **tất cả** findings; UC09 chỉ Reviewer, **không AI**; chứng từ bổ sung do **Intake Officer** upload; backlog chỉ **thêm US25** (không nâng US22, không tách US23, không tách US21).

| ID | Kết quả | Thay đổi chính |
|---|---|---|
| AUD-01 | Đã sửa | BR-11 viết lại (masked cho Ops, Sys Admin không xem nội dung claim); bỏ Sys Admin khỏi UC13 trên cả 2 diagram và matrix; US02 thêm access log; US24 persona Senior Approver + AC masked; IA sitemap/sidebar/SCR-30, 36, 52. |
| AUD-02 | Đã sửa | UC03 tách *AI-01 page check* (trước submit) và *AI-01 extraction* (sau submit); US05 ghi "before submission". |
| AUD-03 | Đã sửa | Nhóm "AI-assisted (included / extending)"; extension point *Page re-read* trong UC04 (spec, Mermaid, `.puml`); nhãn `[page misread]`. |
| AUD-04 | Đã sửa (phương án B) | `SYS-01 «extend» UC11 [test run requested]` tại extension point *Test draft*; US22 giữ Should. |
| AUD-05 | Đã sửa | Xoá SYS-02 khỏi diagram; thêm BR-12 (audit); 08 cross-cutting tham chiếu BR-12. |
| AUD-06 | Đã sửa (phương án B) | UC09 chỉ Reviewer; Approver dùng Return kèm comment; IA approver flow, SCR-37. |
| AUD-07 | Đã sửa | UC09 dùng template, không AI; bỏ citation chips cho request letter; 03 liệt kê "request letters" vào nhóm no-AI. |
| AUD-08 | Đã sửa | BR-08 định nghĩa Alert vs Risk flag; US09 near-duplicate → Risk flag; US10 → Risk flag; SCR-32/SCR-34 cập nhật; bảng thuật ngữ IA. |
| AUD-09 | Đã sửa | Document-alteration chuyển sang later phase; AI-03 đổi tên "Detect clinical inconsistencies". |
| AUD-10 | Đã sửa | Reviewer: dismiss / keep; Approver: escalate (UC07, US15, US20, IA flow, modal, API). |
| AUD-11 | Đã sửa | BR-13 định nghĩa outcome; lifecycle ghi điều kiện; US18, US20. |
| AUD-12 | Đã sửa | BR-10 kiểm lại số dư khi duyệt; AC US20; IA approver flow. |
| AUD-13 | Đã sửa | Lifecycle "deadline passed (scheduled job)"; UC09 exception; AC US19 (gia hạn, Closed). |
| AUD-14 | Đã sửa | Intake tab "Needs information" (`/intake?tab=needs-info`); UC03 trigger/alt; AC US19; Reviewer bỏ khỏi UC03. |
| AUD-15 | Đã sửa | Thêm **US25** (Must 3); lifecycle `ReadyForReview → InReview: assigned reviewer starts review (US25)`; queue tab "Available"; API `startReview`. |
| AUD-16 | Đã sửa (phương án B) | Ops home = Audit log trong bản Must-only (UC01, US01, IA). |
| AUD-17 | Đã sửa | Định nghĩa "auto-accepted suggestion" + attestation (03 principle, BR-03, US06, US11, US18, IA pattern). |
| AUD-18 | Đã sửa | Bảng threshold registry trong 03; BR-03 trỏ vào bảng. |
| AUD-19 | Đã sửa | BR-05: 3 loại line result, remaining limit = min(per-visit, annual − consumed), "reimbursement rate". |
| AUD-20 | Đã sửa | Template thống nhất cho UC01–UC14 kèm các exception còn thiếu. |
| AUD-21 | Đã sửa | Cột BR trong backlog; mỗi UC có dòng Rules / Stories / Screens. |
| AUD-22 | Đã sửa | Toàn bộ AC viết lại G/W/T; định nghĩa quality threshold, clinical threshold, công thức metric US23. |
| AUD-23 | Đã sửa | Bảng INVEST chấm lại ✓/⚠ có lý do; US06, US12, US21, US23 ghi ⚠ (giữ theo quyết định, là ứng viên tách). |
| AUD-24 | Đã sửa | Mermaid: cạnh "is a" về Staff (abstract), actor hình chữ nhật, association là đường thẳng, primary trái / secondary phải. |
| AUD-25 | Đã sửa | UC05 *Resolve validation alerts*, UC06 *Review benefit adjudication*, UC11 *Publish benefit rule-set version*; UC01 ghi chú subfunction. |
| AUD-26 | Đã sửa | UC02 alt không còn "rejected by rules". |
| AUD-27 | Đã sửa | AC US03 có claimed total, channel, balance. |
| AUD-28 | Đã sửa | Khoá exact = invoice no. + provider; near = provider + date + amount. |
| AUD-29 | Đã sửa | Intake *có thể* chọn loại trang; Reviewer *bắt buộc* nếu còn Unclassified. |
| AUD-30 | Đã sửa | 04: nhãn UI English. |
| AUD-31 | Đã sửa | Policy Admin: member/policy là seed data; SCR-40 read-only, role thêm Intake (read). |
| AUD-32 | Đã sửa | Claim workspace: "read: Ops, masked". |
| AUD-33 | Đã sửa | SCR-22 → US25; SCR-70 ghi "shell only; change password out of MVP". |
| AUD-34 | Đã sửa | Golden case theo từng dòng, số dư trước 800.000 / sau 0 (03 UC06, US13). |
| AUD-35 | Đã sửa | Nhãn "or AI unavailable"; định nghĩa Returned / Escalated; ghi chú request info cần `InReview`. |
| AUD-36 | Đã sửa | BR-09 thành quy tắc priority, ngưỡng seed 5.000.000 VND. |
| AUD-37 | Đã sửa | BR-07 + AC US17 giới hạn override. |
| AUD-38 | Đã sửa một phần | `.puml` có extension points, AISvc khai báo sau boundary. **Chưa render được PlantUML** vì máy không có Java/PlantUML — chỉ kiểm tra cú pháp bằng mắt. |

**Điểm sau sửa:** Must = 19 story / **88 pts**; Should = 6 story / **24 pts**.

## 7. Thay đổi sau audit — gộp còn 4 role (31/10/2026)

**Quyết định của user:** giảm từ 6 role xuống 4 (phương án B). Luồng xử lý claim (UC02–UC10) và maker–checker giữ nguyên.

| Trước | Sau | Lý do |
|---|---|---|
| Intake Officer | Intake Officer | Giữ nguyên. |
| Claims Reviewer | Claims Reviewer | Giữ nguyên. |
| Senior Approver + Operations Manager | **Senior Approver** (`SENIOR_APPROVER`) | Ops chỉ xem; Approver là cấp quản lý và đã được xem dữ liệu y tế, nên không cần chế độ masked riêng. Approver nhận UC12 (dashboard, AI quality) và audit log claim events. |
| Policy Administrator + System Administrator | **Administrator** (`ADMINISTRATOR`) | Cả hai chỉ cấu hình hệ thống, không xử lý claim. Administrator nhận UC11 và UC14, không xem nội dung claim. |

Ràng buộc mới đi kèm:
- Administrator không được tự đổi role hay tự khoá tài khoản của mình (UC14, AC US02), vì một người nay vừa sửa được rule set vừa cấp được quyền.
- Test run rule set (UC11 *Test draft*, US22) chỉ dùng **synthetic sample claims**, để Administrator không thấy nội dung claim thật (BR-11).

Các kết quả xử lý trước bị thay thế:
- **AUD-01:** phần "Ops xem masked" không còn; BR-11 chỉ còn Intake (claim mình tạo), Reviewer được giao và Senior Approver xem dữ liệu y tế; Administrator không xem nội dung claim.
- **AUD-16:** không còn vấn đề home của Ops; mọi role đều có home nằm trong bản Must (Approver → Approval queue, Administrator → Benefit plans).
- **AUD-31, AUD-32:** "Policy Admin" đổi thành Administrator; Claim workspace không còn quyền "read: Ops, masked".

File đã cập nhật: README, 03 (actor, diagram, matrix, BR-11, UC01, UC06, UC11–UC14), `use-case.puml`, 04 (sitemap, sidebar, screen inventory, API), 08 (US01, US02, US21–US24, ràng buộc chung), stitch-prompts, `scr-50-operations.html` và `.png` (chụp lại từ HTML), `02_Feature_Map.xmind` (nhánh 01, 05, 09 và thumbnail), `docs/images/feature-map.png`. `01_Product_Research.xmind` giữ nguyên 6 nhóm người dùng vì là tài liệu nghiên cứu đầu vào.

## 8. Feature map viết lại (31/10/2026)

Feature map cũ (9 nhánh, khoảng 82 mục) được làm từ giai đoạn research, còn khoảng 10 mục nằm ngoài MVP hoặc lệch quy tắc (giao lại claim, sửa priority bằng tay, phát hiện chứng từ bị sửa, thư quyết định, đổi mật khẩu…). Bản mới giữ template XMind 3 tầng: **sản phẩm → 11 nhóm chức năng → 45 chức năng**, nội dung tiếng Anh, chỉ gồm chức năng thuộc MVP. Điều này cũng đóng phần còn lại của AUD-33.

File: `02_Feature_Map.xmind`. Ảnh `docs/images/feature-map.png` cần export lại từ XMind.
