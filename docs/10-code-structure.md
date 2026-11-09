# Code Structure

> Design status: TARGET MVP
>
> Purpose: say where every piece of code lives, why it lives there, and what keeps it there. US01 sign-in and US19 benefit rule sets are built; the rest is the target. [Section 14](#14-what-is-built) lists what exists.

The structure follows the [C4 model](architecture/c4/README.md): one folder per container, and one folder per component inside the Claims API and the Document AI Service. The contracts in [docs/api](api/) and the schema in [claimlens.dbml](diagrams/claimlens.dbml) stay where they are. Code is generated from them or tested against them.

## 1. Repository layout

```text
PMDC-AITHUCCHIEN/
├── apps/
│   ├── web/                    # Staff Web App (Next.js)
│   ├── claims-api/             # Claims API, with the Rule Engine (ASP.NET Core)
│   └── document-ai/            # Document AI Service (FastAPI)
├── db/
│   ├── migrations/             # Forward-only SQL, applied in name order
│   └── seed/                   # reference/ for every environment, dev/ for local and test
├── fixtures/
│   └── golden/                 # The seven golden scenarios, shared by every test suite (clean has its documents; excluded-line and limit-capped hold Rule Engine expectations; AI responses arrive with the extraction slice, US04/US05)
├── e2e/                        # Playwright, runs against the whole stack
├── tools/
│   ├── contract/               # Generates the role matrix and OperationPolicies
│   ├── schema/                 # Compares the DBML with a migrated database
│   └── golden/                 # Generates the synthetic golden documents: npm run golden:generate
├── infra/
│   ├── compose.yaml
│   ├── .env.example
│   ├── Caddyfile               # One origin, in development too
│   └── storage/s3.json         # Local identities, with Document AI limited to pages/
├── docs/                       # Unchanged, including docs/api and claimlens.dbml
├── .github/workflows/
├── package.json                # Pins the repository tools and Playwright; no workspaces
├── package-lock.json
├── .editorconfig
├── .gitattributes              # text=auto eol=lf, so Linux containers never see CRLF
├── .gitignore
├── 01_Product_Research.xmind
├── 02_Feature_Map.xmind
└── README.md
```

## 2. Decisions

| # | Decision | Why | Rejected |
|---|---|---|---|
| S1 | This document comes before any scaffolding | Moving a folder on paper costs nothing. | Scaffolding first |
| S2 | One repository, with the three applications under `apps/` | The contracts, the schema and the three codebases change together. | One repository per container: one contract change becomes three pull requests |
| S3 | .NET 10 LTS, pinned in `global.json` | The [API contract](07-api-contract.md#decisions-behind-this-contract) assumes it, and .NET 9 support ends on 10/11/2026. | .NET 9 |
| S4 | Two C# projects, `RuleEngine` and `Api`, with feature folders inside `Api` | The compiler guards the one boundary that matters most (C3 rule 2), and each C3 component still has one obvious home. | Clean Architecture layers: one component spread over four projects. One project per component: 17 projects, plus extra interfaces to break the loop between business components and the AI Job Runner |
| S5 | SQL first: `db/migrations` is what runs, DbUp applies it, and EF Core only maps | Checks, triggers and partial indexes would be raw SQL inside EF migrations anyway, and C# would become a second source of truth beside the DBML. The export from DBML was tested (section 13). DbUp runs inside the API binary, so tests use the same code and no other tool is needed. | EF Core migrations. Flyway or dbmate, which add a second tool |
| S6 | `x-claimlens` generates one policy table, enforced by one middleware, on Minimal APIs | 59 operations each declare roles and audit, and 25 declare allowed statuses. One table cannot drift from the contract. As middleware it runs before model binding, so a caller without access learns nothing from validation errors. | Attributes written by hand on 59 operations. An endpoint filter, which runs after binding |
| S7 | TypeScript and Python types are generated. C# types are written by hand and every real response is checked against the YAML | Both generators ran cleanly on these contracts (section 13). Checking responses inside the integration tests suits a stateful API better than fuzzing it. | Comparing the document ASP.NET generates with the YAML: the two describe the same shape in different forms, so the difference is mostly noise |
| S8 | Must stories first, Should folders created when they are built, one switch for the release | The product works without the Should stories, and their fallback screens already exist. | Empty stubs from day one |
| S9 | Claim scope is checked in the API. Row-level security follows in a later increment | As written, row-level security hides the duplicate lookup from the reviewer and every claim from the job runner (section 13). The MVP holds only synthetic data. | Row-level security now: needs a second database role and a session setting on every pooled connection |
| S10 | `model` names a pipeline, and the Model Gateway has one method per kind of task, plus a fake | A value with its own bounding box most likely needs OCR and an LLM together, while `ai_run` records one model. How to combine them stays inside the provider's adapter, so tasks do not change when the provider is chosen. | One provider model per task. Separate OCR and LLM ports before there is a provider to shape them |
| S11 | LibreOffice runs in its own container, Gotenberg. The Claims API still renders pages | Conversion behaves the same on Windows and in CI, the API image stays small, and the process that parses uploaded files holds no credentials. Gotenberg already handles restarts, timeouts and the queue, and its image already has fonts with Vietnamese glyphs (section 13). | LibreOffice inside the API image: local development needs LibreOffice and the same fonts on every machine, and we would write that process handling ourselves |
| S12 | SeaweedFS for local work, only the S3 API in code, one key prefix per kind of file | MinIO's repository is archived. SeaweedFS is Apache-2.0 and active. Its permissions can be scoped to a prefix, so Document AI can be limited to `pages/`. | MinIO. Garage, which is AGPL-3.0 |
| S13 | One origin through Caddy, in development too: `/api/*` goes to the Claims API, everything else to the web app | TLS needs a reverse proxy anyway, and one origin removes CORS. The contract's `servers: /api/v1` is already relative. With CORS, the `Authorization` header makes the browser send a preflight for each distinct URL, which includes every page image. | `app.` and `api.` subdomains with CORS. A Next.js rewrite for development, which failed a 20 MB upload (section 13). `output: 'export'`, which does not fit dynamic routes such as `/claims/[id]` |
| S14 | Tailwind, shadcn/ui on Base UI, TanStack Query and Table, React Hook Form with Zod, TypeScript 5.9 | Components are copied into the repository, so `DESIGN.md` can restyle them fully. New shadcn projects now default to Base UI; it is added when the first dialog or menu is built. TypeScript 7 breaks `openapi-typescript` today (section 13). | Radix, still supported but no longer the default. A global state store |
| S15 | Golden documents are generated by a script, and seed data is SQL run by the same runner as migrations | No sample documents exist yet, and a script can make blurry and conflicting versions on demand. The runner needs no `psql` on Windows. | Documents made by hand. A separate seeding tool |

## 3. Names

| C4 name | Folder | Compose service | Code name |
|---|---|---|---|
| Staff Web App | `apps/web` | `web` | |
| Claims API | `apps/claims-api` | `claims-api` | `ClaimLens.Api` |
| Rule Engine | `apps/claims-api/src/ClaimLens.RuleEngine` | | `ClaimLens.RuleEngine` |
| Document AI Service | `apps/document-ai` | `document-ai` | `document_ai` |
| Document Converter | None; the stock `gotenberg/gotenberg:8.39.0` image in `infra/compose.yaml` | `converter` | |
| Database | `db` | `postgres` | |
| Object Storage | | `storage` | |

The use case documents call the Document AI Service the "AI Document Intelligence Service". Code uses `document-ai` only.

| Kind of file | Rule | Example |
|---|---|---|
| C# operation | `operationId` in PascalCase | `acceptField` is `AcceptField.cs` |
| React component | PascalCase | `CaseHeader.tsx` |
| Other TypeScript | kebab-case | `use-claim-status.ts` |
| Python | snake_case | `field_extraction.py` |
| Migration | Four digits and a name | `0003_triggers.sql` |
| Generated code | `.g.` in the name where the language allows it | `OperationPolicies.g.cs` |

## 4. Claims API

```text
apps/claims-api/
├── ClaimLens.slnx
├── global.json                 # Pins the .NET 10 SDK
├── Directory.Build.props       # Nullable on, warnings as errors
├── Directory.Packages.props    # One version per NuGet package
├── Dockerfile                  # Build context is the repository root, for db/
├── src/
│   ├── ClaimLens.RuleEngine/   # References nothing
│   │   ├── RuleEvaluator.cs    # Evaluate(input) returns the result
│   │   ├── RuleKinds.cs        # LineResult, RuleType, LimitScope
│   │   ├── Input/              # Lines, rule-set version, balance, dates
│   │   ├── Output/             # Line outcomes, formula rows
│   │   └── BannedSymbols.txt   # Clock, file, network and random APIs
│   └── ClaimLens.Api/
│       ├── Program.cs          # Also runs `migrate` and `seed`
│       ├── Startup.cs
│       ├── Http/
│       │   ├── OperationPolicies.g.cs
│       │   ├── OperationPolicyMiddleware.cs
│       │   ├── Problems.cs
│       │   ├── ConstraintErrorMap.cs   # ck_ and ux_ names to error codes
│       │   └── NotBuiltEndpoints.cs    # 501 for operations with no handler yet
│       ├── AccessControl/      # Sign-in, tokens, ClaimScope
│       ├── Domain/
│       │   ├── Entities/       # Added table by table as features need them, 21 in the end
│       │   └── Enums/Enums.g.cs  # 33 DBML enums; the other 3 belong to the Rule Engine
│       ├── Features/
│       │   ├── Intake/
│       │   ├── Lifecycle/      # ClaimStatusMachine, QueuePriority
│       │   ├── Evidence/       # FieldCatalogue
│       │   ├── Validation/     # ClaimValidator, Checks/ with one file per alert_check
│       │   ├── Adjudication/   # EngineInputLoader, InputHash, RevisionWriter
│       │   ├── RiskFlags/
│       │   ├── Decision/       # OutcomeRules, BalanceRecheck
│       │   ├── RuleSets/       # PublishChecks
│       │   └── Insights/       # Timeline and AI quality
│       ├── DocumentPreparation/   # ConverterClient, PageRenderer, DocumentPreparationWorker
│       ├── AiJobs/             # AiJobQueue, AiJobRunner, DocumentAiClient, Thresholds, Appliers/
│       ├── Audit/              # AuditLog
│       ├── Persistence/        # ClaimLensDbContext, EnumMapping, MigrationRunner
│       └── DocumentStore/      # Reads and writes objects by key
└── tests/
    ├── ClaimLens.RuleEngine.Tests/
    └── ClaimLens.Api.Tests/
        ├── Features/
        ├── Contract/           # Checks real responses against claims-api.yaml
        ├── Authorization/      # One case per operation, role and scope
        ├── Architecture/       # The rules in the table below
        └── Golden/             # Planned, arrives with the extraction slice (US04/US05), when scenarios can run with AI on and off
```

Every feature folder has the same shape. One endpoints file maps the routes, and each operation has one file holding its request, response and handler:

```text
Features/Evidence/
├── EvidenceEndpoints.cs        # One line per operation, named by operationId
├── AcceptField.cs
├── UpdateField.cs
├── ...
└── FieldCatalogue.cs           # Threshold group, required and sensitive flag per field_key
```

Handlers use `ClaimLensDbContext` directly, with no generic repository on top, because a DbContext already is one.

### Component to folder

Each of the 17 components in [C3](architecture/c4/03-component-claims-api.md) has one home, and each of the 59 operations has one file. `*` marks a Should operation.

| C3 component | Folder | Operations |
|---|---|---|
| HTTP API | `Http/` | |
| Access Control | `AccessControl/` | `login`, `refreshToken`, `logout`, `getMe` |
| Claim Intake | `Features/Intake/` | `searchMembers`, `getPolicySummary`, `createClaim`, `updateClaim`, `submitClaim`, `uploadDocument`, `deleteDocument` |
| Claim Lifecycle | `Features/Lifecycle/` | `listClaims`, `getClaimByNumber`, `getClaim`, `getClaimStatus`, `startReview` |
| Evidence | `Features/Evidence/` | `updateClaimFacts`, `listDocuments`, `getPage`, `reclassifyPage`, `rerunExtraction`, `listFields`, `updateField`, `acceptField` |
| Validation | `Features/Validation/` | `listAlerts`, `dismissAlert`, `revalidateClaim` |
| Adjudication | `Features/Adjudication/` | `listExpenseLines`, `addExpenseLine`, `removeExpenseLine`, `confirmMapping`, `overrideLine`, `removeOverride`, `listCalculationRevisions`, `runAdjudication`, `getCalculationRevision` |
| Risk Flags | `Features/RiskFlags/` | `listFlags`\*, `dismissFlag`\*, `keepFlag`\* |
| Decision | `Features/Decision/` | `getRecommendation`, `saveRecommendationDraft`, `draftSummary`\*, `submitRecommendation`, `decideClaim`, `returnClaim` |
| Rule Set Management | `Features/RuleSets/` | `listPlans`, `listPlanVersions`, `createPlanVersion`, `getPlanVersion`, `updateRuleSet`, `publishPlanVersion`, `discardPlanVersion`, `testRuleSet`\*, `listBenefitCategories`, `listSampleClaims`\* |
| AI Quality and Timeline | `Features/Insights/` | `getClaimTimeline`, `getAiQualityMetrics`\*, `getAiQualityDrillDown`\*, `exportReport`\* |
| Rule Engine | `ClaimLens.RuleEngine` project | |
| Document Preparation | `DocumentPreparation/` | |
| AI Job Runner | `AiJobs/` | |
| Audit Log | `Audit/` | |
| Persistence | `Persistence/` | |
| Document Store | `DocumentStore/` | |

`Domain/` is not a component. It holds the shape of the data, which several components share: `claim_field`, for example, is used by Evidence, Validation and AI Job Runner.

### What keeps the structure honest

| Rule | Source | Enforced by |
|---|---|---|
| The Rule Engine has no I/O and depends on nothing | C3 rules 1 and 2 | No project references, `BannedApiAnalyzers` |
| Every policy has an endpoint | S6 | `NotBuiltEndpoints` maps any operation with no handler to 501, so the role, scope and status rules of all 59 run and are tested before the features exist |
| Thresholds are applied only in `AiJobs/`, and only `AiJobs/` sets `ai_level` and `ai_type_level` | C3 rule 4 | Architecture tests |
| Only `AiJobs/` calls the Document AI Service | C3 | Architecture test |
| Only `DocumentStore/` uses the S3 SDK | C3 | Architecture test |
| The engine reads only accepted values | C3 rule 3 | `EngineInputLoader`, golden tests |
| Only `ClaimStatusMachine` changes `claim.status` | C3 rule 5 | Code review, then the database trigger |
| A child record is always looked up with its `claim_id` | BR-11 | Authorization tests: a field of claim B through `/claims/A/…` gives 404 |
| Every write operation records an audit event | BR-12 | A test on every `audit: write` operation |

### What happens on a request

`OperationPolicyMiddleware` finds the policy by endpoint name, which is the `operationId`, and runs in this order:

1. **Release.** A `should` operation answers 404 while `Release:IncludeShould` is off.
2. **Role.** A role that is not listed gets 403 `FORBIDDEN_ROLE`.
3. **Scope.** When the route has a `claimId`, it loads the claim once and checks `registered`, `assigned` or `all`, then gives 403 `CLAIM_OUT_OF_SCOPE`. `listClaims` and `getClaimByNumber` have no `claimId`, so they filter in their own query.
4. **Status.** A status outside `states` gets 409 `CLAIM_STATE_CONFLICT`.
5. **Handler.** It runs, and writes its own `audit: write` event in the same transaction, because only the handler knows the before and after values.
6. **Read audit.** After a successful `audit: read` operation, the middleware writes the event. If that insert fails, the request fails.

A `ck_` or `ux_` failure anywhere goes through `ConstraintErrorMap` to its error code. A name that is not in the map is a bug and gives 500. The proxy's `X-Forwarded-For` is trusted only from the known proxy, so `audit_event.ip_address` holds the real client.

### Background work

Both workers are `BackgroundService` classes inside the API process, and tests can switch them off and drive them directly.

| Worker | Queue | How a crash is handled |
|---|---|---|
| `AiJobRunner` | `ai_run` in `QUEUED`, claimed with `FOR UPDATE SKIP LOCKED`, then committed as `RUNNING` before the call. A claim's `FIELD_EXTRACTION` waits until none of its `PAGE_CHECK` runs is `QUEUED` or `RUNNING` | A transient failure (5xx, 429, timeout, unreachable, an answer off the contract) puts the run back to `QUEUED` with a doubling wait while other runs go ahead; after `MaxAttempts`, or at once for 401 and 422, it is `FAILED`. A run left in `RUNNING` past its task timeout plus 30 seconds is `FAILED` as `TIMEOUT`. Every `FAILED` run triggers the fallback. Both workers share `FailedAttempts`, kept in memory for one API instance |
| `DocumentPreparationWorker` | `document` in `CONVERTING`, with `FOR UPDATE SKIP LOCKED` held during the conversion | The lock dies with the process, so the next pass retries the file. No new column is needed. Pages and `READY` commit together |

`uploadDocument` stores the original first and then inserts the row as `CONVERTING`, so a failed store leaves no row that would block submission. It checks a file's type by its first bytes, not its name: LibreOffice turns any bytes into a PDF, so a corrupted DOCX would otherwise become a page of garbage instead of being refused (section 13). Images are rotated according to their EXIF data before a page image is written, because every bounding box is relative to that image.

`FieldExtractionApplier` and `PageRereadApplier` share `Appliers/FieldReadings`, which records a reading and applies the thresholds. After any change to a claim's evidence, `Features/Lifecycle/ClaimProgression` moves the claim on if it is ready and calls `ClaimRecheck`, an empty hook that Slices 4a and 4b fill with validation and calculation.

### Configuration

Settings without a UI (06, section 2) live in `appsettings.json` and environment variables. Secrets come only from the environment.

| Section | Holds |
|---|---|
| `Auth` | Token lifetimes, signing key (at least 32 bytes), allowed origins, issuer and audience; checked when the API starts |
| `Proxy` | `KnownProxies` and `KnownNetworks` allowed to set `X-Forwarded-For`. With neither set, forwarded headers are ignored |
| `Thresholds` | `PageType` 0.80, `GeneralField` 0.90, `MoneyAndIdentityField` 0.95, `NeedsEntryBelow` 0.70, `PageQuality` 0.50 (to tune on the golden set), `BenefitMapping` 0.85; checked when the API starts |
| `Ai` | `BaseUrl` (the service root), `ServiceKey` (environment only), and per task the pipeline name and version and `TimeoutSeconds` (page check 30, extraction 120, page re-read 60) |
| `Converter`, `Storage` | Base URL, timeout, bucket and credentials |
| `Limits` | 20 MB per file, 30 pages per claim |
| `Queue` | BR-09 high-value threshold |
| `Validation` | Required document types |
| `Release` | `IncludeShould` |
| `Workers` | On or off, poll interval, concurrent runs for `DocumentPreparation` and `AiJobs`; for both also `MaxAttempts` and `RetryDelaySeconds`, how a file whose converter or storage call fails waits (doubling, at most 15 minutes) while the files behind it go ahead, and when it is rejected |

## 5. Staff Web App

```text
apps/web/
├── next.config.ts              # output: standalone
├── package.json
├── package-lock.json
└── src/
    ├── app/                    # Routes only: each page assembles one feature
    │   ├── layout.tsx          # Fonts and global styles
    │   ├── not-found.tsx
    │   ├── sign-in/
    │   └── (app)/
    │       ├── layout.tsx      # Client gate: refresh, getMe, shell by role
    │       ├── intake/
    │       ├── queue/
    │       ├── approvals/
    │       ├── claims/[id]/    # layout.tsx holds the header, tabs and three zones
    │       ├── plans/
    │       ├── insights/ai-quality/
    │       ├── profile/
    │       └── 403/
    ├── features/
    │   ├── auth/               # SignInPage, AuthGate, session hook
    │   ├── shell/              # Sidebar and top bar
    │   ├── intake/
    │   ├── queues/
    │   ├── workspace/
    │   │   ├── shell/          # CaseHeader, TabBar, DocumentRail, ContextRail, ActionBar
    │   │   ├── viewer/         # PageImage, RegionOverlay, RegionDraw
    │   │   ├── overview/
    │   │   ├── evidence/
    │   │   ├── validation/
    │   │   ├── adjudication/   # Includes the override drawer, SCR-38
    │   │   ├── risk/
    │   │   ├── decision/       # Reviewer and approver views, decision modal SCR-39
    │   │   └── timeline/
    │   ├── plans/
    │   ├── ai-quality/
    │   └── profile/
    ├── components/
    │   ├── ui/                 # shadcn/ui on Base UI, restyled from DESIGN.md
    │   ├── ai/                 # AiMarker, ConfidenceBadge, SourcePin, CitationChip, SignalLabel
    │   └── states/             # Skeleton, EmptyState, FallbackBanner, ConflictNotice, StaleNotice
    ├── lib/
    │   ├── api/
    │   │   ├── schema.d.ts     # Generated from claims-api.yaml
    │   │   ├── client.ts       # openapi-fetch, bearer token, retry once on AUTH_REQUIRED
    │   │   ├── claim-queries.ts # The claim hooks and keys intake and the workspace share
    │   │   ├── session.ts      # Access token in memory, one refresh at a time
    │   │   └── problem.ts      # Error code to behaviour
    │   ├── auth/roles.ts       # Role to home and navigation; UX only
    │   ├── labels/             # Codes to English labels
    │   └── format/             # ₫ and dd/MM/yyyy
    ├── styles/tokens.css       # Tokens from DESIGN.md
    └── test/                   # MSW handlers typed from schema.d.ts
```

Tests sit next to the code they test, as `*.test.tsx`.

### Routes

| Route | Screen | Roles | Notes |
|---|---|---|---|
| `/sign-in` | SCR-01 | Public | |
| `/intake` | SCR-10 | Intake | `?tab=submitted` |
| `/intake/new` | SCR-11, SCR-12 | Intake | `createClaim` runs at the end of step 2, then goes to the route below |
| `/intake/:claimId` | SCR-13 | Intake | Step 3 while `DRAFT`; a read-only header and documents after submission |
| `/queue` | SCR-20 | Reviewer | |
| `/approvals` | SCR-21 | Approver | |
| `/claims/:id` | | Reviewer, Approver | Redirects to `evidence`, and to `overview` once that tab exists |
| `/claims/:id/overview` to `/timeline` | SCR-30 to SCR-36 | Reviewer, Approver | One folder per tab. Only `evidence` exists so far, and a tab that is not built is not shown |
| `/plans` | SCR-41 | Admin, Reviewer, Approver | |
| `/plans/:id` | | Admin, Reviewer, Approver | Redirects to `versions` |
| `/plans/:id/versions` | SCR-41 | Admin, Reviewer, Approver | |
| `/plans/:id/versions/:versionId` | SCR-42 | Administrator (edit), Reviewer and Approver (read) | `versionId` is the UUID; the screen shows `v3` |
| `/plans/:id/versions/:versionId/test` | SCR-43 | Admin | Should |
| `/insights/ai-quality` | SCR-50 | Approver | Should |
| `/profile` | SCR-70 | All | |
| `/403`, `not-found.tsx` | SCR-90 | All | |

### Rules

1. **Every signed-in page is a client component.** The access token lives in the page's memory and the refresh cookie is sent only to `/api/v1/auth`, so the Next.js server never knows who is signed in. Guards therefore run in `(app)/layout.tsx`, not in `proxy.ts`, and there is no data fetching in Server Components. The API's 403 is the real check.
2. **Only `lib/api` calls `fetch`.** Features use TanStack Query hooks. Polling `getClaimStatus` every 3 seconds is a `refetchInterval` that stops when nothing is running.
3. **The web app does not decide.** It shows `level`, `allowedActions`, `blockers` and `readiness`, and never compares a confidence with a threshold.
4. **AI output goes through `components/ai`,** so the marker, level and source pin look the same on every screen.
5. **Page images go through `workspace/viewer`.** It fetches `getPage` into a blob URL and revokes it when done. Every page is a PNG and every region is relative to it, so an image with an SVG overlay is enough and pdf.js is not needed.
6. **No i18n library.** Labels are English, written once in `lib/labels`.

## 6. Document AI Service

```text
apps/document-ai/
├── pyproject.toml
├── uv.lock
├── .python-version             # 3.12
├── Dockerfile
├── src/document_ai/
│   ├── main.py                 # create_app: routers, error handlers
│   ├── asgi.py                 # What the server runs
│   ├── config.py
│   ├── security.py             # X-Service-Key
│   ├── errors.py               # RFC 9457 responses, PageFailure
│   ├── api/tasks.py            # Task API: the six routes
│   ├── contracts/models.py     # Generated from document-ai.yaml
│   ├── tasks/
│   │   ├── page_check.py
│   │   ├── field_extraction.py # Also PAGE_REREAD
│   │   ├── benefit_mapping.py
│   │   ├── clinical_signals.py
│   │   └── summary_draft.py
│   ├── pages/reader.py         # Page Reader: boto3, read-only
│   └── gateway/                # Model Gateway
│       ├── ports.py            # One method per kind of task, and the failure types
│       ├── output_checks.py    # Bad model output becomes BAD_MODEL_OUTPUT
│       ├── fake/               # Leaves every value for a person; replays golden answers later
│       └── providers/          # One module per provider, empty until one is chosen
└── tests/
    ├── test_contract.py        # Schemathesis, against the app with the fake gateway
    ├── test_tasks.py
    ├── test_startup.py
    └── test_architecture.py
```

**`model` names a pipeline.** `field-extractor v2.4` in a request and in `ai_run` means one set of provider models plus one prompt version. The dashboard's model version filter then also separates prompt changes, which change results as much as a new model does. This keeps the [C3 rule](architecture/c4/03-component-document-ai.md) that the gateway does not choose a model: the request names the pipeline, and the adapter only resolves it. The adapter, its prompts and that mapping arrive together with the provider.

**The provider needs a spike before US05.** Try the candidates on synthetic Vietnamese invoices and check three things: diacritics, a usable region for each value, and whether the confidence scores can carry the 0.95 threshold.

## 7. Database

```text
db/
├── migrations/
│   ├── 0001_schema.sql         # dbml2sql output, not edited
│   ├── 0002_indexes.sql        # Partial unique indexes (06, section 10)
│   ├── 0003_triggers.sql       # Lifecycle, frozen rule sets, approval and usage
│   ├── 0004_search.sql         # pg_trgm, unaccent, immutable wrapper, GIN index
│   ├── 0005_claim_number.sql
│   └── 0006_roles.sql          # claimlens_app; UPDATE and DELETE revoked on audit_event
└── seed/
    ├── reference/              # benefit_category, needed everywhere
    └── dev/                    # Staff, members, policies, plans, rule sets, sample_claim
```

| Topic | Rule |
|---|---|
| Runner | DbUp inside the API: `dotnet ClaimLens.Api.dll migrate`, and `seed` for local and test. Integration tests call the same code. The scripts are embedded in the binary. |
| Database roles | Migrations run as the owner, and the app connects as `claimlens_app`. |
| Changing the schema | Update the DBML and add a numbered migration. A migration that has been applied is never edited. |
| Keeping them in step | `npm run schema:check` builds one empty database from the migrations and one from the DBML export, and fails if a column, enum, constraint or index in the DBML is missing or different. It needs no server. |
| EF Core | Mapping is written by hand and sits inline in `Persistence/ClaimLensDbContext.cs`, which is enough for the tables built so far. It will be split into one configuration per table in `Persistence/Configurations` when the file becomes hard to read as more tables arrive. `dotnet ef migrations` is not used. |
| Enums | Native PostgreSQL enums, generated into C# from the DBML. A test compares all 36 with `pg_enum`. |
| Seed staff | Two reviewers, to test `CLAIM_ALREADY_ASSIGNED`, and two approvers. `seed` hashes `Seed:DevPassword` at run time, so no hash is in the repository, and it refuses to run without that setting. |

## 8. Object Storage

One bucket with one prefix per kind of file. Keys are generated and never contain a file name, because a file name can hold a patient's name.

| Key | Holds | Read by |
|---|---|---|
| `originals/{claimId}/{documentId}.{ext}` | The uploaded file | Claims API |
| `renditions/{claimId}/{documentId}.pdf` | The PDF of a DOCX or XLSX file | Claims API |
| `pages/{claimId}/{documentId}/{pageNo}.png` | Full page image, `image_key` | Claims API, Document AI Service |
| `thumbnails/{claimId}/{documentId}/{pageNo}.png` | Small image for the document rail | Claims API |
| `ai-raw/{claimId}/{runId}.json` | Full AI response, `raw_output_key` | Claims API |

Document AI credentials can read `pages/` and nothing else. `getPage` with `variant=thumbnail` swaps `pages/` for `thumbnails/` in `image_key`, so the schema needs no new column.

## 9. Generated code

Generated files are committed, so a build needs no generator. CI generates them again and fails if the result differs.

| File | Source | Tool |
|---|---|---|
| `apps/web/src/lib/api/schema.d.ts` | `claims-api.yaml` | `openapi-typescript` |
| `apps/document-ai/src/document_ai/contracts/models.py` | `document-ai.yaml` | `datamodel-code-generator` |
| `apps/claims-api/.../Http/OperationPolicies.g.cs` | `x-claimlens` in `claims-api.yaml` | `tools/contract` |
| `apps/claims-api/.../Domain/Enums/Enums.g.cs`, `.../Persistence/EnumMapping.g.cs` | `claimlens.dbml` | `tools/contract` |
| Role matrix in [07](07-api-contract.md#roles-and-operations) | `x-claimlens` in `claims-api.yaml` | `tools/contract` |
| `db/migrations/0001_schema.sql` | `claimlens.dbml` | `dbml2sql`, once |

`npm run contract:generate` writes them and `npm run contract:check` is what CI runs. C# request and response types, and `DocumentAiClient`, are written by hand. The contract tests catch any drift.

## 10. Tests

| Level | Where | Tools | Proves |
|---|---|---|---|
| Rule Engine | `ClaimLens.RuleEngine.Tests` | xUnit | US11, US12, the same input gives the same result |
| Features | `ClaimLens.Api.Tests/Features` | Testcontainers, fake Document AI | Acceptance criteria |
| Contract, Claims API | `ClaimLens.Api.Tests/Contract` | JsonSchema.Net | Every response any test sees matches the YAML: the check sits in the test client |
| Authorization | `ClaimLens.Api.Tests/Authorization` | Cases from `x-claimlens` | Every role on every operation (Definition of Done) |
| Architecture | `ClaimLens.Api.Tests/Architecture` | NetArchTest | The rules in section 4 |
| Golden | `ClaimLens.Api.Tests/Golden` | `fixtures/golden` | Definition of Done, with AI on and AI off. So far through intake, page check, extraction and review |
| Document AI | `apps/document-ai/tests` | pytest, Schemathesis | Tasks and contract; no provider is called |
| Web | Next to the code | Vitest, Testing Library, MSW | UI states |
| End to end | `e2e/` | Playwright, axe | The three task flows, the keyboard path, WCAG AA |

```text
fixtures/golden/
├── clean/
├── blurry/
├── missing-document/
├── duplicate/
├── conflicting/
├── excluded-line/
└── limit-capped/
    ├── documents/              # Synthetic files, as uploaded
    ├── ai-responses/           # Recorded answer per task, with ids as placeholders
    └── expected.json           # Statuses, alerts, flags, line results, payable
```

Today `clean/documents/` holds the golden packet (5 files, 6 pages), `blurry/documents/` one blurred receipt for US04, `invalid/` a file whose name lies about its type, and `excluded-line/` and `limit-capped/` the Rule Engine expectations. `clean/ai-responses/` and `blurry/ai-responses/` hold the recorded AI answers, replayed by the fake gateway and by the golden tests. `tools/golden/generate.mjs` makes the documents from HTML with Playwright's Chromium and writes the DOCX and XLSX files itself, with no dependency; two runs on one machine give the same bytes, and another machine's fonts change them. Each file stays small enough to commit directly, without Git LFS ([fixtures/golden/README.md](../fixtures/golden/README.md)).

## 11. Should stories

`Release:IncludeShould` in the API and `NEXT_PUBLIC_RELEASE_SHOULD` in the web app are off for the first release. While they are off:

- the 9 `should` operations answer 404, and `CLINICAL_CHECK` and `SUMMARY_DRAFT` are never queued;
- the web app hides the Risk tab, AI quality, Test run and the AI draft button;
- the summary box shows the structured template, the same one used when AI is down;
- "Clinical check not run" stays hidden, or it would appear on every claim.

AI-03 and AI-04 are Should stories, so a demo of all four AI capabilities needs US09, US14 and US15 brought forward.

## 12. Running it

| Service | Image | Port | Runs |
|---|---|---|---|
| `postgres` | `postgres:18` | 5432 | Always |
| `storage` | `chrislusf/seaweedfs:4.52` | 8333 | Always |
| `converter` | `gotenberg/gotenberg:8.39.0` | 3100 | Always |
| `proxy-dev` | `caddy:2`, to the applications on the host | 8088 | Daily work |
| `migrate` | Built from `apps/claims-api`; runs once | | Profile `full` |
| `claims-api` | Built from `apps/claims-api` | internal | Profile `full` |
| `document-ai` | Built from `apps/document-ai` | internal | Profile `full` |
| `web` | Built from `apps/web` | internal | Profile `full` |
| `proxy` | `caddy:2` | 8088 | Profile `full` |

For daily work, Compose runs the services and the proxy, and the applications run on the host. The browser always opens `http://localhost:8088`:

```bash
docker compose -f infra/compose.yaml up -d postgres storage converter proxy-dev
```

```bash
dotnet run --project apps/claims-api/src/ClaimLens.Api --launch-profile claims-api -- migrate
```

```bash
dotnet run --project apps/claims-api/src/ClaimLens.Api --launch-profile claims-api -- seed
```

```bash
dotnet watch --project apps/claims-api/src/ClaimLens.Api
```

```bash
uv run --directory apps/document-ai fastapi dev src/document_ai/asgi.py --port 8001
```

```bash
npm --prefix apps/web run dev
```

Use Chrome or Edge, which accept the `Secure` refresh cookie on `http://localhost`. Once it is chosen, hosting is the `full` profile on one machine, with Caddy handling TLS.

**CI** is GitHub Actions, with one workflow per area, each filtered by path:

| Workflow | Runs |
|---|---|
| `contract` | `redocly lint`, generated files are current, `schema:check` |
| `claims-api` | Build and all C# tests |
| `document-ai` | `ruff`, pytest, Schemathesis |
| `web` | ESLint, `tsc`, Vitest, `next build` |
| `e2e` | Compose profile `full`, then Playwright with axe |

## 13. Checked before this was written

Run on 04/11/2026 against the documents in this repository.

| Check | Result |
|---|---|
| `redocly lint` on both contracts | Valid |
| `dbml2sql`, then applying the result to PostgreSQL 18.6 (PGlite) | Applied: 21 tables, 36 enums, 65 checks, 64 foreign keys |
| The golden case as rows (06, section 9), through to the decision | Accepted by every constraint |
| Bad writes: a reviewer as decider, the submitter as decider, `APPROVE` below the claimed total, a value with no source, an override above the line, a policy of another member | Each refused by the constraint meant for it |
| All of the above again on real PostgreSQL 18.6 in Docker | The same, plus the triggers. A test found that the lifecycle trigger let a claim reach `APPROVED` with no recommendation at all, because the check came out NULL; fixed |
| Row-level security as 06 described it | The reviewer's duplicate lookup found 0 rows where 1 exists, and a job runner with no user saw 0 claims. This led to S9 |
| GIN index on `unaccent(full_name)` as 06 described it | Failed, because `unaccent` is not immutable. Fixed with a wrapper in [06](06-data-model.md#10-rules-the-dbml-cannot-express) |
| `openapi-typescript` on both contracts | Type-checks with TypeScript 5.9; the tool fails to run with 7.0 |
| `datamodel-code-generator` on `document-ai.yaml` | Pydantic v2 models import, and a confidence of 1.4 is refused |
| Schemathesis loading both contracts | 59 and 6 operations, no errors |
| Enums, error codes, role matrix and operation names across DBML, YAML, 04 and 07 | Match. The one difference is deliberate: Document AI's `PageType` leaves out `UNCLASSIFIED` |

Tried while scaffolding, on the real services:

| Check | Result |
|---|---|
| Gotenberg on a Vietnamese DOCX and XLSX | Diacritics and `₫` render correctly with the stock image, one page per worksheet page |
| Gotenberg on a file that is not really a DOCX | It answers 200 with a PDF of the garbage. This is why the API must check the first bytes |
| XLSX with default column widths | Long text is cut at the next filled cell, exactly as when printing from Excel. AI reads only what the rendition shows |
| SeaweedFS with the identities in `infra/storage/s3.json` | Document AI reads `pages/`; it gets `AccessDenied` on `originals/`, `ai-raw/`, on writes and on deletes |
| 20 MB through a Next.js development rewrite | 500 after 30 seconds, against 0.1 seconds directly. Replaced by Caddy, where it takes 0.3 seconds |
| Sign-in in a real browser through the proxy | Each role lands on its home with its own menu. Reload keeps the session, a page outside the role gives 403, sign-out ends the session, and script can read neither the cookie nor any token |
| Schemathesis against Document AI | Found that an unparsable body gave FastAPI's 400 instead of the contract's 422; fixed |
| The whole stack in Docker, with Playwright and axe | 12 browser tests pass, including WCAG AA on Sign in and the shell. Running it found that the Document AI container exited at startup, which no test covering an app built in-process could see; fixed, with a health check and a startup test |
| Each change to the DBML (column length, type, nullability, enum value, constraint and index name) | `schema:check` fails on each of them |

Still unproven: the AI Model Provider, and whether its confidence scores can carry the 0.95 threshold.

### Versions

Latest stable on 04/11/2026. Lock files pin the exact version when each part is scaffolded.

| Part | Versions |
|---|---|
| Web | Next.js 16.4, React 19.3, Tailwind 4.3, TypeScript 5.9 (not 7), TanStack Query 5.105, TanStack Table 9.3 (not in `package.json` yet; added with the review queue), React Hook Form 7.91, Zod 4.6, openapi-fetch 0.17, `@base-ui/react` 1.9, Vitest 5.1, MSW 3.0, Playwright 1.64 |
| Claims API | .NET 10, Npgsql EF Core 10.0, dbup-postgresql 7.0, PDFtoImage 5.4, AWSSDK.S3 4.0, Testcontainers 4.16, xUnit v3 4.0, NetArchTest 1.3, JsonSchema.Net 9.6 |
| Document AI | Python 3.12, FastAPI 0.162, Pydantic 2.14, boto3 1.43, httpx 0.28, pytest 9.2, Schemathesis 4.33, datamodel-code-generator 0.89 |
| Services | PostgreSQL 18, Gotenberg 8.39, SeaweedFS 4.52 |

## 14. What is built

Five slices work end to end: US01 sign-in, US19 rule sets, Slice 1 intake (US02, US03), Slice 2 AI reading (US04, US05) and Slice 3 the review queue and Evidence (US23, US06).

| Part | State |
|---|---|
| `infra/`, `db/`, generators, CI | Done |
| `ClaimLens.RuleEngine` | Done: US11 and US12 on the golden numbers |
| Claims API | Sign-in, the 8 Must operations of Rule Set Management, and the 12 of intake: `searchMembers`, `getPolicySummary`, `createClaim`, `updateClaim`, `uploadDocument`, `deleteDocument`, `submitClaim`, `listDocuments`, `getPage`, `getClaim`, `getClaimStatus` and `listClaims` for `MY_DRAFTS` and `MY_SUBMISSIONS`, plus `reclassifyPage`, and the 9 of review: `startReview`, `getClaimByNumber`, `listFields`, `updateField`, `acceptField`, `rerunExtraction`, `listExpenseLines`, `addExpenseLine` and `removeExpenseLine`, with the reviewer's four queue views. Document Store on S3, Document Preparation with Gotenberg, PDFium and SkiaSharp, `ClaimStatusMachine`, and the AI Job Runner with the page check, field extraction and page re-read appliers, `Thresholds` and `FieldCatalogue`. The role, scope, status and audit rules of all 59 operations run; the other 25 answer 501 until their features are built, and so do the approver's queue views |
| Document AI Service | All six routes with the fake gateway, which replays the recorded answers for the golden pages when `DOCUMENT_AI_REPLAY_DIR` is set (a page is recognised by its dHash and sharpness) |
| Staff Web App | Sign-in, shell, role routing, SCR-41 plans and versions, SCR-42 rule set editor, SCR-10 to SCR-13 intake, with the AI page check on SCR-13 (type, AI marker and level, Choose type, the fallback banner), SCR-20 the review queue, the top bar lookup, the Claim workspace with its page viewer, and SCR-31 Evidence. The approver's home is a placeholder, and the approver reaches Evidence by the lookup, read only |

US19 adds three checks the documents did not spell out, because the Rule Engine would otherwise quietly use one rule and ignore the other: a category has one coverage or exclusion rule, a version has one reimbursement rate, and one limit per scope. A repeat is refused with `VALIDATION_FAILED` and the field that repeats. The eligibility rule `ELIG-01` is added by the system with order 10, the value the seeded rule sets use, and has no tab. Rules apply by kind (eligibility, waiting periods, exclusions, coverage, then the rate and limits), and a save or publish that orders a rule before a kind that applies first is refused with `RULE_INVALID`. A version is published with an effective date of today or later in Vietnam time. US20 test runs (`testRuleSet`, `listSampleClaims`) are Should and still hidden.

Slice 1 queues a `PAGE_CHECK` run for each ready file and a `FIELD_EXTRACTION` run on submission ([seq-01-intake](diagrams/seq-01-intake.md)); Slice 2 runs them, fills the field slots of the [field catalogue](03-use-cases.md#field-catalogue) and moves the claim to `READY_FOR_REVIEW` or `NEEDS_VERIFICATION` ([seq-02-ai-reading](diagrams/seq-02-ai-reading.md)). A claim whose pages are all Other has no required field and is `READY_FOR_REVIEW`; the required-document check of US07 catches it. `ai_value` keeps a national ID in full, and `listFields` masks it. The fallback banner follows the latest run of each task, so a failed page check on an earlier file shows only on that file's pages once a later file was checked.

Slice 3 ([seq-03-review](diagrams/seq-03-review.md)) lets a reviewer take a claim and verify each field beside its source. What it leaves for later:

- **Below 1280 px** the workspace keeps a 1100 px minimum and the page scrolls sideways. The overlay for zone C and the dropdown for zone A that the [information architecture](04-information-architecture.md) asks for are not built. To settle with the non-functional requirements.
- **Slice 4a** adds `updateClaimFacts`, and fills `ClaimRecheck`.
- **Slice 4b:** a hand-classified invoice with no lines passes readiness, because readiness only checks the lines that exist. Calculation has to refuse it.
- **Slice 5:** `submitRecommendation` has to refuse an `IN_REVIEW` claim with an unresolved required field, and one with a `PAGE_REREAD` queued or running, because that applier does not check the claim's status.
- **A prescription read by hand** has no way to add a medication line: only an invoice has "Add expense line". To look at with US09, the first feature that reads medication.
- The fallback banner says "AI assistance unavailable" for the whole claim when one page's re-read failed, because `fallbackActive` follows the latest run of each task. The page's own message and its rail status are the precise ones.

Next, in the order the stories depend on each other: validation (US07, US08), then adjudication.

## Still open

| Item | Needed for | Assumption until then |
|---|---|---|
| AI Model Provider and pipelines | US04, US05, US10 | The fake gateway |
| How the fake gateway recognises a golden page | End-to-end tests | A hash of the page image, because a request carries only generated ids |
| Hosting | The first deployment | The `full` profile on one machine |
| Non-functional requirements | Logging, retention | `X-Request-Id` and `/health` only; no folder depends on the answer |
| Class and sequence diagrams | Detailed design of each feature | This document fixes where they apply |

**Related:** [C4 model](architecture/c4/README.md) · [API contract](07-api-contract.md) · [Data model](06-data-model.md) · [Information architecture](04-information-architecture.md) · [DESIGN.md](DESIGN.md)
