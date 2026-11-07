# API Contract

> Design status: TARGET MVP
>
> Contracts: [claims-api.yaml](api/claims-api.yaml) (Staff Web App to Claims API, 59 operations) and [document-ai.yaml](api/document-ai.yaml) (AI Job Runner to Document AI Service, 6 operations), both OpenAPI 3.1. The YAML files are the source of truth for shapes and status codes; this page explains the rules behind them.

## How the contracts are used

The two files are written by hand and the code follows them, so the web app, the Claims API and the Python service can be built at the same time. Each boundary has its own file because the two sides of it use different languages and it is the easiest place for them to drift apart. What is generated from them, and how the rest is tested against them, is in [Code structure](10-code-structure.md#9-generated-code).

Check both files after any change:

```bash
npx @redocly/cli lint docs/api/claims-api.yaml docs/api/document-ai.yaml
```

GitHub shows the YAML as text. To read it as documentation, build a page and open it in a browser (the page is not committed):

```bash
npx @redocly/cli build-docs docs/api/claims-api.yaml -o claims-api.html
```

Each operation in `claims-api.yaml` has an `x-claimlens` object that says who may call it, in which claim statuses, in which release, and whether the call is audited. The [role matrix](#roles-and-operations) below is generated from it, and the authorization tests required by the Definition of Done can be too.

## Conventions

| Topic | Rule |
|---|---|
| Base path | `/api/v1`. Claim data sits under `/claims/{claimId}/…`, so the claim scope is checked the same way everywhere. |
| Actions | A step that is not a plain edit is a `POST` to a named action, such as `/submit`, `/start-review` or `/decision`. `operationId` is the name used in the [screen-to-API mapping](04-information-architecture.md#screen-to-api-mapping). |
| Names | JSON properties are camelCase. Enum values are the same as in the [DBML](diagrams/claimlens.dbml), so `NEEDS_VERIFICATION` and `line_amount` read the same in the database and the API. |
| Labels | The API sends codes; the web app turns them into English labels. The exceptions are text written for a person: `alert.message`, `aiReason`, summaries and notes. |
| Money | Integer VND (`int64`). No decimals, and safe in JavaScript for any realistic amount. Rates are integer basis points (`rateBp: 8000` means 80%). |
| Confidence | A number from 0 to 1. The API also sends the level (`HIGH`, `CHECK`, `MISSING`) that the thresholds gave it, so the web app never applies a threshold. |
| Regions | `{x, y, w, h}`, normalised to the page (0 to 1). |
| Time | Timestamps are RFC 3339 with an offset. Business dates are `YYYY-MM-DD`. The web app formats them as dd/MM/yyyy. |
| Nulls | A property that has no value is present and null, so a screen can tell "empty" from "not sent". |
| Paging | `page` and `pageSize` (default 25, maximum 100); the answer has `items`, `page`, `pageSize` and `total`. |
| Queues | `listClaims` takes a `view`, one per tab, because Returned and Decided by me cannot be expressed with a status filter. The answer also has `counts` for the tab badges. |
| Server-driven actions | `getClaim` returns `allowedActions` and `blockers`, and `getRecommendation` returns `readiness`. The web app renders these and does not repeat the rules, which is how "Submit disabled, with the missing item named" stays correct. |
| Warnings | A problem that does not stop the action comes back with the success in `warnings`, such as a service date outside the coverage period. |
| Trace | Every response has `X-Request-Id`, also stored in `audit_event.request_id`. |

Some data stays inside the system and is never sent:

- `password_hash`, object-storage keys, `input_hash`, `golden_scenario` and `eval_label`.
- `ai_value` when its level is `MISSING`, because a value below 0.70 is kept but never shown (US05).
- A full national ID. Responses carry `nationalIdMasked`.

Member search is a `POST`, so a citizen ID never reaches a URL or an access log.

## Sign-in and tokens

Sign-in uses a short access token and a rotating refresh token.

| | Access token | Refresh token |
|---|---|---|
| Form | Signed JWT | Opaque; only its SHA-256 hash is stored in `refresh_token` |
| Lifetime | 15 minutes | 8 hours from sign-in; rotation never extends it |
| Kept in | The page's memory, never `localStorage` | `HttpOnly`, `Secure`, `SameSite=Strict` cookie, `Path=/api/v1/auth` |
| Sent as | `Authorization: Bearer` | The cookie, only to `/auth/refresh` and `/auth/logout` |

1. `login` returns the access token and sets the cookie. A wrong password, an unknown email and a disabled account all get the same 401.
2. On a 401 `AUTH_REQUIRED`, the web app calls `refreshToken` and retries once. Reloading the page does the same, because memory was cleared.
3. Each refresh replaces the refresh token. The old one is accepted for 10 more seconds so two tabs can refresh together. Reuse after that revokes every token from that sign-in, because it suggests the token was stolen.
4. `logout` revokes them too. A disabled account loses access at its next refresh, so within 15 minutes.
5. `/auth/refresh` and `/auth/logout` reject a request whose `Origin` is not the web app. Other endpoints do not use cookies, so they need no CSRF protection.

This has two consequences for the build. The web app and the API must be on the same site, or the `SameSite=Strict` cookie is not sent. The MVP serves both from one origin: a reverse proxy sends `/api/*` to the Claims API and everything else to the web app, so no CORS is needed. Because an `<img>` tag cannot send a Bearer token, the web app fetches page images with `getPage` and shows them through blob URLs.

## Synchronous and asynchronous work

| Work | How it runs | What the caller gets |
|---|---|---|
| Validation, Rule Engine, test runs | Inside the request, because they make no outside call | The result, including `progress` on every change |
| AI tasks | Queued in `ai_run`, run by the AI Job Runner | 202, then poll `getClaimStatus` |
| Upload conversion and page split | In the background after the file is stored | 201 with `CONVERTING`, then poll `getClaimStatus` |

`getClaimStatus` is the only endpoint to poll, every 3 seconds while something is running. It carries no medical data, identity data or file names, so it is the one claim read that is not audited (BR-12). The reads that are audited write one event per request; a queue page writes one event, not one per row.

What triggers each AI task:

| Task | Trigger |
|---|---|
| `PAGE_CHECK` | A document reaches `READY` |
| `FIELD_EXTRACTION` | `submitClaim` |
| `PAGE_REREAD` | `rerunExtraction`, one page only |
| `BENEFIT_MAPPING` | The claim is `IN_REVIEW` with no open Blocking alert; afterwards only for lines not yet mapped |
| `CLINICAL_CHECK` | The first validation, then only when the diagnosis or an expense line changes; earlier flags become `SUPERSEDED` |
| `SUMMARY_DRAFT` | `draftSummary` |

Any data change in `IN_REVIEW` re-runs validation and adjudication inside the request. If an open Blocking alert stops adjudication, no revision is created and the current one reports `isStale: true`. A revision is stale when the SHA-256 of the current inputs, balance included, differs from its `input_hash`, so no extra column is needed. A stale revision cannot be submitted.

## Concurrency

Tables with `row_version` (`claim`, `claim_field`) expose it as `rowVersion`, and an edit sends back the value it read. A newer value on the server gives 409 `VERSION_CONFLICT`, with the current values in `details`, which is what the Conflict UI state needs to keep local input and show what changed. This is in the body and not in an `ETag` header because fields arrive in a list, and each one has its own version.

Other operations are protected by the state they require: `startReview` by the assignee, `submitRecommendation` and `decideClaim` by `revisionNo`, and alerts and flags by their status. A plan draft has one editor in practice, so `updateRuleSet` is last-write-wins until publishing.

## Key flows

**Taking a claim.** The reviewer sees unassigned claims in the `AVAILABLE` view with no member name (BR-11), then calls `startReview`. Two reviewers racing get one success and one 409 `CLAIM_ALREADY_ASSIGNED`. "Open next claim" is `listClaims` with `pageSize=1` followed by `startReview`.

**AI is down.** A failed run sets `fallbackActive` on the claim. The reviewer sets each page type with `reclassifyPage`, which creates the empty header fields, adds lines with `addExpenseLine`, and enters values with `updateField`. Each value still needs a drawn source region (BR-02).

**Recommending.** `getRecommendation` gives `readiness` and `attestationItems`, the auto-accepted `line_amount` fields and benefit mappings used in the calculation. `submitRecommendation` sends the `revisionNo` and the list the reviewer attested. A different list than the server's gives 409 `ATTESTATION_OUTDATED`. The outcome is the proposed one, or `REJECTED` with a reason code and note. The client never sends an amount.

**Deciding.** The approver can take the action that matches the payable, `REJECT`, or `returnClaim`. Other amounts are not possible at this step: changing an amount is a line override, done by the reviewer, which creates a new revision.

**The balance changed.** When an approval finds that `policy.balance_version` moved since the revision:

1. The system creates a `BALANCE_RECHECK` revision and answers 409 `BALANCE_CHANGED`, with both amounts and the actions still open.
2. Same payable: the approver repeats `decideClaim` on the new `revisionNo`.
3. Different payable: only `returnClaim` and `REJECT` remain, so the reviewer can write a new recommendation on the right number. The approver takes that step, so `decided_by` always names a person who acted (BR-01).

## Errors

Errors follow RFC 9457 (`application/problem+json`) with a stable `code`, which is what the web app reads. `errors[]` lists field problems with `VALIDATION_FAILED`.

| Status | Means | Codes |
|---|---|---|
| 401 | Not signed in, or the token is no longer valid | `AUTH_REQUIRED`, `INVALID_CREDENTIALS`, `REFRESH_TOKEN_INVALID` |
| 403 | Signed in but not allowed | `FORBIDDEN_ROLE`, `CLAIM_OUT_OF_SCOPE`, `MAKER_CHECKER`, `ORIGIN_NOT_ALLOWED` |
| 404 | Does not exist | `NOT_FOUND` |
| 409 | The current state does not allow this | See the table below |
| 413, 415 | The file was refused before anything was stored | `FILE_TOO_LARGE`, `UNSUPPORTED_FILE_TYPE` |
| 422 | The request itself breaks a rule | See the table below |
| 429 | Too many sign-in attempts | `TOO_MANY_ATTEMPTS` |
| 500 | A fault in the Claims API. The body names nothing about it; `requestId` finds it in the log | `INTERNAL_ERROR` |

A claim outside the caller's scope gives 403 by id, as UC11 requires. `getClaimByNumber` gives 404 there, so a claim number reveals nothing about other claims.

| Code | Status | Meaning | Rule or constraint |
|---|---|---|---|
| `CLAIM_STATE_CONFLICT` | 409 | Not allowed in the claim's status | Lifecycle trigger |
| `VERSION_CONFLICT` | 409 | Someone else changed this first | `row_version` |
| `UPLOAD_IN_PROGRESS` | 409 | A file is still uploading or converting | US03 |
| `NO_READY_DOCUMENT` | 409 | Nothing to submit | US03 |
| `CLAIM_ALREADY_ASSIGNED` | 409 | Another reviewer took the claim | US23 |
| `ALERT_NOT_OPEN`, `FLAG_NOT_OPEN` | 409 | Already handled | `alert_status`, `flag_status` |
| `BLOCKING_ALERT_OPEN` | 409 | An open Blocking alert stops this | US07 |
| `MAPPING_UNCONFIRMED` | 409 | A category still needs confirming | US10 |
| `LINE_AMOUNT_MISSING` | 409 | A line has no amount | Data model §7 |
| `NO_EFFECTIVE_RULE_SET` | 409 | No published version on the service date | US11 |
| `NO_CALCULATION_REVISION` | 409 | Adjudication has not run yet | Data model §7 |
| `NO_ACTIVE_OVERRIDE` | 409 | The line has no override to remove | US16 |
| `REVISION_OUTDATED` | 409 | A newer revision exists | Data model §7 |
| `REVISION_STALE` | 409 | The inputs changed since the latest revision | US13 |
| `ATTESTATION_OUTDATED` | 409 | The attested list is not the current one | US17 |
| `BALANCE_CHANGED` | 409 | The balance moved since the revision | BR-10 |
| `DRAFT_ALREADY_EXISTS` | 409 | The plan already has a draft | `ux_rule_set_one_draft` |
| `RULE_SET_NOT_DRAFT` | 409 | Only a draft can change | Rule-set trigger |
| `VALIDATION_FAILED` | 422 | A field is missing or out of range | |
| `POLICY_MEMBER_MISMATCH` | 422 | The policy belongs to another member | `claim_policy_member` |
| `SOURCE_REQUIRED` | 422 | A value needs a source region | `ck_claim_field_source_required`, BR-02 |
| `FIELD_HAS_NO_AI_VALUE` | 422 | Nothing to accept | US06 |
| `ALERT_NOT_DISMISSIBLE` | 422 | A claimed-total mismatch must be fixed | `ck_alert_total_not_dismissible` |
| `UNKNOWN_CATEGORY` | 422 | Not a benefit category | `benefit_category` |
| `OVERRIDE_EXCEEDS_LINE` | 422 | Above the line amount | `ck_override_cap`, BR-07 |
| `OVERRIDE_AMOUNT_MISMATCH` | 422 | The amount does not fit the result | `ck_override_result` |
| `SUMMARY_REQUIRED` | 422 | A summary is required | US17 |
| `OUTCOME_NOT_ALLOWED` | 422 | Only the proposed outcome or Rejected | BR-13 |
| `OUTCOME_REASON_REQUIRED` | 422 | A different outcome needs a reason and note | `ck_recommendation_mismatch_reason` |
| `DECISION_NOT_ALLOWED` | 422 | The action does not match the payable | `ck_decision_br13_*`, BR-13 |
| `DECISION_REASON_REQUIRED` | 422 | A rejection needs a reason code | `ck_decision_br13_rejected` |
| `DECISION_COMMENT_REQUIRED` | 422 | A rejection with a payable above 0 needs a comment | BR-13 |
| `RULE_INVALID` | 422 | A rule is missing the values of its type, or is ordered before a kind that applies first | `ck_rule_*`, US19 |
| `EFFECTIVE_DATE_NOT_LATER` | 422 | Must be today or later in Vietnam, and later than the current version | US19 |
| `CATEGORY_NOT_CONFIGURED` | 422 | An active category has no rule | Data model Q7 |

The database repeats the critical rules, so the Claims API checks first and maps a `ck_` or `ux_` failure to the same code. A constraint failure with no mapping is a bug and gives 500.

## Roles and operations

Generated from `x-claimlens`. Scope: `any` is not tied to a claim, `own` is claims the person registered (`registered` in the YAML), `assigned` is the assigned reviewer, `all` is every claim. A blank means 403. *Should* marks the 9 operations of the next increment. On the operations marked `assigned`, a reviewer who has not taken the claim gets 403; `startReview` and the `AVAILABLE` view are how a claim is reached before that.

<!-- role-matrix:start -->

**Auth**

| Operation | Request | Intake | Reviewer | Approver | Admin | Audit |
|---|---|---|---|---|---|---|
| `login` | `POST /auth/login` | public | public | public | public | write |
| `refreshToken` | `POST /auth/refresh` | public | public | public | public | none |
| `logout` | `POST /auth/logout` | public | public | public | public | write |
| `getMe` | `GET /me` | any | any | any | any | none |

**Intake**

| Operation | Request | Intake | Reviewer | Approver | Admin | Audit |
|---|---|---|---|---|---|---|
| `searchMembers` | `POST /members/search` | any |  |  |  | read |
| `getPolicySummary` | `GET /policies/{policyId}/summary` | any |  |  |  | read |
| `createClaim` | `POST /claims` | any |  |  |  | write |
| `updateClaim` | `PATCH /claims/{claimId}` | own |  |  |  | write |
| `submitClaim` | `POST /claims/{claimId}/submit` | own |  |  |  | write |
| `uploadDocument` | `POST /claims/{claimId}/documents` | own |  |  |  | write |
| `deleteDocument` | `DELETE /claims/{claimId}/documents/{documentId}` | own |  |  |  | write |

**Claims**

| Operation | Request | Intake | Reviewer | Approver | Admin | Audit |
|---|---|---|---|---|---|---|
| `listClaims` | `GET /claims` | own | assigned | all |  | read |
| `getClaimByNumber` | `GET /claim-numbers/{claimNumber}` | own | assigned | all |  | read |
| `getClaim` | `GET /claims/{claimId}` | own | assigned | all |  | read |
| `getClaimStatus` | `GET /claims/{claimId}/status` | own | assigned | all |  | none |
| `startReview` | `POST /claims/{claimId}/start-review` |  | any |  |  | write |

**Evidence**

| Operation | Request | Intake | Reviewer | Approver | Admin | Audit |
|---|---|---|---|---|---|---|
| `updateClaimFacts` | `PATCH /claims/{claimId}/facts` |  | assigned |  |  | write |
| `listDocuments` | `GET /claims/{claimId}/documents` | own | assigned | all |  | read |
| `getPage` | `GET /claims/{claimId}/pages/{pageId}/image` | own | assigned | all |  | read |
| `reclassifyPage` | `PUT /claims/{claimId}/pages/{pageId}/type` | own | assigned |  |  | write |
| `rerunExtraction` | `POST /claims/{claimId}/pages/{pageId}/rerun-extraction` |  | assigned |  |  | write |
| `listFields` | `GET /claims/{claimId}/fields` |  | assigned | all |  | read |
| `updateField` | `PATCH /claims/{claimId}/fields/{fieldId}` |  | assigned |  |  | write |
| `acceptField` | `POST /claims/{claimId}/fields/{fieldId}/accept` |  | assigned |  |  | write |

**Validation**

| Operation | Request | Intake | Reviewer | Approver | Admin | Audit |
|---|---|---|---|---|---|---|
| `listAlerts` | `GET /claims/{claimId}/alerts` |  | assigned | all |  | read |
| `dismissAlert` | `POST /claims/{claimId}/alerts/{alertId}/dismiss` |  | assigned |  |  | write |
| `revalidateClaim` | `POST /claims/{claimId}/revalidate` |  | assigned |  |  | write |

**Adjudication**

| Operation | Request | Intake | Reviewer | Approver | Admin | Audit |
|---|---|---|---|---|---|---|
| `listExpenseLines` | `GET /claims/{claimId}/expense-lines` |  | assigned | all |  | read |
| `addExpenseLine` | `POST /claims/{claimId}/expense-lines` |  | assigned |  |  | write |
| `removeExpenseLine` | `DELETE /claims/{claimId}/expense-lines/{lineId}` |  | assigned |  |  | write |
| `confirmMapping` | `PUT /claims/{claimId}/expense-lines/{lineId}/mapping` |  | assigned |  |  | write |
| `overrideLine` | `PUT /claims/{claimId}/expense-lines/{lineId}/override` |  | assigned |  |  | write |
| `removeOverride` | `DELETE /claims/{claimId}/expense-lines/{lineId}/override` |  | assigned |  |  | write |
| `listCalculationRevisions` | `GET /claims/{claimId}/calculation-revisions` |  | assigned | all |  | read |
| `runAdjudication` | `POST /claims/{claimId}/calculation-revisions` |  | assigned |  |  | write |
| `getCalculationRevision` | `GET /claims/{claimId}/calculation-revisions/{revisionNo}` |  | assigned | all |  | read |

**Risk**

| Operation | Request | Intake | Reviewer | Approver | Admin | Audit |
|---|---|---|---|---|---|---|
| `listFlags` *Should* | `GET /claims/{claimId}/flags` |  | assigned | all |  | read |
| `dismissFlag` *Should* | `POST /claims/{claimId}/flags/{flagId}/dismiss` |  | assigned |  |  | write |
| `keepFlag` *Should* | `POST /claims/{claimId}/flags/{flagId}/keep` |  | assigned |  |  | write |

**Decision**

| Operation | Request | Intake | Reviewer | Approver | Admin | Audit |
|---|---|---|---|---|---|---|
| `getRecommendation` | `GET /claims/{claimId}/recommendation` |  | assigned | all |  | read |
| `saveRecommendationDraft` | `PUT /claims/{claimId}/recommendation` |  | assigned |  |  | write |
| `draftSummary` *Should* | `POST /claims/{claimId}/recommendation/summary-draft` |  | assigned |  |  | write |
| `submitRecommendation` | `POST /claims/{claimId}/recommendation/submit` |  | assigned |  |  | write |
| `decideClaim` | `POST /claims/{claimId}/decision` |  |  | all |  | write |
| `returnClaim` | `POST /claims/{claimId}/return` |  |  | all |  | write |

**Timeline**

| Operation | Request | Intake | Reviewer | Approver | Admin | Audit |
|---|---|---|---|---|---|---|
| `getClaimTimeline` | `GET /claims/{claimId}/timeline` |  | assigned | all |  | read |

**Policy**

| Operation | Request | Intake | Reviewer | Approver | Admin | Audit |
|---|---|---|---|---|---|---|
| `listPlans` | `GET /plans` |  | any | any | any | none |
| `listPlanVersions` | `GET /plans/{planId}/versions` |  | any | any | any | none |
| `createPlanVersion` | `POST /plans/{planId}/versions` |  |  |  | any | write |
| `getPlanVersion` | `GET /plans/{planId}/versions/{versionId}` |  | any | any | any | none |
| `updateRuleSet` | `PUT /plans/{planId}/versions/{versionId}/rules` |  |  |  | any | write |
| `publishPlanVersion` | `POST /plans/{planId}/versions/{versionId}/publish` |  |  |  | any | write |
| `discardPlanVersion` | `POST /plans/{planId}/versions/{versionId}/discard` |  |  |  | any | write |
| `testRuleSet` *Should* | `POST /plans/{planId}/versions/{versionId}/test-runs` |  |  |  | any | write |
| `listBenefitCategories` | `GET /benefit-categories` |  | any | any | any | none |
| `listSampleClaims` *Should* | `GET /sample-claims` |  |  |  | any | none |

**AI quality**

| Operation | Request | Intake | Reviewer | Approver | Admin | Audit |
|---|---|---|---|---|---|---|
| `getAiQualityMetrics` *Should* | `GET /ai-quality/metrics` |  |  | any |  | none |
| `getAiQualityDrillDown` *Should* | `GET /ai-quality/drill-down` |  |  | all |  | read |
| `exportReport` *Should* | `GET /ai-quality/export` |  |  | any |  | read |

<!-- role-matrix:end -->

## Decisions behind this contract

| # | Decision | Why |
|---|---|---|
| 1 | Two files | The boundaries have different callers and languages. |
| 2 | All 23 stories, Should operations marked | The contract does not change when they arrive. |
| 3 | OpenAPI 3.1, written by hand | Nullable values are simpler than in 3.0, and ASP.NET Core 10 generates 3.1 by default, which helps when reading a difference. The code is checked by testing real responses against the YAML. |
| 4 | Access token and rotating refresh token | Chosen by the team. Keeping the refresh token in an HttpOnly cookie and the access token in memory limits what a script injected into the page can steal. |
| 5 | Rules in the request, AI through the queue | The Rule Engine makes no outside call. AI can be slow or down. |
| 6 | Reviewers take claims themselves; Available shows no member name | Keeps BR-11 while still letting a reviewer choose. |
| 7 | Priority labels `HIGH` and `NORMAL`; the sort keeps every BR-09 key | Needs no new thresholds. The review queue uses the claimed total because a payable may not exist yet. |
| 8 | The server sets the amount | Every amount traces back to a revision. The only departure from the payable is a rejection, with a reason. |
| 9 | The approver, not the system, returns a claim after a balance change | A person is always behind a decision row (BR-01), and a failed request does not change the claim's status. |
| 10 | `CLAIMED_TOTAL_MISMATCH` must be fixed, not dismissed | Dismissing it would only move the failure to `ck_revision_eligible`. |
| 11 | `getPage` serves thumbnails too, and `getClaimStatus` carries AI run status | One endpoint per job. Separate ones would add operations without adding capability. |
| 12 | `rowVersion` in the body, not `ETag` | Fields arrive in a list with a version each, and every conflict then takes the same 409 path. |
| 13 | Notifications, member search in the top bar and Save draft on Evidence left out | No requirement or table is behind them. |
| 14 | One origin for the web app and the API | A reverse proxy is needed for TLS anyway. One origin removes CORS, and with it a preflight for every page image. |

## Still open

| Item | Needed for | Assumption until then |
|---|---|---|
| Clinical relevance threshold, severity bands and the page quality threshold | US04, US09 | Placeholders in config; to be tuned on the golden set |
| AI Model Provider and pipelines | All AI tasks | `model` is a request field that names a pipeline, so this does not change the contract |
| Hosting | The first deployment | Web app and API on one origin behind a reverse proxy |

## UI changes this contract requires

The prototypes in [docs/ui](ui/) include these changes. Each file opens on its main state; add the hash to its URL to see another one. The Stitch project itself has not changed; the [prompts](stitch-prompts.md#thay-đổi-từ-api-contract) bring it in line.

| Screen | Change | Other states |
|---|---|---|
| All screens | Removed the notification bell. | |
| SCR-20 Review queue | Added the Available tab, with no Member column. Priority shows High and Normal, in BR-09 order. The table filter searches claim numbers only. | `#available` |
| SCR-31 Evidence | Removed "Save draft". Added "Add expense line". | |
| SCR-33 Adjudication | Added the stale revision, and an overridden line with "Remove override". | `#stale`, `#override` |
| SCR-35 Decision, reviewer | "Approve full" is disabled when the payable is below the claimed total. Reject shows 0 ₫ and requires a reason and note. | `#reject` |
| SCR-35 Decision, [approver](ui/scr-35-decision-approver.html) | New. Read-only recommendation, line results, kept flag and attested values, with the decision dialogs and the balance-changed state. | `#confirm`, `#reject`, `#return`, `#balance-changed` |
| SCR-50 AI quality | Added the mapping correction tile. | |

SCR-31 and SCR-33 are shown at 1600 px. At the 1280 px minimum they are cramped: amounts wrap in SCR-33 and a badge is clipped in SCR-31, because zone C keeps its 480 px. That was already the case, and is to be fixed when the screens are built.

**Related:** [Use cases](03-use-cases.md) · [Information architecture](04-information-architecture.md) · [Data model](06-data-model.md) · [User stories](08-invest-requirements.md) · [C4 model](architecture/c4/README.md)
