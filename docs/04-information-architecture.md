# Information Architecture

> Design status: TARGET MVP
>
> Platform: web, desktop-first (1440 px reference; usable down to 1280 px). Tablet/mobile are read-only fallbacks, not primary targets.

The IA is organised around **one claim case** and **one work queue per role**. Staff should never hunt for work: each role signs in to its queue, opens a case, and finishes it in a single workspace.

## Sitemap

```text
ClaimLens
│
├── Public
│   └── Sign in                                   /sign-in
│
└── Authenticated (left sidebar + top bar)
    │
    ├── Work
    │   ├── Intake                                /intake                    Intake Officer
    │   │   ├── New claim (stepper)               /intake/new
    │   │   │   ├── 1 Member & policy
    │   │   │   ├── 2 Claim details
    │   │   │   └── 3 Documents & submit
    │   │   ├── My submissions                    /intake?tab=submitted
    │   │   └── Needs information (add documents) /intake?tab=needs-info
    │   │
    │   ├── Review queue                          /queue                     Claims Reviewer
    │   │   └── tabs: Available · Assigned to me · Needs verification · Needs information · Returned
    │   │
    │   ├── Approval queue                        /approvals                 Senior Approver
    │   │   └── tabs: Pending · Escalated · Decided by me
    │   │
    │   └── Claim workspace                       /claims/:id                Reviewer, Approver (read: Ops, masked — BR-11)
    │       ├── Overview                          /claims/:id/overview
    │       ├── Documents & evidence              /claims/:id/evidence
    │       ├── Validation                        /claims/:id/validation
    │       ├── Adjudication                      /claims/:id/adjudication
    │       ├── Risk                              /claims/:id/risk
    │       ├── Decision                          /claims/:id/decision
    │       ├── Timeline                          /claims/:id/timeline
    │       └── (drawers / modals)
    │           ├── Request information           modal
    │           ├── Override line                 drawer
    │           ├── Dismiss / keep flag (Reviewer) · Escalate flag (Approver)   modal
    │           └── Approve · Reject · Return     modal
    │
    ├── Search claims                             /claims                    Intake, Reviewer, Approver, Ops (scoped by BR-11)
    │
    ├── Policy
    │   ├── Members & policies                    /members                   Policy Admin, Intake (read; seeded data in MVP)
    │   │   └── Member detail                     /members/:id
    │   └── Benefit plans                         /plans                     Policy Admin
    │       └── Plan                              /plans/:id
    │           ├── Versions                      /plans/:id/versions
    │           ├── Rule set editor (draft)       /plans/:id/versions/:v
    │           │   └── tabs: Categories · Exclusions · Waiting periods · Coinsurance · Limits
    │           └── Test run                      /plans/:id/versions/:v/test
    │
    ├── Insights
    │   ├── Operations dashboard                  /insights/operations       Ops Manager
    │   ├── AI quality                            /insights/ai-quality       Ops Manager
    │   └── Audit log                             /insights/audit            Ops Manager (claim events, masked), Sys Admin (access log only)
    │
    ├── Administration
    │   └── Staff accounts                        /admin/staff               Sys Admin
    │
    ├── Profile                                   /profile                   all
    └── 403 / 404                                 /403  /404
```

## Navigation model

Global shell: **left sidebar** (collapsible, icon + label) · **top bar** (global claim search by claim no. / member / invoice no., notifications, profile) · **content area**.

Sidebar items are rendered per role — never shown disabled.

| Sidebar group | Item | Intake | Reviewer | Approver | Policy Admin | Ops | Sys Admin |
|---|---|---|---|---|---|---|---|
| Work | Intake | ● home | | | | | |
| Work | Review queue | | ● home | | | | |
| Work | Approval queue | | | ● home | | | |
| Work | Search claims | ✓ | ✓ | ✓ | | ✓ | |
| Policy | Members & policies | read | | | ✓ | | |
| Policy | Benefit plans | | read | read | ● home | | |
| Insights | Operations dashboard | | | | | ● home | |
| Insights | AI quality | | | | | ✓ | |
| Insights | Audit log | | | | | ✓ masked | ✓ access log |
| Admin | Staff accounts | | | | | | ● home |

● = landing page after sign-in (US01). In a Must-only release (US23 not shipped) the Operations Manager lands on Audit log.

Data visibility follows BR-11: medical and identity values are shown only to the Intake Officer who registered the claim, the assigned Reviewer and Senior Approvers; Ops sees them masked; Sys Admin never opens claim content.

## The core screen: Claim workspace

The Claim workspace is where ~80 % of reviewer time is spent. It is a **three-zone layout** with persistent context, so evidence is never more than one click away.

```text
┌───────────────────────────────────────────────────────────────────────────────────────────┐
│ Case header: CLM-2026-000184 · Nguyễn V. A · Policy GOLD-OP · Service 12/10/2026          │
│ Status pill · Assignee · Rule set v3 (eff. 01/01/2026) · Payable 800,000 ₫ · [Primary CTA]│
├───────────────────────────────────────────────────────────────────────────────────────────┤
│ Tabs: Overview · Evidence · Validation (2) · Adjudication · Risk (1) · Decision · Timeline │
├──────────────┬──────────────────────────────────────────┬─────────────────────────────────┤
│ A. Document  │ B. Working panel (changes per tab)       │ C. Context rail                 │
│    rail      │                                          │                                 │
│ thumbnails   │ Evidence: field list by confidence       │ Document viewer with            │
│ grouped by   │ Validation: alerts by severity           │ highlighted source region       │
│ type, with   │ Adjudication: expense-line table         │ (follows the selected row)      │
│ quality and  │   result · rule code · amount · formula  │                                 │
│ AI status    │ Risk: flag cards with evidence           │ ── or ──                        │
│              │ Decision: summary draft + recommendation │ Policy clause / rule detail     │
│              │                                          │ Remaining balance meter         │
├──────────────┴──────────────────────────────────────────┴─────────────────────────────────┤
│ Sticky action bar: progress (e.g. 3 fields to verify · 1 blocking alert) · secondary · CTA │
└───────────────────────────────────────────────────────────────────────────────────────────┘
```

**Linking rule:** selecting any row in zone B (a field, alert, expense line, flag or summary citation) moves zone C to its source — the highlighted page region or the rule clause. This is the "click *Cosmetic service* → see the invoice line and exclusion EX-04" interaction from the product research.

**Primary CTA by status:**

| Status | Primary CTA (reviewer) | Primary CTA (approver) |
|---|---|---|
| NeedsVerification or ReadyForReview, unassigned | Start review | — |
| NeedsVerification, assigned | Verify next field | — |
| InReview, blocking alert open | Resolve next alert | — |
| InReview, no blocking alert | Run adjudication / Continue to decision | — |
| InReview, decision tab | Submit recommendation (with attestation) | — |
| NeedsInformation | (read-only, waiting for documents) | — |
| PendingApproval | (read-only) | Approve · Partially approve · Reject · Return |

## Screen inventory

| Screen ID | Screen | Presentation | Role(s) | Stories |
|---|---|---|---|---|
| `SCR-01` | Sign in | Page | all | US01 |
| `SCR-10` | Intake home (drafts, submitted, needs information) | Page | Intake | US03, US04, US19 |
| `SCR-11` | New claim stepper — member & policy | Step page | Intake | US03 |
| `SCR-12` | New claim stepper — claim details | Step page | Intake | US03 |
| `SCR-13` | Documents & submit (upload, AI page check; also supplementary upload) | Step page | Intake | US04, US05, US19 |
| `SCR-20` | Review queue | Page | Reviewer | US01, US25 |
| `SCR-21` | Approval queue | Page | Approver | US18, US20 |
| `SCR-22` | Search claims | Page | Intake, Reviewer, Approver, Ops | US25 |
| `SCR-30` | Claim workspace — Overview | Tab | Reviewer, Approver, Ops (masked) | US18, US20 |
| `SCR-31` | Claim workspace — Documents & evidence | Tab | Reviewer | US05, US06, US07 |
| `SCR-32` | Claim workspace — Validation | Tab | Reviewer | US08, US09 |
| `SCR-33` | Claim workspace — Adjudication | Tab | Reviewer, Approver | US11, US12, US13, US14, US17 |
| `SCR-34` | Claim workspace — Risk | Tab | Reviewer, Approver | US09, US10, US15, US20 |
| `SCR-35` | Claim workspace — Decision | Tab | Reviewer, Approver | US16, US18, US20 |
| `SCR-36` | Claim workspace — Timeline | Tab | Reviewer (own claims), Approver, Ops (masked) | US24 |
| `SCR-37` | Request information | Modal | Reviewer | US19 |
| `SCR-38` | Override line | Drawer | Reviewer | US17 |
| `SCR-39` | Decision confirmation (approve / partially approve / reject / return) | Modal | Approver | US20 |
| `SCR-40` | Members & policies / member detail (read-only, seeded) | Page | Policy Admin, Intake (read) | US03 |
| `SCR-41` | Benefit plans & versions | Page | Policy Admin | US21 |
| `SCR-42` | Rule set editor | Page with tabs | Policy Admin | US21 |
| `SCR-43` | Rule set test run | Page | Policy Admin | US22 |
| `SCR-50` | Operations dashboard | Page | Ops | US23 |
| `SCR-51` | AI quality | Page | Ops | US23 |
| `SCR-52` | Audit log | Page | Ops (claim events, masked), Sys Admin (access log only) | US24, US02 |
| `SCR-60` | Staff accounts (+ form modal) | Page | Sys Admin | US02 |
| `SCR-70` | Profile | Page | all | — (shell only; change password out of MVP) |
| `SCR-90` | 403 / 404 | Page | all | cross-cutting |

**Priority screens for UI design** (highest value, design first): `SCR-31` Evidence, `SCR-33` Adjudication, `SCR-35` Decision, `SCR-20` Review queue, `SCR-13` Upload, `SCR-50` Operations dashboard.

## Screen hierarchy

```mermaid
flowchart TB
    signin["SCR-01 Sign in"] --> shell["Authenticated shell"]

    shell --> intake["SCR-10 Intake home"]
    intake --> s1["SCR-11 Member & policy"] --> s2["SCR-12 Claim details"] --> s3["SCR-13 Documents & submit"]

    shell --> queue["SCR-20 Review queue"]
    shell --> approvals["SCR-21 Approval queue"]
    shell --> search["SCR-22 Search claims"]

    queue & approvals & search --> ws["Claim workspace /claims/:id"]
    ws --> ov["SCR-30 Overview"]
    ws --> ev["SCR-31 Evidence"]
    ws --> va["SCR-32 Validation"]
    ws --> ad["SCR-33 Adjudication"]
    ws --> ri["SCR-34 Risk"]
    ws --> de["SCR-35 Decision"]
    ws --> tl["SCR-36 Timeline"]
    ad --> ovr["SCR-38 Override drawer"]
    va & de --> req["SCR-37 Request info modal"]
    de --> conf["SCR-39 Decision modal"]

    shell --> members["SCR-40 Members & policies"]
    shell --> plans["SCR-41 Benefit plans"] --> editor["SCR-42 Rule set editor"] --> test["SCR-43 Test run"]

    shell --> opsd["SCR-50 Operations"]
    shell --> aiq["SCR-51 AI quality"]
    shell --> audit["SCR-52 Audit log"]
    shell --> staff["SCR-60 Staff accounts"]
    shell --> profile["SCR-70 Profile"]
    shell --> err["SCR-90 403 / 404"]

    classDef core fill:#1a56db,color:#fff,stroke:#123c99
    class ws,ev,ad,de,queue core
```

## Primary task flows

### Reviewer: from queue to recommendation

```mermaid
flowchart LR
    q["Review queue<br/>sorted by priority"] --> open["Start review<br/>(self-assign)"]
    open --> v{"Fields to<br/>verify?"}
    v -->|yes| ev["Evidence tab<br/>verify lowest confidence first"] --> val
    v -->|no| val{"Blocking<br/>alerts?"}
    val -->|yes| fix["Validation tab<br/>fix data or dismiss w/ reason"] --> val
    val -->|no| map{"Unconfirmed<br/>mappings?"}
    map -->|yes| cm["Adjudication tab<br/>confirm benefit categories"] --> calc
    map -->|no| calc["Rule engine result<br/>per line + formula"]
    calc --> risk{"Open<br/>flags?"}
    risk -->|yes| rk["Risk tab<br/>dismiss / keep for approver"] --> dec
    risk -->|no| dec["Decision tab<br/>edit AI summary, choose outcome"]
    dec --> att["Attest auto-accepted<br/>AI values"] --> sub["Submit → Pending approval"]
    fix -.->|"evidence missing"| ri["Request information"]
    calc -.->|"edit data"| val
```

### Intake: from packet to processing

```mermaid
flowchart LR
    a["Search member"] --> b["Confirm policy<br/>coverage + balance"] --> c["Claim details"] --> d["Drop files"]
    d --> e["AI page check<br/>type + quality per page"]
    e -->|low quality| f["Re-capture hint"] --> d
    e -->|ok| g["Submit"] --> h["Processing → Review queue"]
    n["Needs information tab<br/>open claim"] --> d
```

The same upload step serves supplementary documents for a claim in `NeedsInformation`; only the new pages are processed (US19).

### Approver: decision

```mermaid
flowchart LR
    a["Approval queue"] --> b["Overview: amount, outcome,<br/>overrides, open flags"] --> c{"Agree?"}
    c -->|yes| d["Approve / Partial / Reject<br/>confirm amount + reason"] --> bal{"Balance unchanged<br/>since calculation?"}
    bal -->|yes| e["Decision recorded,<br/>balance consumed"]
    bal -->|no| rc["Blocked: re-run<br/>adjudication"]
    c -->|"needs work or<br/>evidence missing"| f["Return with comment"] --> g["Back to reviewer<br/>(Returned tab)"]
    c -->|"flag needs<br/>investigation"| esc["Escalate flag<br/>(Escalated marker)"]
```

## AI interaction patterns

AI content must be **recognisable, inspectable and reversible** everywhere it appears. These patterns are part of the IA so every screen uses the same grammar.

| Pattern | Where | Behaviour |
|---|---|---|
| **AI suggestion marker** | Any AI-produced value, category, flag or text | Small sparkle icon + "AI" label; disappears (replaced by a person's initials) once a human accepts or edits. |
| **Confidence level** | Fields, page types, mappings | Shown by icon + text, never colour alone; raw score on hover. Fields have three levels: *High* (auto-accepted suggestion, AI marker kept), *Check* (needs verification), *Missing* (left empty). Page types and mappings have two: *High* and *Check*. Thresholds per BR-03 in the use case document. |
| **Attestation** | Decision tab, on submit | Lists every auto-accepted AI value used in the calculation; the reviewer confirms before the recommendation is submitted. |
| **Source pin** | Fields, expense lines, alerts, summary citations | Click → zone C scrolls to page and draws the bounding box. |
| **Signal, not finding** | Risk flags from AI | Neutral wording ("Possible duplicate", "Medication may not match diagnosis"); never "fraud". |
| **Citation chips** | AI summary | Each sentence ends with chips (`Invoice p2`, `EX-04`); text without a citation is flagged for the reviewer. (Request letters are template-based, no AI.) |
| **Re-run scope** | Evidence tab | "Re-run AI on this page" — never silently on the whole claim; verified fields are preserved. |
| **AI status** | Document rail, case header | Queued · Reading · Done · Failed (manual mode). |
| **Fallback banner** | Any tab when the AI service failed | "AI assistance unavailable — fields require manual entry"; the workflow continues. |

## UI state contract

| State | Required behaviour |
|---|---|
| Initial loading | Keep shell and case header; skeletons for the affected panel. |
| AI processing | Per-document progress in the document rail; the reviewer can already open verified documents. |
| Low confidence | Field shows *Check* or *Missing*; queue counters show remaining items; CTA becomes "Verify next field". |
| AI failed | Fallback banner; fields empty and editable; claim not blocked. |
| Empty | Distinguish "no claims assigned" from "no results for filters"; show the permitted primary action. |
| Validation error | Keep input, associate message with the field, focus the first error. |
| Recalculating | Previous result stays visible but dimmed with "Recalculating…"; new revision number on completion with a "What changed" link. |
| Conflict | Another user changed the claim → keep local input, show diff, ask to reload. |
| Forbidden | 403 page with a safe route back to the role home. |
| Destructive / final action | Confirmation shows claim no., outcome and final amount; final decisions cannot be undone in the UI. |
| Success | Toast + next item from the queue ("Open next claim"). |

## Labelling & terminology

Consistent words across UI, docs and code:

| Concept | UI label | Avoid |
|---|---|---|
| Claim case | Claim | File, ticket |
| Extracted value | Field | Data point |
| Deterministic issue (incl. exact duplicate) — Validation tab | Alert | Error (unless system error) |
| AI signal or near-duplicate — Risk tab | Risk flag / Signal | Fraud, suspicious customer |
| AI value at or above threshold | Auto-accepted suggestion | Verified, confirmed |
| Rule engine output per line | Line result | AI decision |
| Reviewer proposal | Recommendation | Decision |
| Approver action | Decision | Approval (for reject too) |
| Changed engine result | Override | Edit |
| Benefit balance | Remaining balance | Limit left |

UI labels are in **English** (decided in the UI brief); sample data such as names and addresses stays Vietnamese.

## Responsive & accessibility rules

- Desktop-first: the three-zone workspace needs ≥ 1280 px; below that, zone C becomes a toggleable overlay and zone A collapses to a dropdown.
- Tables keep all columns on desktop; below 1024 px they become summary cards with an expand action.
- Every status, confidence and severity uses icon + text, not colour alone; contrast ≥ 4.5:1.
- Full keyboard path: queue rows → tabs → zone B rows (↑/↓) → Enter opens source in zone C; shortcuts for Accept (A), Edit (E), Next field (N).
- Money is always shown as integer VND with thousand separators and "₫"; dates as dd/MM/yyyy.

## Screen-to-API mapping

| Screen | API operations |
|---|---|
| Sign in | `login`, `logout`, `getMe` |
| Intake | `searchMembers`, `getPolicySummary`, `createClaim`, `uploadDocument`, `deleteDocument`, `submitClaim` |
| Queues & search | `listClaims` (filters: status, assignee, priority; scoped by BR-11), `startReview` (self-assign) |
| Evidence | `listDocuments`, `getPage`, `listFields`, `acceptField`, `updateField`, `reclassifyPage`, `rerunExtraction` |
| Validation | `listAlerts`, `resolveAlert`, `dismissAlert` |
| Adjudication | `listExpenseLines`, `confirmMapping`, `runAdjudication`, `getCalculationRevision`, `overrideLine` |
| Risk | `listFlags`, `addFlag`, `dismissFlag`, `keepFlag`, `escalateFlag` (Approver only) |
| Decision | `getSummaryDraft`, `submitRecommendation` (with attestation), `createInfoRequest`, `extendInfoDeadline`, `decideClaim`, `returnClaim` |
| Timeline / audit | `getClaimTimeline`, `listAuditEvents` (masked for Ops), `listAccessLog` (Sys Admin) |
| Policy | `listPlans`, `createPlanVersion`, `updateRuleSet`, `publishPlanVersion`, `testRuleSet`, `listMembers`, `getMember` |
| Insights | `getOperationsMetrics`, `getAiQualityMetrics`, `exportReport` |
| Staff | `listStaff`, `createStaff`, `updateStaff`, `setStaffStatus` |

**Related:** [Use cases](03-use-cases.md) · [User stories](08-invest-requirements.md)
