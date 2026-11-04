# C3: Component, Document AI Service

> Design status: TARGET MVP
>
> Purpose: zoom into the Document AI Service and show how the six AI tasks are kept to suggestions only.

```mermaid
---
title: Component diagram for the Document AI Service
config:
  flowchart:
    wrappingWidth: 320
    subGraphTitleMargin:
      top: 4
      bottom: 28
---
flowchart TB
    api["Claims API<br/><i>[Container: ASP.NET Core]</i>"]

    subgraph service["Document AI Service [Container]"]
        tasks["Task API<br/><i>[Component: FastAPI, Pydantic]</i><br/>One operation per AI task,<br/>checks input and output shapes"]
        check["Page Check<br/><i>[Component: Python]</i><br/>Page type and quality score"]
        extract["Field Extraction<br/><i>[Component: Python]</i><br/>Field values with page,<br/>bounding box and confidence"]
        mapping["Benefit Mapping<br/><i>[Component: Python]</i><br/>Expense line text to<br/>benefit category"]
        clinical["Clinical Signals<br/><i>[Component: Python]</i><br/>Signal type, relevance score<br/>and one-sentence reason"]
        summary["Summary Draft<br/><i>[Component: Python]</i><br/>Editable text with<br/>a citation per sentence"]
        pages["Page Reader<br/><i>[Component: boto3]</i><br/>Loads page images by key"]
        gateway["Model Gateway<br/><i>[Component: Python HTTP client]</i><br/>The only caller of the provider;<br/>timeouts and response checks"]
    end

    storage[("Object Storage<br/><i>[Container: S3-compatible]</i>")]
    provider["AI Model Provider<br/><i>[External Software System]</i>"]

    api -- "Requests one AI task, receives suggestions<br/>[HTTPS, JSON]" --> tasks
    tasks -- "PAGE_CHECK" --> check
    tasks -- "FIELD_EXTRACTION, PAGE_REREAD" --> extract
    tasks -- "BENEFIT_MAPPING" --> mapping
    tasks -- "CLINICAL_CHECK" --> clinical
    tasks -- "SUMMARY_DRAFT" --> summary
    check -- "Loads page images" --> pages
    extract -- "Loads page images" --> pages
    check -- "Asks for page type and quality" --> gateway
    extract -- "Asks for OCR and field values" --> gateway
    mapping -- "Asks for a category" --> gateway
    clinical -- "Asks for a relevance judgement" --> gateway
    summary -- "Asks for a draft" --> gateway
    pages -- "Reads objects, read-only<br/>[S3 API, HTTPS]" --> storage
    gateway -- "Sends pages and text, receives model output<br/>[HTTPS, JSON]" --> provider

    subgraph key["Key"]
        direction LR
        keyComponent["AI component"]
        keyNeighbour["Neighbouring container"]
        keyExternal["External system"]
    end

    classDef aiBox fill:#7c3aed,color:#fff,stroke:#5b21b6
    classDef neighbour fill:#dbe6fb,color:#123c99,stroke:#1a56db
    classDef external fill:#6b7280,color:#fff,stroke:#374151,stroke-dasharray:5 3
    class tasks,check,extract,mapping,clinical,summary,pages,gateway,keyComponent aiBox
    class api,storage,keyNeighbour neighbour
    class provider,keyExternal external
    style service fill:transparent,stroke:#7c3aed,stroke-dasharray:5 5
    style key fill:transparent,stroke:#888
```

## Component catalogue

(Should) marks a story that follows in the next increment.

| Component | AI use case and `ai_task` | Returns | Does not do | Stories |
|---|---|---|---|---|
| Task API | All | The task's result plus the model name and version used | Business rules | All |
| Page Check | AI-01, `PAGE_CHECK` | Page type, type confidence, quality score | Mark a page Unclassified or Low quality; the Claims API does, from the score | US04 |
| Field Extraction | AI-01, `FIELD_EXTRACTION`, `PAGE_REREAD` | For each field in the catalogue: value, page, bounding box (0 to 1), confidence | Guess a value it cannot find; it returns none | US05, US06 |
| Benefit Mapping | AI-02, `BENEFIT_MAPPING` | Benefit category and confidence per line, from the list it was given | Decide coverage or amounts | US10 |
| Clinical Signals | AI-03, `CLINICAL_CHECK` | Signal type, relevance score and reason per line | Raise a flag, set its severity or call anything fraud; the Claims API creates the flag and sets the severity from the score | US09 (Should) |
| Summary Draft | AI-04, `SUMMARY_DRAFT` | Sentences with citations to the items it was given | Use facts beyond those items, or submit anything | US15 (Should) |
| Page Reader | AI-01 tasks | Page images | Write to storage | US04, US05 |
| Model Gateway | All | Checked model output | Choose the model; the request names it | All |

## Rules that shape the structure

1. **The service is stateless.** It has no database, writes no files and remembers nothing between requests. The Claims API saves the full response as `raw_output_key`, so a run can be examined later without the service keeping anything.
2. **It reports, it does not decide.** Every result is a raw score or a suggestion. All thresholds (BR-03, quality and clinical) are applied by the [AI Job Runner](03-component-claims-api.md#how-ai-jobs-run).
3. **Input is limited to what the task needs.** Only Page Check and Field Extraction can read images. Benefit Mapping gets line text and the category list. Summary Draft gets only accepted facts, rule codes, overrides and kept flags, which is what makes its citations checkable (US15).
4. **Every value has a source.** A field without a page and bounding box cannot be accepted (BR-02), so Field Extraction returns the source with each value.
5. **Bad model output is a failed call.** Model Gateway checks each response against the expected shape. Failures reach the Claims API as an error, and the claim follows the normal [fallback](02-container.md#when-a-container-is-down). A single page that cannot be read is reported in the response, and only that page falls back.
6. **One way out to the provider.** Keeping the provider behind Model Gateway limits a change of provider to one component, and gives one place to control what data leaves ClaimLens.

**Previous:** [C2: Container](02-container.md) · **See also:** [C3: Claims API](03-component-claims-api.md) · **Up:** [C4 overview](README.md)

**Sources of truth:** [AI placement matrix](../../03-use-cases.md#ai-placement-matrix), [INVEST requirements](../../08-invest-requirements.md), [claimlens.dbml](../../diagrams/claimlens.dbml), [document-ai.yaml](../../api/document-ai.yaml).
