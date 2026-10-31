# INVEST User Stories — ClaimLens MVP

> Design status: TARGET MVP

Each story delivers one observable outcome for one role, can be built and demoed on its own (with the listed prerequisites seeded), and carries testable acceptance criteria. Stories tagged **🟣 AI** contain an AI capability; their criteria always state the confidence behaviour, the human checkpoint and the fallback. Thresholds and business rules (BR-xx) are defined in [Use cases](03-use-cases.md).

## INVEST checklist

| Principle | How this backlog applies it |
|---|---|
| **I**ndependent | Each story has a demo path using seeded data (members, policies, rule sets, sample claim packets). AI stories have a manual fallback so the workflow runs without them. |
| **N**egotiable | Layout, thresholds and wording are adjustable; the outcome, permissions and business rules are fixed. |
| **V**aluable | Every story names the reviewer, approver or business benefit — no purely technical stories. |
| **E**stimable | Inputs, outputs, roles and exceptions are explicit; story points use Fibonacci (1–8). |
| **S**mall | No story exceeds 8 points; larger capabilities are split (e.g. extraction vs. verification vs. re-run). |
| **T**estable | Criteria are written as Given / When / Then with measurable conditions and map to golden-case test claims. |

## Epics

| Epic | Goal | Use cases | Stories |
|---|---|---|---|
| E1 Access & routing | Right people see the right work | UC01, UC14 | US01, US02, US25 |
| E2 Intake | A claim packet becomes a case | UC02, UC03 | US03–US04 |
| E3 Document AI | Documents become trusted, sourced facts | UC03, UC04 | US05–US07 |
| E4 Validation | Problems surface before money is computed | UC05 | US08–US10 |
| E5 Adjudication | Explainable per-line payment proposal | UC06 | US11–US14 |
| E6 Risk | Anomalies reviewed, not auto-judged | UC07 | US15 |
| E7 Decision | Accountable, two-person decision | UC08–UC10 | US16–US20 |
| E8 Policy rules | Accurate, versioned coverage logic | UC11 | US21–US22 |
| E9 Oversight | Operations and audit visibility | UC12, UC13 | US23–US24 |

## Product backlog

| ID | User story | Value | Pts | Priority | Depends on | BR |
|---|---|---|---|---|---|---|
| US01 | As a **staff member**, I want to sign in and land on my role's home so that I start working on what is assigned to me. | Focused start | 3 | Must | Seeded accounts | BR-11, BR-12 |
| US02 | As an **Administrator**, I want to create, disable and assign a role to staff accounts and review the access log so that only authorised people handle medical data. | Access control | 3 | Must | US01 | BR-11, BR-12 |
| US03 | As an **Intake Officer**, I want to find a member and register a claim against their active policy so that the case starts with the correct coverage context. | Correct linkage | 3 | Must | Seeded members & policies | BR-01, BR-12 |
| US04 | As an **Intake Officer**, I want to upload PDF/JPG/PNG documents and submit them for processing so that the reviewer receives a complete packet. | Fast intake | 5 | Must | US03 | BR-12 |
| US05 | 🟣 AI · As an **Intake Officer**, I want each page automatically classified and checked for readability so that wrong or blurry documents are caught before submission. | Fewer re-requests | 5 | Must | US04 | BR-03 |
| US06 | 🟣 AI · As a **Claims Reviewer**, I want invoice, receipt, prescription and diagnosis fields extracted with their source region so that I don't retype data and can see where each value came from. | Less data entry, traceability | 8 | Must | US05 | BR-02, BR-03 |
| US07 | 🟣 AI · As a **Claims Reviewer**, I want to verify low-confidence fields side by side with the highlighted source and correct them so that every claim fact is trustworthy. | Reliable facts | 5 | Must | US06, US25 | BR-02, BR-03, BR-12 |
| US08 | As a **Claims Reviewer**, I want automatic completeness and consistency checks (required documents, identity, dates, invoice arithmetic) so that I see blocking problems immediately. | Early error detection | 5 | Must | US07 | BR-07 |
| US09 | As a **Claims Reviewer**, I want duplicate invoice and duplicate claim detection so that the same expense is not paid twice. | Prevent overpayment | 3 | Must | US07 | BR-07, BR-08 |
| US10 | 🟣 AI · As a **Claims Reviewer**, I want clinically inconsistent lines flagged (e.g. medication not matching the diagnosis) with the evidence shown so that I check them before paying. | Catch hidden issues | 5 | Should | US07 | BR-08 |
| US11 | 🟣 AI · As a **Claims Reviewer**, I want each expense line mapped to a benefit category with a confidence so that free-text hospital service names can be evaluated by the rules. | Rules can run on messy data | 5 | Must | US07, US21 | BR-03 |
| US12 | As a **Claims Reviewer**, I want each line evaluated against the rule-set version effective on the service date, showing result, rule code and clause so that every inclusion and exclusion is explainable. | Explainable adjudication | 8 | Must | US11, US21 | BR-04, BR-05 |
| US13 | As a **Claims Reviewer**, I want the payable amount computed with the reimbursement rate and remaining limit and its formula displayed so that I can trust and explain the number. | Correct payment | 5 | Must | US12 | BR-05, BR-10 |
| US14 | As a **Claims Reviewer**, I want validation and adjudication to re-run when I change data, keeping earlier revisions so that corrections never leave stale results. | Consistency, audit | 5 | Must | US08, US13 | BR-12 |
| US15 | 🟣 AI · As a **Claims Reviewer**, I want to review each risk flag with its evidence and dismiss it or keep it for the approver with a reason so that anomalies are handled fairly without auto-accusing the customer. | Fair risk handling | 5 | Should | US09, US10 | BR-07, BR-08 |
| US16 | 🟣 AI · As a **Claims Reviewer**, I want a draft review summary that cites the accepted facts, rule codes and open flags so that I write the justification in minutes, not from scratch. | Faster write-up | 3 | Should | US13 | BR-02 |
| US17 | As a **Claims Reviewer**, I want to override a line result with a mandatory reason code so that justified exceptions are possible and visible. | Controlled flexibility | 3 | Must | US13 | BR-05, BR-07 |
| US18 | As a **Claims Reviewer**, I want to submit an approve / partial / reject recommendation to the approval queue so that the decision moves to an accountable approver. | Workflow handoff | 3 | Must | US13 | BR-03, BR-09, BR-13 |
| US19 | As a **Claims Reviewer**, I want to request missing or corrected documents with a pre-filled checklist and deadline so that the claim can be completed. | Faster completion | 3 | Should | US08 | BR-12 |
| US20 | As a **Senior Approver**, I want to approve, partially approve, reject or return a recommendation I did not prepare so that every final decision has two-person control. | Accountability | 5 | Must | US18 | BR-01, BR-06, BR-08, BR-10, BR-13 |
| US21 | As an **Administrator**, I want to configure and publish a versioned benefit rule set (categories, exclusions, waiting period, reimbursement rate, limits) linked to clause text so that adjudication reflects the contract. | Accurate coverage | 8 | Must | — | BR-04, BR-05 |
| US22 | As an **Administrator**, I want to test a draft rule set against synthetic sample claims before publishing so that rule errors are caught before they affect payments. | Safe changes | 3 | Should | US21 | BR-04 |
| US23 | As a **Senior Approver**, I want a dashboard of backlog, handling time, outcomes and AI correction rates so that I can balance workload and judge whether AI is helping. | Operational control | 5 | Should | US18 | BR-11 |
| US24 | As a **Senior Approver**, I want a claim timeline showing every correction, AI run, rule version, override and decision so that any outcome can be reconstructed. | Auditability | 3 | Must | US14 | BR-11, BR-12 |
| US25 | As a **Claims Reviewer**, I want to pick up the next claim from my review queue, sorted by priority, so that each claim has one owner and urgent work is handled first. | No duplicated work | 3 | Must | US01, US04 | BR-09, BR-11 |

MVP release = all **Must** stories (19 stories, 88 pts). **Should** stories (6 stories, 24 pts) follow in the next increment; the product works without them because each has a manual equivalent. In a Must-only release requests for information are handled outside the system.

## Acceptance criteria

### US01 — Sign in to role home
- **Given** an active account, **when** valid credentials are submitted, **then** the user lands on their role home (Intake → Intake; Reviewer → Review queue; Approver → Approval queue; Administrator → Benefit plans) and sees only permitted navigation.
- **Given** invalid credentials or a disabled account, **when** sign-in is attempted, **then** the same generic error appears and no account state is disclosed.
- **Given** a signed-in user without permission, **when** a protected URL is opened directly, **then** the 403 page is shown with a link back to the role home.

### US02 — Manage staff access
- **Given** the admin creates or edits an account, **when** it is saved, **then** it has exactly one role and an audit event stores the before/after role, actor and time.
- **Given** an account is disabled, **when** that user tries to sign in, **then** sign-in fails with the generic error.
- **Given** the admin selects their own account, **when** they try to disable it or change its role, **then** the action is blocked.
- **Given** the admin opens the access log, **then** it lists sign-ins, role changes and claim-open events (claim number only) without any medical or identity values.

### US03 — Register claim
- **Given** a search by member name, national ID or policy number, **when** results return, **then** each shows policy status, coverage period and remaining outpatient balance.
- **Given** member, policy, service date, provider, claimed total and channel are entered, **when** the claim is created, **then** it gets a unique number, status `Draft` and an audit event.
- **Given** a service date outside the coverage period, **when** the claim is created, **then** a warning is shown but registration is allowed; rejection later still requires a Senior Approver.
- **Given** no member matches, **then** "No member found" is shown and no claim can be created.

### US04 — Upload and submit documents
- **Given** PDF, JPG or PNG files ≤ 20 MB each and ≤ 30 pages per claim, **when** uploaded, **then** each shows progress and page thumbnails; **given** any other type or size, **then** the file is rejected with the reason.
- **Given** an upload is running, **then** Submit is disabled; **given** no upload is running, **then** files can be removed before submission.
- **Given** at least one document, **when** Submit is clicked, **then** the claim moves to `Processing` and extraction (US06) starts.

### US05 — 🟣 Classify pages and check readability
- **Given** a page is uploaded, **when** the page check finishes (before submission), **then** the page shows a type (Invoice, Receipt, Prescription, Visit summary, Lab result, Other) and a confidence.
- **Given** page-type confidence < 0.80, **then** the page shows "Unclassified"; the officer may choose a type; if left Unclassified, the reviewer must choose it in US07; submission is not blocked.
- **Given** a page quality score below the configured quality threshold (blur or crop), **then** it is marked "Low quality" with a re-capture hint; submission is still possible.
- **Given** the AI service fails, **then** pages are listed as "Unclassified", the fallback banner is shown and submission continues.

### US06 — 🟣 Extract fields with source
- **Given** a submitted claim, **when** extraction runs, **then** fields follow the catalogue (insured name, ID number, policy no., provider, service date, invoice no., line description, quantity, unit price, line amount, total, diagnosis, medications) and each value stores document, page and bounding box; clicking it highlights the region.
- **Given** confidence ≥ 0.90 (≥ 0.95 for money and identity fields), **then** the value is an auto-accepted suggestion: it keeps the AI marker and the audit records `acceptedBy = threshold`.
- **Given** 0.70 ≤ confidence < 0.90 (< 0.95 for money and identity fields), **then** the field is marked "Needs verification".
- **Given** confidence < 0.70, **then** the field is left empty and marked "Needs entry" — no guessed value is shown.
- **Given** any extraction run, **then** model name/version, run ID and time are stored.
- **Given** the AI service fails, **then** the claim moves to `NeedsVerification` with empty fields for manual entry.

### US07 — 🟣 Verify and correct fields
- **Given** an assigned claim with fields to verify, **when** the Evidence tab opens, **then** fields are grouped by document, ordered by lowest confidence first, with a counter of remaining items.
- **Given** a field, **when** the reviewer accepts, edits or marks "Not present", **then** old value, new value, user and time are stored and the AI marker is replaced by the reviewer's initials.
- **Given** all required fields are accepted (by threshold or by a person), **then** the claim moves to `ReadyForReview` and, being assigned, on to `InReview`.
- **Given** a misclassified or unreadable page, **when** the reviewer reclassifies it and re-runs AI on that page, **then** only that page is re-extracted and verified fields on other pages are kept; **given** the AI service fails, **then** the reviewer enters that page's fields manually.

### US08 — Completeness and consistency checks
- **Given** accepted fields, **when** validation runs, **then** it checks: required documents present, insured name and policy match the claim, service dates consistent across documents, invoice total equals sum of lines (tolerance 0 VND).
- **Given** a failed check, **then** an Alert shows severity (Blocking / Warning), the conflicting values and their sources.
- **Given** an open Blocking alert, **when** adjudication is requested, **then** it is blocked until the alert is resolved or dismissed with a reason code and note.

### US09 — Duplicate detection
- **Given** an invoice number + provider already used in another claim, **when** validation runs, **then** a Blocking duplicate Alert links to that claim.
- **Given** the same provider + service date + amount with a different invoice number, **when** validation runs, **then** a near-duplicate Risk flag (source: rule) links to that claim for review in US15.

### US10 — 🟣 Clinical consistency flags
- **Given** a medication or service line whose AI relevance score to the stated diagnosis is below the configured clinical threshold, **when** validation runs, **then** a Warning Risk flag shows the line, the diagnosis and the AI's one-sentence reason, labelled "Signal — not a finding".
- **Given** a clinical flag, **then** amounts and line results are unchanged.
- **Given** the AI service fails, **then** no clinical flags are produced and the claim shows "Clinical check not run".
- **Given** the golden-case set, **when** the clinical check runs, **then** its false-positive rate is recorded for the AI quality view (US23).

### US11 — 🟣 Map expense lines to benefit categories
- **Given** expense lines, **when** mapping runs, **then** each line shows a suggested benefit category and confidence.
- **Given** mapping confidence < 0.85, **then** the reviewer must confirm or change the category before adjudication runs.
- **Given** mapping confidence ≥ 0.85, **then** the mapping is an auto-accepted suggestion with the AI marker and is listed for attestation at submission (US18).
- **Given** the reviewer changes a category, **then** the choice comes from the rule set's category list and is audited.
- **Given** the AI service fails, **then** every line requires manual category selection.

### US12 — Per-line rule evaluation
- **Given** confirmed mappings, **when** adjudication runs, **then** the rule-set version effective on the service date is used and its version label is displayed.
- **Given** a result, **then** each line shows Covered / Partially covered / Excluded, covered amount, rule code (e.g. EX-04) and clause excerpt.
- **Given** the same inputs and rule version, **when** adjudication is re-run, **then** results are identical.
- **Given** no published rule-set version is effective on the service date, **then** adjudication is blocked with "No effective rule set".

### US13 — Payable amount and formula
- **Given** the golden case (Consultation 200,000, Lab test 500,000, Medication 400,000 Covered; Cosmetic service 300,000 Excluded by EX-04; reimbursement rate 80%; remaining limit 800,000), **when** adjudication runs, **then** eligible = 1,100,000, after rate = 880,000, payable = min(880,000; 800,000) = 800,000 VND, and the formula shows each step.
- **Given** any result, **then** remaining balance before (golden case: 800,000) and projected after (golden case: 0) are displayed.

### US14 — Re-run after corrections
- **Given** a field, mapping or document is changed in `InReview`, **when** the change is saved, **then** validation and adjudication re-run automatically without leaving the claim workspace.
- **Given** a re-run, **then** a new numbered calculation revision is created; previous revisions remain viewable and changed lines are highlighted.
- **Given** a re-run in progress, **then** the previous result stays visible, dimmed, with "Recalculating…".

### US15 — 🟣 Review risk flags
- **Given** a risk flag, **then** it shows type, severity, evidence and source (AI or rule); AI flags are labelled "Signal — not a finding".
- **Given** the reviewer dismisses a flag or keeps it for the approver, **when** confirming, **then** a reason code and note are required.
- **Given** an anomaly without a flag, **when** the reviewer adds a manual flag, **then** it stores type, severity and note.
- **Given** an open High flag, **when** the reviewer submits a recommendation, **then** a warning is shown and the flag is shown to the approver.

### US16 — 🟣 Draft review summary
- **Given** accepted facts, rule codes, overrides and open flags, **when** the draft is generated, **then** it references only those items and every sentence has a citation chip linking to its source; a sentence without a citation is marked for the reviewer.
- **Given** the reviewer edits the draft, **when** the recommendation is submitted, **then** the record stores whether the text was AI-drafted and whether it was edited.
- **Given** the AI service fails, **then** the summary box shows an empty structured template.

### US17 — Override with reason
- **Given** a line result, **when** the reviewer overrides it, **then** a new amount or result, a reason code and a note are required.
- **Given** an override amount above the claimed line amount, **then** it is rejected; **given** any override, **then** the payable is still capped by the remaining limit.
- **Given** an overridden line, **then** it is visually distinct and the original engine result stays visible.

### US18 — Submit recommendation
- **Given** no open Blocking alert, all mappings below 0.85 confirmed, a summary and a recommendation type, **when** Submit is clicked, **then** the reviewer confirms an attestation listing the auto-accepted AI values used in the calculation, and the claim moves to `PendingApproval`.
- **Given** any of those conditions is missing, **then** Submit is disabled and the missing item is named.
- **Given** a recommendation type that differs from the type implied by the payable (BR-13), **then** a reason is required.
- **Given** submission, **then** the claim appears in the approval queue ordered by priority (high value, open High flags, age).

### US19 — Request additional information
- **Given** open alerts, **when** the reviewer opens Request information, **then** the checklist is pre-filled from them; the reviewer can add items and must set a response deadline.
- **Given** the request is confirmed, **then** a letter is generated from the template (no AI), can be exported as PDF, and the claim moves to `NeedsInformation`.
- **Given** a claim in `NeedsInformation`, **when** the Intake Officer uploads supplementary documents from the Intake "Needs information" tab and submits, **then** the claim moves to `Processing` and only the new pages are processed.
- **Given** the deadline passes without new documents, **then** a scheduled job moves the claim to `Closed` with an audit event; **given** the deadline has not passed, **then** the reviewer can extend it.

### US20 — Approve / reject / return
- **Given** the approver submitted the recommendation, **then** decision actions are disabled with an explanation.
- **Given** approve, partially approve or reject is chosen, **when** confirming, **then** the confirmation shows claim number, outcome and final amount; the outcome must match BR-13; reject requires a reason code.
- **Given** return is chosen, **then** a comment is required and the claim moves back to `InReview` and the reviewer's Returned tab.
- **Given** the remaining balance changed since the calculation revision, **when** approving, **then** approval is blocked until adjudication is re-run.
- **Given** an approval, **then** the benefit balance is reduced by the paid amount and the decision is recorded with rule-set version and calculation revision.
- **Given** a kept flag that needs investigation, **when** the approver escalates it with a reason code and note, **then** the claim gets the *Escalated* marker, stays `PendingApproval` and appears in the Escalated tab.

### US21 — Configure and publish rule set
- **Given** a draft version, **when** configuring, **then** covered categories, exclusions, waiting periods, reimbursement rate and per-visit/annual limits can be set, each with a rule code and clause text.
- **Given** a draft, **when** publishing, **then** an effective-from date later than the current version's is required.
- **Given** a published version, **then** it cannot be edited, only superseded by a new version.

### US22 — Test rule set
- **Given** a draft rule set and selected synthetic sample claims (no real claim content, BR-11), **when** the test runs, **then** per-line results of the draft appear next to the current published version with differences highlighted.
- **Given** a test run, **then** no real claim, balance or decision is changed.

### US23 — Operations dashboard
- **Given** a date range and plan filter, **then** the dashboard shows counts by status, backlog age buckets (0–2, 3–5, > 5 working days), median handling time (submission → decision), outcomes and workload per reviewer.
- **Given** the AI quality view, **then** it shows low-confidence rate (fields marked Needs verification or Needs entry ÷ fields extracted), correction rate by field (fields changed by a person ÷ fields suggested by AI) and flag dismissal rate (flags dismissed ÷ flags raised).
- **Given** export is clicked, **then** an XLSX of the filtered data is downloaded.
- **Given** a drill-down to a claim, **then** the claim workspace opens with the same scope as the approval queue.

### US24 — Claim timeline
- **Given** a claim, **when** the Timeline opens, **then** it lists in order: status changes, uploads, AI runs (model version), field corrections (before/after), alerts resolved, flags, calculation revisions, overrides and decisions, each with actor and time.
- **Given** a Claims Reviewer, **then** the timeline is available only for claims they handled.
- **Given** an Administrator, **then** the claim timeline is not available (403).

### US25 — Pick up claim from review queue
- **Given** the review queue, **then** it has tabs Available · Assigned to me · Needs verification · Needs information · Returned, sorted by priority (BR-09) then age.
- **Given** an unassigned claim in `NeedsVerification` or `ReadyForReview`, **when** the reviewer clicks "Start review", **then** it is assigned to them; a `ReadyForReview` claim moves to `InReview`, a `NeedsVerification` claim opens on the Evidence tab.
- **Given** a claim assigned to another reviewer, **then** "Start review" is unavailable (one assignee per claim).
- **Given** the global search by claim number, member or invoice number, **then** only claims the user may see are returned.

## Per-story INVEST review

✓ meets · ⚠ partly meets (accepted, with reason).

| Story | I | N | V | E | S | T | Notes |
|---|---|---|---|---|---|---|---|
| US01 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Four roles, each with a home screen that is in the Must release. |
| US02 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | One role per account; no self role change; access log without claim content. |
| US03 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Uses seeded member/policy data; no external lookup. |
| US04 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Limits explicit; supplementary upload is in US19. |
| US05 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Runs before submission; quality threshold is configured. |
| US06 | ✓ | ✓ | ✓ | ⚠ | ⚠ | ✓ | 8 pts across four document types; split candidate (invoice/receipt vs. prescription/diagnosis) if velocity data shows overrun. |
| US07 | ⚠ | ✓ | ✓ | ✓ | ✓ | ✓ | Needs an assigned claim (US25); demoable with seeded extraction results. |
| US08 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Deterministic; zero tolerance stated. |
| US09 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Match keys stated; exact → Alert, near → Risk flag. |
| US10 | ✓ | ✓ | ✓ | ⚠ | ✓ | ✓ | Clinical threshold tuned on the golden set; flag-only, cannot change money. |
| US11 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Threshold 0.85 negotiable; auto-accepted mappings attested in US18. |
| US12 | ✓ | ✓ | ✓ | ✓ | ⚠ | ✓ | 8 pts; reproducibility is testable. |
| US13 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Golden case defined per line. |
| US14 | ⚠ | ✓ | ✓ | ✓ | ✓ | ✓ | Spans UC05 and UC06; revision model needed in ERD. |
| US15 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Reviewer only; approver escalation is in US20. |
| US16 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Citation requirement makes it testable. |
| US17 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Reason code catalogue seeded; bounds stated. |
| US18 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Submission gate and attestation explicit. |
| US19 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Template letter; sending to customer is outside MVP. |
| US20 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Maker–checker, BR-13 outcome and balance re-check testable. |
| US21 | ✓ | ✓ | ✓ | ⚠ | ⚠ | ✓ | 8 pts with five rule types plus versioning; split candidate (configure draft / publish & versioning). |
| US22 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Reuses SYS-01; optional extension of UC11. |
| US23 | ✓ | ✓ | ✓ | ⚠ | ⚠ | ✓ | Two views plus export in 5 pts; metric formulas now defined. |
| US24 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Reads audit events from all stories (BR-12). |
| US25 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Defines assignment; includes global search scope. |

## Cross-cutting acceptance constraints

- Roles: `INTAKE_OFFICER`, `CLAIMS_REVIEWER`, `SENIOR_APPROVER`, `ADMINISTRATOR`. The API enforces RBAC; the UI only hides.
- AI results are stored as suggestions with confidence, source region, model version and run ID; accepted values are stored separately from raw AI output, with `acceptedBy` = threshold or user (BR-03).
- Money is stored as integer VND; all rule evaluation is deterministic and reproducible.
- Every state change, correction, override, dismissal, decision and claim access writes an audit event (BR-12).
- Medical and identity data follow BR-11 (hidden from the Administrator).
- Only synthetic or de-identified claim data is used in the MVP.

## Definition of Done

- Acceptance criteria pass on the golden-case set (clean, blurry, missing document, duplicate, conflicting, excluded line, limit-capped).
- API authorization tests cover every role for the story's endpoints.
- AI stories log confidence and source for 100% of outputs and pass with the AI service switched off (fallback path).
- UI states (loading, empty, error, AI processing, low confidence) implemented per the IA state contract.

**Related:** [Use cases](03-use-cases.md) · [Information architecture](04-information-architecture.md) · [Audit report](09-audit-report.md)
