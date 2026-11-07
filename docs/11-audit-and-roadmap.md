# Audit toàn bộ (tài liệu + code) và kế hoạch code chức năng — ClaimLens

> Ngày audit: 07/11/2026 · Nhánh: `feat/us19-rule-sets` (commit `7dcf716`) · Phạm vi: toàn bộ `docs/`, `README.md`, `db/`, `apps/claims-api`, `apps/document-ai`, `apps/web`, `e2e/`, `infra/`, `tools/`, `.github/workflows`.
>
> Audit trước ([09](09-audit-report.md)) chỉ xét tài liệu yêu cầu. Lần này xét cả code, và mọi lỗi logic nêu dưới đây đều đã được **chạy thử để xác nhận** (mục 3).

## 1. Tóm tắt

| | Kết quả |
|---|---|
| Đã xây | 2 / 23 story: **US01** đăng nhập và **US19** rule set (11 / 85 điểm Must). 12 / 59 operation có handler, 47 trả 501. |
| Kiểm tra tự động | **Tất cả xanh**: C# 289/289, Document AI 44/44 (ruff sạch), Web 98/98 + ESLint + `tsc`, `contract:lint`, `contract:check`, `schema:check`. E2E: pass trên CI; ở máy local stack Docker lên được nhưng thiếu trình duyệt Playwright (mục 3.4). |
| Lỗi logic code | **2 Major đã xác nhận** (F-01 thứ tự rule bỏ qua eligibility, F-02 tin mọi `X-Forwarded-For`), **1 Major tiềm ẩn** (F-03 audit đọc ghi sau khi dữ liệu đã gửi), 5 Minor. |
| Cần quyết định | 2 điểm nghiệp vụ chưa có trong tài liệu (Q-A trạng thái policy, Q-B publish lùi ngày). |
| Tài liệu | Nền tảng tốt và truy vết đủ; 8 chỗ lệch với code đã xây (D-01 … D-08). |

**Điểm làm tốt** (đã kiểm tra, giữ nguyên):

- Ranh giới AI / Rule Engine / Người được thực thi bằng code, không chỉ bằng chữ: Rule Engine không tham chiếu gì và bị `BannedApiAnalyzers` cấm đồng hồ, I/O, random; architecture test canh giữ.
- Một bảng policy sinh từ `x-claimlens` cho cả 59 operation; middleware chạy trước model binding; 501 cho operation chưa xây nên role, scope, status của cả 59 đã được test.
- DB lặp lại quy tắc quan trọng (trigger lifecycle, rule set đóng băng, usage khớp approval, audit chỉ insert) và `ConstraintErrorMap` có test hai chiều với hợp đồng.
- Drift được chặn bằng máy: `contract:check` (file sinh ra còn khớp), `schema:check` (DBML ↔ migrations), test so enum C# với `pg_enum`, test mọi response khớp YAML.
- Auth đúng thiết kế: refresh token xoay vòng, chỉ lưu hash, cửa sổ 10 giây cho 2 tab, tái dùng muộn thu hồi cả family, cookie `HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth`, kiểm `Origin`, thông báo lỗi đăng nhập không lộ trạng thái tài khoản.
- Golden case đúng từng bước (1.400.000 → 1.100.000 → 880.000 → 800.000) ở engine, fixture, seed và tài liệu.

## 2. Findings — code

Mức độ: **Major** = có thể trả sai tiền, lộ/giả mạo dữ liệu audit, hoặc chặn slice kế tiếp. **Minor** = phòng thủ nhiều lớp, độ bền, hoặc trải nghiệm.

| ID | Mức | Vị trí | Vấn đề | Đề xuất sửa |
|---|---|---|---|---|
| F-01 | **Major** (đã chạy thử) | [RuleEvaluator.cs:102](../apps/claims-api/src/ClaimLens.RuleEngine/RuleEvaluator.cs#L102), [RuleSetRules.cs:154](../apps/claims-api/src/ClaimLens.Api/Features/RuleSets/RuleSetRules.cs#L154), [rule-rows.ts:36](../apps/web/src/features/plans/editor/rule-rows.ts#L36) | Engine dừng ở rule **đầu tiên** theo `apply_order` quyết định được dòng. API nhận mọi `applyOrder` từ −32768 đến 32767, editor cho gõ tự do. Một rule `COVERAGE` đặt thứ tự trước `ELIG-01` hoặc `WAIT-30` làm dòng được **Covered và trả tiền** dù ngày khám ngoài thời hạn hợp đồng hoặc còn trong thời gian chờ. Publish vẫn qua vì chỉ kiểm mỗi category có rule. Đây đúng loại lỗi "engine lặng lẽ bỏ qua rule" mà US19 đã chặn cho các trường hợp lặp. | Hai lớp: (1) validation khi lưu và publish: `ELIGIBILITY` < `WAITING_PERIOD` < `EXCLUSION` < `COVERAGE` < `COINSURANCE` < `LIMIT` theo `apply_order`, lỗi `RULE_INVALID` tại `rules[i].applyOrder`; (2) engine áp thứ bậc theo loại rule rồi mới tới `apply_order` trong cùng loại, để dữ liệu cũ hoặc nhập thẳng DB cũng không lọt. Thêm test engine cho cả hai kịch bản ở mục 3.1. |
| F-01b | Minor | [RuleSetRules.cs:50](../apps/claims-api/src/ClaimLens.Api/Features/RuleSets/RuleSetRules.cs#L50), [030_plans_and_rules.sql](../db/seed/dev/030_plans_and_rules.sql) | `ELIG-01` do API thêm có order **0**, seed dùng **10**. Hai nguồn sự thật cho cùng một rule hệ thống. | Chốt một giá trị (0) và sửa seed bằng script seed mới; nếu F-01 áp thứ bậc theo loại thì giá trị này không còn ảnh hưởng tiền. |
| F-02 | **Major** (đã chạy thử) | [Startup.cs:68](../apps/claims-api/src/ClaimLens.Api/Startup.cs#L68) | `KnownIPNetworks` và `KnownProxies` bị xoá rỗng, và không cấu hình nào (appsettings, compose, CI) đặt `Proxy:KnownProxies`. Với cả hai danh sách rỗng, ASP.NET Core **tin `X-Forwarded-For` từ bất kỳ ai**. Hệ quả: (a) giả mạo `audit_event.ip_address`; (b) né giới hạn 20 lần sai/địa chỉ của `SignInThrottle` bằng cách đổi header mỗi lần. Trái với [10 §4](10-code-structure.md#what-happens-on-a-request): "trusted only from the known proxy". | Fail closed: chỉ bật forwarded headers khi có `Proxy:KnownProxies` hoặc `Proxy:KnownNetworks`; compose đặt mạng nội bộ của Caddy; thêm test "một caller trực tiếp gửi XFF không đổi được RemoteIpAddress". |
| F-03 | **Major** (tiềm ẩn, chặn Slice 1) | [OperationPolicyMiddleware.cs:36-41](../apps/claims-api/src/ClaimLens.Api/Http/OperationPolicyMiddleware.cs#L36), [ClaimScopeTests.cs:131](../apps/claims-api/tests/ClaimLens.Api.Tests/Authorization/ClaimScopeTests.cs#L131) | Audit cho thao tác **đọc** được ghi **sau** `await next(context)`, khi handler đã ghi body JSON và response đã bắt đầu gửi. Nếu insert audit lỗi, client vẫn đã nhận dữ liệu y tế, còn `ErrorMiddleware` bỏ qua vì `HasStarted`. Tài liệu hứa "If that insert fails, the request fails" (BR-12). Test hiện tại dùng handler giả **không ghi body** nên không thể phát hiện. Chưa gây hại vì chưa có operation `audit: read` nào được xây, nhưng Slice 1 có ngay 4 cái (`searchMembers`, `getPolicySummary`, `listDocuments`, `getPage`). | Với operation `audit: read`: đệm body (JSON nhỏ; riêng `getPage` là ảnh) rồi mới insert audit, thành công mới chép body ra; hoặc ghi audit trước khi trả kết quả trong cùng transaction. Thêm test: handler thật ghi body + audit insert bị ép lỗi → client nhận 500 và không có body. |
| F-04 | Minor | [0006_roles.sql:13](../db/migrations/0006_roles.sql#L13) | `grant … on all tables` chỉ áp cho bảng đang có. Không có `alter default privileges`, nên bảng hoặc sequence do migration sau tạo sẽ khiến app lỗi `permission denied`. | Migration `0007`: `alter default privileges for role <owner> in schema public grant select, insert, update, delete on tables to claimlens_app` (và `usage, select on sequences`); test "app role có quyền trên mọi bảng trừ `schema_migration`, không UPDATE/DELETE `audit_event`". |
| F-05 | Minor | [0003_triggers.sql:72](../db/migrations/0003_triggers.sql#L72) | Trigger chỉ đóng băng `PUBLISHED`. Ở mức DB, một version `DISCARDED` vẫn đổi được sang `DRAFT` hay `PUBLISHED`. API chỉ cho chuyển từ `DRAFT`, nên đây là lỗ phòng thủ nhiều lớp. | Trong `0007`: chỉ cho `DRAFT → PUBLISHED` và `DRAFT → DISCARDED`; map constraint mới sang `RULE_SET_NOT_DRAFT`. |
| F-06 | Minor | [ErrorMiddleware.cs:37](../apps/claims-api/src/ClaimLens.Api/Http/ErrorMiddleware.cs#L37) | Lỗi 500 trả body rỗng, trong khi mọi lỗi khác là `application/problem+json` có `code` và `requestId`. Web app không có `code` để hiển thị trạng thái lỗi. | Trả problem `INTERNAL_ERROR` (thêm vào contract nếu chưa có) với `requestId`. |
| F-07 | Minor | [Startup.cs:43-51](../apps/claims-api/src/ClaimLens.Api/Startup.cs#L43) | Không kiểm `Auth:SigningKey` lúc khởi động (rỗng hoặc ngắn → lần đăng nhập đầu mới 500); JWT không có `iss`/`aud`. | `ValidateOnStart` cho `AuthOptions` (key ≥ 32 byte, `AllowedOrigins` không rỗng); thêm issuer/audience. |
| F-08 | Minor | [SignInThrottle.cs:12](../apps/claims-api/src/ClaimLens.Api/AccessControl/SignInThrottle.cs#L12) | Từ điển đếm lần sai không bao giờ dọn; gửi email ngẫu nhiên liên tục làm bộ nhớ tăng mãi. | Dọn cửa sổ đã hết hạn khi ghi, hoặc dùng `MemoryCache` có hạn kích thước. |

### Quyết định nghiệp vụ

**Đã chốt (07/11/2026):**

- **Q-A:** thêm `policy.terminated_on` (bắt buộc khi status khác `ACTIVE`). `ELIG-01` coi policy hết hiệu lực từ ngày đó.
- **Q-B:** không cho publish với ngày hiệu lực trước hôm nay (giờ Việt Nam), và vẫn phải sau version hiện tại. Lý do: đây là cách đơn giản và an toàn nhất, và DB chưa có claim nào nên không có claim cũ bị ảnh hưởng. Cả hai quyết định được làm cùng task A.

Bảng dưới giữ câu hỏi gốc:

| ID | Câu hỏi | Hiện trạng | Đề xuất mặc định |
|---|---|---|---|
| Q-A | Policy `LAPSED` / `TERMINATED` thì dòng chi phí có bị Excluded không? | `ELIG-01` chỉ so ngày khám với `coverage_start`/`coverage_end`; [EvaluationInput](../apps/claims-api/src/ClaimLens.RuleEngine/Input/EvaluationInput.cs) không có trạng thái policy, DBML không có ngày chấm dứt. UC02 chỉ nói "policy lapsed → cảnh báo, vẫn đăng ký". Hiện một policy đã chấm dứt nhưng còn trong năm hợp đồng vẫn được trả. | Thêm `policy.terminated_on` (nullable); `ELIG-01` coi policy hết hiệu lực từ ngày đó. Cần trước Slice 4b. |
| Q-B | Publish rule set với ngày hiệu lực **trong quá khứ** có được không? | [PublishPlanVersion.cs:45](../apps/claims-api/src/ClaimLens.Api/Features/RuleSets/PublishPlanVersion.cs#L45) chỉ đòi sau ngày của version hiện tại. Publish lùi ngày sẽ đổi version áp cho claim đang xử lý có ngày khám sau đó (revision của chúng thành stale). | Cho phép (hợp đồng có thể sửa hồi tố) nhưng hiện cảnh báo trong hộp publish nêu số claim đang mở bị ảnh hưởng; hoặc cấm ngày < hôm nay. |

## 3. Bằng chứng

### 3.1 F-01 — chạy Rule Engine thật

Chương trình nháp tham chiếu `ClaimLens.RuleEngine`, policy năm 2026:

```text
Outside coverage, COV-01 order -5 vs ELIG-01 order 0 -> Covered by COV-01, payable 300000
In waiting period, COV-01 order 15 vs WAIT-30 order 20 -> Covered by COV-01, payable 300000
```

Đúng ra cả hai phải là `Excluded`, payable 0.

### 3.2 F-02 — chạy `ForwardedHeadersMiddleware` với cấu hình như `Startup.cs`

```text
Direct caller 203.0.113.9 sending X-Forwarded-For 198.51.100.77 -> RemoteIpAddress seen by the API: 198.51.100.77
```

### 3.3 Các bộ kiểm tra

| Bộ | Lệnh | Kết quả |
|---|---|---|
| Claims API + Rule Engine | `dotnet test ClaimLens.slnx` (Testcontainers PostgreSQL) | 289 passed |
| Document AI | `uv run ruff check`, `ruff format --check`, `pytest` (có Schemathesis) | sạch, 44 passed |
| Web | `npm run lint`, `tsc --noEmit`, `vitest run` | sạch, 98 passed |
| Hợp đồng và schema | `npm run contract:lint`, `contract:check`, `schema:check` | hợp lệ, file sinh ra khớp, migrations chứa đủ DBML |

### 3.4 E2E

`docker compose --profile full up -d --build` build đủ 3 image, migrate và seed chạy xong, `http://localhost:8088/sign-in` trả 200. Cả 17 test Playwright **không chạy được** trên máy này vì chưa tải trình duyệt Chromium của Playwright 1.64 (`browserType.launch: Executable doesn't exist`), không phải do code. Trên CI (cài trình duyệt bằng `npx playwright install --with-deps chromium`), workflow `e2e` của PR #2 tại commit `7dcf716` **pass**, cùng với `claims-api` và `web`.

## 4. Findings — tài liệu

| ID | Vị trí | Lệch | Sửa |
|---|---|---|---|
| D-01 | [10:5](10-code-structure.md), [c4/README:5](architecture/c4/README.md) | Vẫn ghi "Nothing here is built yet". | Ghi rõ đã xây US01, US19; trỏ tới mục 14. |
| D-02 | [10 §7](10-code-structure.md#7-database) | Nói cấu hình EF ở `Persistence/Configurations`; thực tế viết trong `ClaimLensDbContext`. | Sửa tài liệu, hoặc tách khi số bảng tăng (ghi ngưỡng). |
| D-03 | [10 §1, §3, §4, §10](10-code-structure.md), [README §3.3](../README.md) | `infra/converter`, `tools/golden`, `tests/…/Golden/` chưa có; "seven golden scenarios" nhưng mới có 2 (`excluded-line`, `limit-capped`) và chỉ chứa kỳ vọng của engine, chưa có `documents/`, `ai-responses/`. | Đánh dấu "chưa có, đến cùng slice X"; `infra/converter` → dùng image Gotenberg gốc. |
| D-04 | [10 §5 Routes](10-code-structure.md#routes), [04 SCR-42](04-information-architecture.md) | SCR-42 ghi chỉ Administrator; code và ma trận quyền README cho Reviewer, Approver xem chỉ đọc. | Ghi "Administrator (sửa), Reviewer và Approver (đọc)". |
| D-05 | [06:5](06-data-model.md), [06 §11](06-data-model.md#11-open-questions-and-assumptions) | "PostgreSQL 14 or later" trong khi compose và kiểm tra dùng 18; Q11 nằm sau Q12. | Ghi PostgreSQL 18; sắp lại thứ tự. |
| D-06 | [README Project progress](../README.md#project-progress), [09 AUD-38](09-audit-report.md) | PlantUML vẫn chưa render-check từ 31/10; chưa có NFR; chưa có class/sequence diagram. | Giữ việc mở nhưng gắn vào slice (NFR trước khi deploy; sequence diagram viết khi thiết kế mỗi slice). |
| D-07 | [10 §13 Versions](10-code-structure.md#versions) | Liệt kê TanStack Table 9.3 nhưng `apps/web` chưa cài. | Ghi "cài khi xây hàng đợi (Slice 3)". |
| D-08 | Toàn bộ `docs/` | 09 và một phần `stitch-prompts.md` viết tiếng Việt, phần còn lại tiếng Anh. | Chỉ ghi nhận; tài liệu audit giữ tiếng Việt theo tiền lệ của 09. |

## 5. Việc sửa đã tạo thành task

Ba task độc lập, mỗi task một worktree và một nhánh, không đụng chung file:

| Task | Gồm | File chính |
|---|---|---|
| **A. Thứ tự rule an toàn** | F-01, F-01b, Q-A, Q-B | `ClaimLens.RuleEngine`, `Features/RuleSets/*`, `apps/web/src/features/plans/editor/*`, `db/migrations/0008_*`, seed mới, DBML, `docs/03`, `docs/08`, khối `policy` trong `docs/06` |
| **B. Gia cố nền tảng API và DB** | F-02 … F-08 | `Startup.cs`, `Http/*`, `AccessControl/*`, `db/migrations/0007_*`, `infra/compose.yaml`, test tương ứng. Hai task dùng hai số migration khác nhau (0007, 0008) nên merge theo thứ tự nào cũng được |
| **C. Đồng bộ tài liệu với code** | D-01 … D-07 | `README.md`, `docs/04`, `docs/06`, `docs/10`, `docs/architecture/c4/README.md` |

Thứ tự merge khuyến nghị: B và A trước Slice 1 (Slice 1 cần F-03 và F-04), C bất cứ lúc nào.

## 6. Kế hoạch code chức năng thật

Còn **16 story Must / 74 điểm**. Chia 6 slice theo phụ thuộc giữa các story; mỗi slice chạy được đầu–cuối (API + worker + UI + test), giống cách US01 và US19 đã làm.

### 6.0 Trước khi bắt đầu

| Việc | Vì sao | Khi nào |
|---|---|---|
| Merge task A, B | F-03 chặn mọi thao tác đọc có audit; F-04 chặn bảng mới | Trước Slice 1 |
| ~~Trả lời Q-A, Q-B~~ | Đã chốt 07/11, làm trong task A | — |
| `tools/golden`: sinh tài liệu tổng hợp (HTML → PDF/PNG bằng headless Chrome) cho 7 kịch bản | Definition of Done chạy trên 7 golden case; Slice 1–2 cần file `clean`, `blurry` | Trong Slice 1 |
| Spike nhà cung cấp AI (tiếng Việt có dấu, bounding box, confidence có đỡ được ngưỡng 0.95) | [10 §6](10-code-structure.md#6-document-ai-service): "needs a spike before US05" | Song song Slice 1, xong trước Slice 2 |
| NFR tối thiểu (log, lưu giữ tài liệu, thời gian phản hồi) | Còn mở từ README | Trước lần deploy đầu |

### 6.1 Các slice

| Slice | Story (điểm) | Claims API | Worker / AI | Web | Hoàn thành khi |
|---|---|---|---|---|---|
| **1. Intake** | US02 (3), US03 (5) = 8 | `searchMembers`, `getPolicySummary`, `createClaim`, `updateClaim`, `uploadDocument`, `deleteDocument`, `listDocuments`, `getPage`, `getClaimStatus`, `listClaims` (view Intake), `getClaim` (scope `registered`), `submitClaim` (xếp hàng `FIELD_EXTRACTION`) | `DocumentStore` (S3), `DocumentPreparation`: kiểm magic bytes, Gotenberg cho DOCX/XLSX, PDFtoImage tách trang, xoay EXIF, thumbnail, worker `FOR UPDATE SKIP LOCKED` | SCR-10 Intake home, SCR-11/12 stepper tạo claim, SCR-13 upload có tiến độ và thumbnail | Golden `clean` đi từ tìm member tới `PROCESSING`; file sai loại/quá 20 MB/quá 30 trang bị từ chối đúng mã |
| **2. AI đọc tài liệu** | US04 (5), US05 (8) = 13 | `ClaimStatusMachine` (chỉ nó đổi `claim.status`) | `AiJobs`: hàng đợi `ai_run`, runner, `DocumentAiClient`, `Thresholds` (chỉ ở đây), applier cho `PAGE_CHECK` và `FIELD_EXTRACTION`, timeout → `FAILED` → fallback; Document AI: fake gateway phát lại `ai-responses` của golden theo hash ảnh trang; adapter nhà cung cấp thật sau spike | Trạng thái trang (loại, độ tin cậy, Low quality, Unclassified), banner fallback khi AI tắt | Golden `clean` → `READY_FOR_REVIEW`, `blurry` → `NEEDS_VERIFICATION`; tắt AI → `NEEDS_VERIFICATION` với ô trống |
| **3. Hàng đợi và Evidence** | US23 (3), US06 (5) = 8 | `listClaims` (4 view + `counts`), `startReview` (409 `CLAIM_ALREADY_ASSIGNED`), `getClaimByNumber`, `getClaim` (`allowedActions`, `blockers`), `listFields`, `updateField`, `acceptField`, `reclassifyPage`, `rerunExtraction`, `updateClaimFacts`, `addExpenseLine`, `removeExpenseLine`, `listExpenseLines` | `PAGE_REREAD` chỉ một trang, giữ field người đã xử lý | SCR-20, khung Claim workspace (header, tab, 3 vùng), `workspace/viewer` (ảnh + overlay + vẽ vùng), SCR-31, `components/ai` | Hai reviewer tranh một claim: một thắng, một 409; tab Available không có tên member (BR-11); sửa field lưu trước/sau và initials |
| **4a. Kiểm tra hợp lệ** | US07 (5), US08 (3) = 8 | `listAlerts`, `dismissAlert`, `revalidateClaim`; `ClaimValidator` mỗi check một file; khoá trùng hoá đơn = số hoá đơn + MST người bán (tên chuẩn hoá khi không có MST) | — | SCR-32 | Golden `missing-document`, `duplicate`, `conflicting` ra đúng alert; mismatch tổng tiền không dismiss được |
| **4b. Tính quyền lợi** | US10 (5), US11 (8), US12 (5), US13 (5), US16 (3) = 26 | `confirmMapping`, `runAdjudication`, `listCalculationRevisions`, `getCalculationRevision`, `overrideLine`, `removeOverride`; `EngineInputLoader` (chỉ giá trị đã chấp nhận), `InputHash`, `RevisionWriter`, stale | applier `BENEFIT_MAPPING` (≥ 0.85 tự chấp nhận, còn lại chờ xác nhận) | SCR-33 (công thức, dòng đổi được tô, stale, đang tính lại), SCR-38 drawer override | Golden `excluded-line`, `limit-capped` ra đúng 800.000 qua API; chạy lại cùng đầu vào cho kết quả giống hệt; sửa dữ liệu tạo revision mới |
| **5. Quyết định và truy vết** | US17 (3), US18 (5), US22 (3) = 11 | `getRecommendation` (`readiness`, `attestationItems`), `saveRecommendationDraft`, `submitRecommendation` (409 `ATTESTATION_OUTDATED`), `decideClaim` (BR-13, khoá policy, `BALANCE_RECHECK`), `returnClaim`, `getClaimTimeline` | — | SCR-21 Approval queue, SCR-35 (reviewer và approver), SCR-39, SCR-36 Timeline, SCR-30 Overview | Golden đi trọn tới `PARTIALLY_APPROVED`, số dư 5.000.000 v2; người nộp không tự duyệt được; số dư đổi giữa chừng tạo revision `BALANCE_RECHECK` |
| **6. Bản Should** | US09, US14, US15, US20, US21 = 17 | 9 operation `should` | `CLINICAL_CHECK`, `SUMMARY_DRAFT` | Tab Risk, nút AI draft, SCR-43 test run, SCR-50 | Bật `Release:IncludeShould`; demo đủ 4 năng lực AI |

Tổng Must còn lại: 8 + 13 + 8 + 8 + 26 + 11 = **74 điểm**. Slice 4b lớn nhất (26 điểm): nếu chậm, tách US13 + US16 thành Slice 4c.

### 6.2 Cách làm mỗi slice (giữ nguyên chuẩn của US01, US19)

1. Viết sequence diagram cho luồng chính của slice (bù dần mục còn mở "class and sequence diagrams").
2. API trước: handler theo `operationId`, mỗi operation một file, xoá dần `NotBuiltEndpoints`; test Features + Contract + Authorization + Audit.
3. Worker/AI với fake gateway và golden fixture; test bật và tắt AI.
4. Web: query hook trong `features/*`, trạng thái loading / empty / AI processing / AI unavailable / error; test Vitest + MSW.
5. E2E Playwright + axe cho luồng của slice.
6. Cập nhật `docs/10` mục 14 và README "Project progress" trong cùng PR.

## 7. Kết quả xử lý (07/11/2026)

Ba task chạy song song trên ba nhánh, rồi được review lại và kiểm trên bản gộp B → A → C.

| ID | Kết quả | Thay đổi chính |
|---|---|---|
| F-01 | Đã sửa | Engine quyết định từng dòng theo loại rule (eligibility → waiting period → exclusion → coverage), `apply_order` chỉ xếp trong cùng loại; `EngineVersion` 1.1.0. Lưu và publish từ chối thứ tự trái với thứ bậc đó (422 `RULE_INVALID` tại `rules[i].applyOrder`), để cột Order trong editor luôn đúng với cách rule được áp. Hai kịch bản ở mục 3.1 thành test. |
| F-01b | Đã sửa, khác đề xuất | `ELIG-01` dùng order **10** theo seed, không phải 0: GOLD-OP v3 đã publish nên trigger đóng băng rule của nó, không script seed nào đổi được mà không lách guard. |
| F-02 | Đã sửa | Chỉ bật forwarded headers khi có `Proxy:KnownProxies` hoặc `Proxy:KnownNetworks`. Profile `full` cố định mạng `10.201.88.0/24` và Caddy ở `10.201.88.10`. Chưa thử trên Docker Engine Linux. |
| F-03 | Đã sửa | Body của thao tác `audit: read` nằm trong bộ đệm tới khi audit lưu xong. Test mới giả lập server đã gửi byte ra ngoài, ép insert audit lỗi: client nhận 500 `INTERNAL_ERROR`, không có dữ liệu. |
| F-04 | Đã sửa | `0007`: default privileges cho bảng và sequence tạo sau; thu hồi quyền trên sequence của `schema_migration` và trên `schema_seed`. |
| F-05 | Đã sửa | `0007`: version sinh ra là `DRAFT`, chỉ chuyển sang `PUBLISHED` hoặc `DISCARDED`; `DISCARDED` bị đóng băng (`ck_rule_set_status_transition` → `RULE_SET_NOT_DRAFT`). |
| F-06 | Đã sửa | 500 trả `application/problem+json` với `INTERNAL_ERROR` và `requestId`; thêm mã vào contract. |
| F-07 | Đã sửa | `AuthOptions` kiểm lúc khởi động (key ≥ 32 byte, origin, issuer, audience); token có và được kiểm `iss`, `aud`. |
| F-08 | Đã sửa | `SignInThrottle` dọn cửa sổ hết hạn tối đa mỗi phút một lần. |
| Q-A | Đã làm | `policy.terminated_on` + `ck_policy_terminated_on` (migration `0008`, DBML); `EvaluationInput.TerminatedOn`; `ELIG-01` coi policy hết hiệu lực từ ngày đó. |
| Q-B | Đã làm | Publish từ chối ngày trước hôm nay (giờ Việt Nam), thông báo nêu ngày sớm nhất được nhận; hộp publish có `min` = hôm nay. DB không lặp lại kiểm tra này vì chỉ API có đồng hồ nghiệp vụ. |
| D-01 … D-07 | Đã sửa | README, `docs/04`, `docs/06`, `docs/10`, `docs/architecture/c4/README.md`. |
| D-09 (mới) | Đã sửa | Phát hiện khi review: `docs/10` §14 còn ghi `ELIG-01` order 0, bảng Configuration thiếu `Proxy` và issuer/audience của `Auth`. |

Kiểm trên bản gộp B → A → C (không conflict): C# 342/342, Web 101/101 + ESLint + `tsc`, `contract:check`, `schema:check` đều pass. E2E: session A báo 17/17 pass trên nhánh A với Docker stack đầy đủ; bản gộp chạy e2e trên CI của PR.

**Liên quan:** [Audit yêu cầu](09-audit-report.md) · [Code structure](10-code-structure.md) · [User stories](08-invest-requirements.md) · [API contract](07-api-contract.md)
