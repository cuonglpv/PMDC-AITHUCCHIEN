# Sequence: AI reading (Slice 2, US04 and US05)

> Built in Slice 2. Names are the `operationId`s of [claims-api.yaml](../api/claims-api.yaml) and [document-ai.yaml](../api/document-ai.yaml), and the components of the [Claims API C3 view](../architecture/c4/03-component-claims-api.md).

Slice 1 queues a `PAGE_CHECK` run for each file that reaches `READY` and a `FIELD_EXTRACTION` run when the claim is submitted ([seq-01-intake](seq-01-intake.md)). The AI Job Runner runs them, applies the BR-03 thresholds and moves the claim on. The AI reads; people decide what it read.

## Page check (US04)

```mermaid
sequenceDiagram
    autonumber
    participant Runner as AI Job Runner
    participant DB as PostgreSQL
    participant AI as Document AI Service
    participant Store as Object Storage
    actor Officer as Intake Officer
    participant Web as Staff Web App

    Runner->>DB: Oldest QUEUED run, FOR UPDATE SKIP LOCKED; RUNNING, pages READING; commit
    Runner->>AI: runPageCheck (page ids and image keys, the pipeline name and version)
    AI->>Store: Read each page image (pages/ only)
    AI-->>Runner: Type, confidence and quality per page, failedPages
    Runner->>Store: ai-raw/{claimId}/{runId}.json
    Runner->>DB: Lock the pages. Type in use: AI type at 0.80 or more, else UNCLASSIFIED,<br/>never over a type the officer chose. LOW_QUALITY below 0.50. Run SUCCEEDED, audit
    loop Every 3 seconds while a check is queued or running
        Web->>DB: getClaimStatus (through the Claims API)
    end
    Web-->>Officer: Type with the AI marker and High or Check, or Unclassified with Choose type, or Low quality
    opt Unclassified page
        Officer->>Web: Choose type
        Web->>DB: reclassifyPage: type in use, source USER, audit
    end
```

## Field extraction (US05)

```mermaid
sequenceDiagram
    autonumber
    participant Runner as AI Job Runner
    participant DB as PostgreSQL
    participant AI as Document AI Service
    participant Store as Object Storage

    Note over Runner,DB: Waits until the claim has no PAGE_CHECK QUEUED or RUNNING
    Runner->>DB: The FIELD_EXTRACTION run: RUNNING; commit
    Runner->>DB: Ready documents with a type, from the first classified page
    alt No document has a type with fields
        Runner->>DB: Nothing to ask: apply the readiness rule straight away
    else
        Runner->>AI: runFieldExtraction (documents, typed pages, the field catalogue)
        AI-->>Runner: Value, confidence, page and region per field and item
        Runner->>Store: ai-raw/{claimId}/{runId}.json
        Runner->>DB: Lock the claim. A slot for every catalogue field of the document's type:<br/>HIGH with a readable value, page and region → AUTO_ACCEPTED (audit acceptedBy = threshold)<br/>CHECK → NEEDS_VERIFICATION · below 0.70 or not found → NEEDS_ENTRY, AI value kept but never shown<br/>Each invoice item → an expense line from its auto-accepted values
    end
    Runner->>DB: ClaimStatusMachine: READY_FOR_REVIEW if every required slot is resolved<br/>and no page is UNCLASSIFIED, else NEEDS_VERIFICATION. Run SUCCEEDED, audit
```

## When the AI fails

```mermaid
sequenceDiagram
    autonumber
    participant Runner as AI Job Runner
    participant DB as PostgreSQL
    participant AI as Document AI Service

    Runner->>AI: runPageCheck or runFieldExtraction
    alt 5xx, 429, timeout, unreachable, or an answer off the contract
        AI-->>Runner: Error
        Runner->>DB: Back to QUEUED, waiting 10 s, then 20 s, 40 s and so on; other runs go ahead
        Note over Runner: After Workers:AiJobs:MaxAttempts (5) the run fails
    else 401 or 422
        AI-->>Runner: Error
    end
    Runner->>DB: Run FAILED with error_code, audit AI_RUN_FAILED
    alt Page check
        Runner->>DB: Its pages FAILED, Unclassified unless the officer chose a type
    else Field extraction
        Runner->>DB: Empty NEEDS_ENTRY header slots for each document of a known type,<br/>claim NEEDS_VERIFICATION
    end
    Note over DB: fallbackActive is true while the latest run of a task failed:<br/>the web shows the fallback banner and the work goes on by hand.<br/>A run left RUNNING past its timeout plus 30 s fails as TIMEOUT.
```
