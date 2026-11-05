-- SQL dump generated using DBML (dbml.dbdiagram.io)
-- Database: PostgreSQL

CREATE TYPE "staff_role" AS ENUM (
  'INTAKE_OFFICER',
  'CLAIMS_REVIEWER',
  'SENIOR_APPROVER',
  'ADMINISTRATOR'
);

CREATE TYPE "account_status" AS ENUM (
  'ACTIVE',
  'DISABLED'
);

CREATE TYPE "policy_status" AS ENUM (
  'ACTIVE',
  'LAPSED',
  'TERMINATED'
);

CREATE TYPE "rule_set_status" AS ENUM (
  'DRAFT',
  'PUBLISHED',
  'DISCARDED'
);

CREATE TYPE "rule_type" AS ENUM (
  'ELIGIBILITY',
  'COVERAGE',
  'EXCLUSION',
  'WAITING_PERIOD',
  'COINSURANCE',
  'LIMIT'
);

CREATE TYPE "limit_scope" AS ENUM (
  'PER_VISIT',
  'ANNUAL'
);

CREATE TYPE "claim_status" AS ENUM (
  'DRAFT',
  'PROCESSING',
  'NEEDS_VERIFICATION',
  'READY_FOR_REVIEW',
  'IN_REVIEW',
  'PENDING_APPROVAL',
  'APPROVED',
  'PARTIALLY_APPROVED',
  'REJECTED'
);

CREATE TYPE "intake_channel" AS ENUM (
  'COUNTER',
  'POST',
  'EMAIL',
  'PARTNER'
);

CREATE TYPE "file_format" AS ENUM (
  'JPG',
  'PNG',
  'PDF',
  'DOCX',
  'XLSX'
);

CREATE TYPE "document_status" AS ENUM (
  'UPLOADING',
  'CONVERTING',
  'READY',
  'REJECTED',
  'REMOVED'
);

CREATE TYPE "page_type" AS ENUM (
  'INVOICE',
  'RECEIPT',
  'PRESCRIPTION',
  'VISIT_SUMMARY',
  'LAB_RESULT',
  'OTHER',
  'UNCLASSIFIED'
);

CREATE TYPE "page_quality" AS ENUM (
  'NOT_CHECKED',
  'READABLE',
  'LOW_QUALITY'
);

CREATE TYPE "page_ai_status" AS ENUM (
  'QUEUED',
  'READING',
  'DONE',
  'FAILED'
);

CREATE TYPE "value_source" AS ENUM (
  'AI',
  'USER'
);

CREATE TYPE "ai_task" AS ENUM (
  'PAGE_CHECK',
  'FIELD_EXTRACTION',
  'PAGE_REREAD',
  'BENEFIT_MAPPING',
  'CLINICAL_CHECK',
  'SUMMARY_DRAFT'
);

CREATE TYPE "ai_run_status" AS ENUM (
  'QUEUED',
  'RUNNING',
  'SUCCEEDED',
  'FAILED'
);

CREATE TYPE "confidence_level" AS ENUM (
  'HIGH',
  'CHECK',
  'MISSING'
);

CREATE TYPE "field_key" AS ENUM (
  'insured_name',
  'national_id',
  'policy_number',
  'provider_name',
  'provider_tax_code',
  'service_date',
  'invoice_number',
  'invoice_total',
  'line_description',
  'line_quantity',
  'line_unit_price',
  'line_amount',
  'diagnosis',
  'medication'
);

CREATE TYPE "field_status" AS ENUM (
  'NEEDS_VERIFICATION',
  'NEEDS_ENTRY',
  'AUTO_ACCEPTED',
  'CONFIRMED',
  'CORRECTED',
  'ENTERED',
  'NOT_PRESENT'
);

CREATE TYPE "line_origin" AS ENUM (
  'AI_EXTRACTED',
  'MANUAL'
);

CREATE TYPE "mapping_status" AS ENUM (
  'PENDING',
  'AUTO_ACCEPTED',
  'NEEDS_CONFIRMATION',
  'CONFIRMED',
  'CHANGED',
  'MANUAL'
);

CREATE TYPE "alert_check" AS ENUM (
  'REQUIRED_DOCUMENTS',
  'INSURED_IDENTITY',
  'POLICY_MATCH',
  'DATE_CONSISTENCY',
  'INVOICE_ARITHMETIC',
  'CLAIMED_TOTAL_MISMATCH',
  'DUPLICATE_INVOICE'
);

CREATE TYPE "alert_severity" AS ENUM (
  'BLOCKING',
  'WARNING'
);

CREATE TYPE "alert_status" AS ENUM (
  'OPEN',
  'RESOLVED',
  'DISMISSED'
);

CREATE TYPE "flag_type" AS ENUM (
  'MEDICATION_DIAGNOSIS',
  'SERVICE_DIAGNOSIS',
  'LAB_DIAGNOSIS',
  'QUANTITY'
);

CREATE TYPE "flag_severity" AS ENUM (
  'LOW',
  'MEDIUM',
  'HIGH'
);

CREATE TYPE "flag_status" AS ENUM (
  'OPEN',
  'DISMISSED',
  'KEPT',
  'SUPERSEDED'
);

CREATE TYPE "eval_label" AS ENUM (
  'TRUE_POSITIVE',
  'FALSE_POSITIVE'
);

CREATE TYPE "reason_code" AS ENUM (
  'DISMISS_VALUE_VERIFIED',
  'DISMISS_CLINICALLY_PLAUSIBLE',
  'DISMISS_KNOWN_EXCEPTION',
  'KEEP_NEEDS_APPROVER',
  'KEEP_EVIDENCE_MISSING',
  'OVERRIDE_MEDICAL_NECESSITY',
  'OVERRIDE_CONTRACT_EXCEPTION',
  'OVERRIDE_DATA_ERROR',
  'OUTCOME_EXCLUSION_AND_LIMIT',
  'OUTCOME_MEDICAL_NECESSITY',
  'OUTCOME_GENERAL_EXCLUSION',
  'OUTCOME_ANNUAL_MAXIMUM',
  'OUTCOME_OUTSIDE_COVERAGE',
  'OUTCOME_EVIDENCE_MISSING'
);

CREATE TYPE "line_result" AS ENUM (
  'COVERED',
  'PARTIALLY_COVERED',
  'EXCLUDED'
);

CREATE TYPE "revision_trigger" AS ENUM (
  'INITIAL',
  'DATA_CHANGE',
  'MAPPING_CHANGE',
  'OVERRIDE',
  'BALANCE_RECHECK'
);

CREATE TYPE "claim_outcome" AS ENUM (
  'APPROVED',
  'PARTIALLY_APPROVED',
  'REJECTED'
);

CREATE TYPE "recommendation_status" AS ENUM (
  'DRAFT',
  'SUBMITTED',
  'RETURNED',
  'DECIDED'
);

CREATE TYPE "decision_action" AS ENUM (
  'APPROVE',
  'PARTIALLY_APPROVE',
  'REJECT',
  'RETURN'
);

CREATE TYPE "actor_type" AS ENUM (
  'USER',
  'SYSTEM',
  'AI'
);

CREATE TYPE "golden_scenario" AS ENUM (
  'CLEAN',
  'BLURRY',
  'MISSING_DOCUMENT',
  'DUPLICATE',
  'CONFLICTING',
  'EXCLUDED_LINE',
  'LIMIT_CAPPED'
);

CREATE TABLE "staff_account" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "email" varchar(254) NOT NULL,
  "password_hash" varchar(255) NOT NULL,
  "full_name" varchar(120) NOT NULL,
  "initials" varchar(4) NOT NULL,
  "role" staff_role NOT NULL,
  "status" account_status NOT NULL DEFAULT 'ACTIVE',
  "last_sign_in_at" timestamptz,
  "created_at" timestamptz NOT NULL DEFAULT (now())
);

CREATE TABLE "refresh_token" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "staff_account_id" uuid NOT NULL,
  "family_id" uuid NOT NULL,
  "token_hash" char(64) UNIQUE NOT NULL,
  "issued_at" timestamptz NOT NULL DEFAULT (now()),
  "expires_at" timestamptz NOT NULL,
  "rotated_at" timestamptz,
  "revoked_at" timestamptz,
  CONSTRAINT "ck_refresh_token_expiry" CHECK (expires_at > issued_at)
);

CREATE TABLE "member" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "member_code" varchar(20) UNIQUE NOT NULL,
  "full_name" varchar(120) NOT NULL,
  "national_id" varchar(20) UNIQUE NOT NULL,
  "date_of_birth" date NOT NULL,
  "created_at" timestamptz NOT NULL DEFAULT (now())
);

CREATE TABLE "benefit_plan" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "code" varchar(20) UNIQUE NOT NULL,
  "name" varchar(80) NOT NULL,
  "description" text,
  "created_at" timestamptz NOT NULL DEFAULT (now())
);

CREATE TABLE "policy" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "policy_number" varchar(20) UNIQUE NOT NULL,
  "member_id" uuid NOT NULL,
  "plan_id" uuid NOT NULL,
  "status" policy_status NOT NULL,
  "coverage_start" date NOT NULL,
  "coverage_end" date NOT NULL,
  "first_enrolled_on" date NOT NULL,
  "opening_consumed" bigint NOT NULL DEFAULT 0,
  "consumed_amount" bigint NOT NULL DEFAULT 0,
  "balance_version" integer NOT NULL DEFAULT 0,
  "created_at" timestamptz NOT NULL DEFAULT (now()),
  CONSTRAINT "ck_policy_period" CHECK (coverage_end > coverage_start),
  CONSTRAINT "ck_policy_enrolment" CHECK (first_enrolled_on <= coverage_start),
  CONSTRAINT "ck_policy_consumed" CHECK (opening_consumed >= 0 and consumed_amount >= opening_consumed)
);

CREATE TABLE "rule_set_version" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "plan_id" uuid NOT NULL,
  "version_no" integer NOT NULL,
  "status" rule_set_status NOT NULL DEFAULT 'DRAFT',
  "effective_from" date,
  "based_on_version_id" uuid,
  "change_note" text,
  "created_by" uuid NOT NULL,
  "created_at" timestamptz NOT NULL DEFAULT (now()),
  "published_by" uuid,
  "published_at" timestamptz,
  CONSTRAINT "ck_rule_set_published" CHECK (status <> 'PUBLISHED' or (effective_from is not null and published_by is not null and published_at is not null))
);

CREATE TABLE "benefit_category" (
  "code" varchar(40) PRIMARY KEY,
  "name" varchar(80) NOT NULL,
  "description" text,
  "is_active" boolean NOT NULL DEFAULT true
);

CREATE TABLE "benefit_rule" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "rule_set_version_id" uuid NOT NULL,
  "rule_code" varchar(20) NOT NULL,
  "rule_type" rule_type NOT NULL,
  "category_code" varchar(40),
  "clause_text" text NOT NULL,
  "clause_ref" varchar(40),
  "apply_order" smallint NOT NULL,
  "rate_bp" integer,
  "limit_amount" bigint,
  "limit_scope" limit_scope,
  "waiting_days" integer,
  CONSTRAINT "ck_rule_category" CHECK (rule_type not in ('COVERAGE', 'EXCLUSION') or category_code is not null),
  CONSTRAINT "ck_rule_rate" CHECK (rule_type <> 'COINSURANCE' or rate_bp between 0 and 10000),
  CONSTRAINT "ck_rule_limit" CHECK (rule_type <> 'LIMIT' or (limit_amount > 0 and limit_scope is not null)),
  CONSTRAINT "ck_rule_waiting" CHECK (rule_type <> 'WAITING_PERIOD' or waiting_days >= 0)
);

CREATE TABLE "sample_claim" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "code" varchar(30) UNIQUE NOT NULL,
  "name" varchar(120) NOT NULL,
  "plan_id" uuid NOT NULL,
  "service_date" date NOT NULL,
  "first_enrolled_on" date NOT NULL,
  "consumed_before" bigint NOT NULL DEFAULT 0,
  "lines" jsonb NOT NULL,
  "created_by" uuid NOT NULL,
  "created_at" timestamptz NOT NULL DEFAULT (now()),
  CONSTRAINT "ck_sample_claim_lines" CHECK (jsonb_array_length(lines) > 0)
);

CREATE TABLE "claim" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "claim_number" varchar(20) UNIQUE NOT NULL,
  "member_id" uuid NOT NULL,
  "policy_id" uuid NOT NULL,
  "provider_name" varchar(200) NOT NULL,
  "service_date" date NOT NULL,
  "claimed_total" bigint NOT NULL,
  "channel" intake_channel NOT NULL,
  "status" claim_status NOT NULL DEFAULT 'DRAFT',
  "status_changed_at" timestamptz NOT NULL DEFAULT (now()),
  "registered_by" uuid NOT NULL,
  "assignee_id" uuid,
  "assigned_at" timestamptz,
  "submitted_at" timestamptz,
  "golden_scenario" golden_scenario,
  "row_version" integer NOT NULL DEFAULT 0,
  "created_at" timestamptz NOT NULL DEFAULT (now()),
  "updated_at" timestamptz NOT NULL DEFAULT (now()),
  CONSTRAINT "ck_claim_total" CHECK (claimed_total > 0),
  CONSTRAINT "ck_claim_assignee_required" CHECK (assignee_id is not null or status in ('DRAFT', 'PROCESSING', 'NEEDS_VERIFICATION', 'READY_FOR_REVIEW')),
  CONSTRAINT "ck_claim_assigned_at" CHECK ((assignee_id is null) = (assigned_at is null))
);

CREATE TABLE "document" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "claim_id" uuid NOT NULL,
  "original_filename" varchar(255) NOT NULL,
  "file_format" file_format NOT NULL,
  "size_bytes" bigint NOT NULL,
  "sha256" char(64) NOT NULL,
  "original_key" varchar(500) NOT NULL,
  "rendition_key" varchar(500),
  "page_count" smallint,
  "status" document_status NOT NULL DEFAULT 'UPLOADING',
  "rejection_reason" varchar(200),
  "uploaded_by" uuid NOT NULL,
  "uploaded_at" timestamptz NOT NULL DEFAULT (now()),
  "removed_by" uuid,
  "removed_at" timestamptz,
  CONSTRAINT "ck_document_size_20mb" CHECK (size_bytes between 1 and 20971520),
  CONSTRAINT "ck_document_rendition" CHECK (file_format not in ('DOCX', 'XLSX') or status <> 'READY' or rendition_key is not null),
  CONSTRAINT "ck_document_rejection_reason" CHECK (status <> 'REJECTED' or rejection_reason is not null),
  CONSTRAINT "ck_document_removed" CHECK ((status = 'REMOVED') = (removed_at is not null))
);

CREATE TABLE "document_page" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "document_id" uuid NOT NULL,
  "claim_id" uuid NOT NULL,
  "page_no" smallint NOT NULL,
  "claim_page_no" smallint NOT NULL,
  "image_key" varchar(500) NOT NULL,
  "width_px" integer NOT NULL,
  "height_px" integer NOT NULL,
  "page_type" page_type NOT NULL DEFAULT 'UNCLASSIFIED',
  "page_type_source" value_source,
  "page_type_set_by" uuid,
  "quality" page_quality NOT NULL DEFAULT 'NOT_CHECKED',
  "ai_status" page_ai_status NOT NULL DEFAULT 'QUEUED',
  "ai_run_id" uuid,
  "ai_page_type" page_type,
  "ai_type_confidence" numeric(4,3),
  "ai_type_level" confidence_level,
  "ai_quality_score" numeric(4,3),
  CONSTRAINT "ck_page_limit_30" CHECK (claim_page_no between 1 and 30),
  CONSTRAINT "ck_page_type_set_by" CHECK (page_type_source is distinct from 'USER' or page_type_set_by is not null),
  CONSTRAINT "ck_page_ai_confidence" CHECK (ai_type_confidence is null or ai_type_confidence between 0 and 1)
);

CREATE TABLE "ai_run" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "claim_id" uuid NOT NULL,
  "task" ai_task NOT NULL,
  "scope_document_id" uuid,
  "scope_page_id" uuid,
  "model_name" varchar(80) NOT NULL,
  "model_version" varchar(40) NOT NULL,
  "status" ai_run_status NOT NULL DEFAULT 'QUEUED',
  "error_code" varchar(60),
  "triggered_by" uuid,
  "requested_at" timestamptz NOT NULL DEFAULT (now()),
  "started_at" timestamptz,
  "finished_at" timestamptz,
  "raw_output_key" varchar(500),
  CONSTRAINT "ck_ai_run_reread_scope" CHECK (task <> 'PAGE_REREAD' or scope_page_id is not null),
  CONSTRAINT "ck_ai_run_error" CHECK (status <> 'FAILED' or error_code is not null)
);

CREATE TABLE "claim_field" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "claim_id" uuid NOT NULL,
  "document_id" uuid NOT NULL,
  "field_key" field_key NOT NULL,
  "item_no" smallint NOT NULL DEFAULT 0,
  "expense_line_id" uuid,
  "status" field_status NOT NULL,
  "value_text" text,
  "value_amount" bigint,
  "value_date" date,
  "source_page_id" uuid,
  "source_bbox" jsonb,
  "ai_run_id" uuid,
  "ai_value" text,
  "ai_confidence" numeric(4,3),
  "ai_level" confidence_level,
  "ai_page_id" uuid,
  "ai_bbox" jsonb,
  "resolved_by" uuid,
  "resolved_at" timestamptz,
  "row_version" integer NOT NULL DEFAULT 0,
  CONSTRAINT "ck_claim_field_source_required" CHECK (status not in ('AUTO_ACCEPTED', 'CONFIRMED', 'CORRECTED', 'ENTERED') or (value_text is not null and source_page_id is not null and source_bbox is not null)),
  CONSTRAINT "ck_claim_field_resolved_by" CHECK ((status in ('CONFIRMED', 'CORRECTED', 'ENTERED', 'NOT_PRESENT')) = (resolved_by is not null)),
  CONSTRAINT "ck_claim_field_auto_from_ai" CHECK (status <> 'AUTO_ACCEPTED' or (ai_run_id is not null and ai_level = 'HIGH')),
  CONSTRAINT "ck_claim_field_ai_confidence" CHECK (ai_confidence is null or ai_confidence between 0 and 1)
);

CREATE TABLE "expense_line" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "claim_id" uuid NOT NULL,
  "document_id" uuid NOT NULL,
  "line_no" smallint NOT NULL,
  "description" text,
  "quantity" numeric(10,2),
  "unit_price" bigint,
  "amount" bigint,
  "origin" line_origin NOT NULL DEFAULT 'AI_EXTRACTED',
  "mapping_run_id" uuid,
  "ai_category_code" varchar(40),
  "ai_confidence" numeric(4,3),
  "ai_level" confidence_level,
  "category_code" varchar(40),
  "mapping_status" mapping_status NOT NULL DEFAULT 'PENDING',
  "mapping_resolved_by" uuid,
  "mapping_resolved_at" timestamptz,
  "override_result" line_result,
  "override_amount" bigint,
  "override_reason" reason_code,
  "override_note" text,
  "override_by" uuid,
  "override_at" timestamptz,
  "removed_by" uuid,
  "removed_at" timestamptz,
  CONSTRAINT "ck_expense_line_amount" CHECK (amount is null or amount >= 0),
  CONSTRAINT "ck_expense_line_category" CHECK (mapping_status in ('PENDING', 'NEEDS_CONFIRMATION') or category_code is not null),
  CONSTRAINT "ck_expense_line_mapping_by" CHECK ((mapping_status in ('CONFIRMED', 'CHANGED', 'MANUAL')) = (mapping_resolved_by is not null)),
  CONSTRAINT "ck_expense_line_auto_from_ai" CHECK (mapping_status <> 'AUTO_ACCEPTED' or (mapping_run_id is not null and ai_level = 'HIGH')),
  CONSTRAINT "ck_override_complete" CHECK ((override_result is null and override_amount is null and override_reason is null and override_note is null and override_by is null and override_at is null) or (override_result is not null and override_amount is not null and override_reason is not null and override_note is not null and override_by is not null and override_at is not null)),
  CONSTRAINT "ck_override_cap" CHECK (override_amount is null or override_amount between 0 and amount),
  CONSTRAINT "ck_override_result" CHECK ((override_result <> 'COVERED' or override_amount = amount) and (override_result <> 'EXCLUDED' or override_amount = 0) and (override_result <> 'PARTIALLY_COVERED' or override_amount between 1 and amount - 1)),
  CONSTRAINT "ck_override_reason_kind" CHECK (override_reason is null or override_reason::text like 'OVERRIDE_%'),
  CONSTRAINT "ck_expense_line_removed" CHECK ((removed_at is null) = (removed_by is null))
);

CREATE TABLE "alert" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "claim_id" uuid NOT NULL,
  "check_code" alert_check NOT NULL,
  "severity" alert_severity NOT NULL,
  "fingerprint" varchar(200) NOT NULL,
  "message" text NOT NULL,
  "details" jsonb,
  "duplicate_of_claim_id" uuid,
  "status" alert_status NOT NULL DEFAULT 'OPEN',
  "raised_at" timestamptz NOT NULL DEFAULT (now()),
  "last_checked_at" timestamptz NOT NULL DEFAULT (now()),
  "resolved_by" uuid,
  "resolved_at" timestamptz,
  "reason_code" reason_code,
  "note" text,
  CONSTRAINT "ck_alert_dismiss_reason" CHECK (status <> 'DISMISSED' or (reason_code is not null and note is not null and resolved_by is not null)),
  CONSTRAINT "ck_alert_reason_kind" CHECK (reason_code is null or reason_code::text like 'DISMISS_%'),
  CONSTRAINT "ck_alert_duplicate_link" CHECK (check_code <> 'DUPLICATE_INVOICE' or duplicate_of_claim_id is not null),
  CONSTRAINT "ck_alert_not_self" CHECK (duplicate_of_claim_id is null or duplicate_of_claim_id <> claim_id),
  CONSTRAINT "ck_alert_total_not_dismissible" CHECK (status <> 'DISMISSED' or check_code <> 'CLAIMED_TOTAL_MISMATCH')
);

CREATE TABLE "risk_flag" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "claim_id" uuid NOT NULL,
  "ai_run_id" uuid NOT NULL,
  "expense_line_id" uuid NOT NULL,
  "diagnosis_field_id" uuid,
  "flag_type" flag_type NOT NULL,
  "severity" flag_severity NOT NULL,
  "relevance_score" numeric(4,3) NOT NULL,
  "ai_reason" text NOT NULL,
  "status" flag_status NOT NULL DEFAULT 'OPEN',
  "reviewed_by" uuid,
  "reviewed_at" timestamptz,
  "reason_code" reason_code,
  "note" text,
  "eval_label" eval_label,
  "created_at" timestamptz NOT NULL DEFAULT (now()),
  CONSTRAINT "ck_risk_flag_reason" CHECK (status not in ('DISMISSED', 'KEPT') or (reviewed_by is not null and reason_code is not null and note is not null)),
  CONSTRAINT "ck_risk_flag_reason_kind" CHECK ((status <> 'DISMISSED' or reason_code::text like 'DISMISS_%') and (status <> 'KEPT' or reason_code::text like 'KEEP_%'))
);

CREATE TABLE "calculation_revision" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "claim_id" uuid NOT NULL,
  "revision_no" integer NOT NULL,
  "rule_set_version_id" uuid NOT NULL,
  "trigger_type" revision_trigger NOT NULL,
  "triggered_by" uuid,
  "engine_version" varchar(20) NOT NULL,
  "input_hash" char(64) NOT NULL,
  "claimed_total" bigint NOT NULL,
  "eligible_total" bigint NOT NULL,
  "after_rate_amount" bigint NOT NULL,
  "per_visit_limit" bigint,
  "annual_limit" bigint,
  "consumed_before" bigint NOT NULL,
  "balance_version" integer NOT NULL,
  "remaining_limit" bigint NOT NULL,
  "payable_amount" bigint NOT NULL,
  "projected_remaining_after" bigint NOT NULL,
  "formula" jsonb NOT NULL,
  "created_at" timestamptz NOT NULL DEFAULT (now()),
  CONSTRAINT "ck_revision_eligible" CHECK (eligible_total between 0 and claimed_total),
  CONSTRAINT "ck_revision_after_rate" CHECK (after_rate_amount between 0 and eligible_total),
  CONSTRAINT "ck_revision_payable_formula" CHECK (payable_amount = least(after_rate_amount, greatest(remaining_limit, 0)))
);

CREATE TABLE "calculation_line" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "revision_id" uuid NOT NULL,
  "expense_line_id" uuid NOT NULL,
  "line_no" smallint NOT NULL,
  "category_code" varchar(40) NOT NULL,
  "claimed_amount" bigint NOT NULL,
  "engine_result" line_result NOT NULL,
  "engine_covered_amount" bigint NOT NULL,
  "engine_rule_id" uuid NOT NULL,
  "is_overridden" boolean NOT NULL DEFAULT false,
  "final_result" line_result NOT NULL,
  "final_covered_amount" bigint NOT NULL,
  "changed_since_previous" boolean NOT NULL DEFAULT false,
  CONSTRAINT "ck_calc_line_engine_amount" CHECK (engine_covered_amount between 0 and claimed_amount),
  CONSTRAINT "ck_calc_line_final_amount" CHECK (final_covered_amount between 0 and claimed_amount),
  CONSTRAINT "ck_calc_line_override" CHECK (is_overridden or (final_result = engine_result and final_covered_amount = engine_covered_amount)),
  CONSTRAINT "ck_calc_line_engine_result" CHECK ((engine_result <> 'COVERED' or engine_covered_amount = claimed_amount) and (engine_result <> 'EXCLUDED' or engine_covered_amount = 0) and (engine_result <> 'PARTIALLY_COVERED' or engine_covered_amount between 1 and claimed_amount - 1)),
  CONSTRAINT "ck_calc_line_final_result" CHECK ((final_result <> 'COVERED' or final_covered_amount = claimed_amount) and (final_result <> 'EXCLUDED' or final_covered_amount = 0) and (final_result <> 'PARTIALLY_COVERED' or final_covered_amount between 1 and claimed_amount - 1))
);

CREATE TABLE "recommendation" (
  "id" uuid PRIMARY KEY DEFAULT (gen_random_uuid()),
  "claim_id" uuid NOT NULL,
  "calculation_revision_id" uuid NOT NULL,
  "status" recommendation_status NOT NULL DEFAULT 'DRAFT',
  "proposed_outcome" claim_outcome NOT NULL,
  "recommended_outcome" claim_outcome,
  "outcome_reason" reason_code,
  "outcome_note" text,
  "recommended_payable" bigint,
  "claimed_total" bigint,
  "summary" jsonb,
  "summary_ai_run_id" uuid,
  "summary_ai_draft" text,
  "summary_edited" boolean NOT NULL DEFAULT false,
  "attested_values" jsonb,
  "attested_at" timestamptz,
  "open_high_flags" smallint NOT NULL DEFAULT 0,
  "submitted_by" uuid,
  "submitted_at" timestamptz,
  "decision_action" decision_action,
  "decided_by" uuid,
  "decider_role" staff_role NOT NULL DEFAULT 'SENIOR_APPROVER',
  "decided_at" timestamptz,
  "decision_revision_id" uuid,
  "final_payable" bigint,
  "decision_reason" reason_code,
  "decision_comment" text,
  "balance_version_checked" integer,
  "created_at" timestamptz NOT NULL DEFAULT (now()),
  CONSTRAINT "ck_recommendation_submitted" CHECK (status = 'DRAFT' or (recommended_outcome is not null and recommended_payable is not null and claimed_total is not null and submitted_by is not null and submitted_at is not null and attested_at is not null)),
  CONSTRAINT "ck_recommendation_mismatch_reason" CHECK (recommended_outcome is null or recommended_outcome = proposed_outcome or (outcome_reason is not null and outcome_note is not null)),
  CONSTRAINT "ck_recommendation_reason_kind" CHECK (outcome_reason is null or outcome_reason::text like 'OUTCOME_%'),
  CONSTRAINT "ck_decision_present" CHECK ((status in ('RETURNED', 'DECIDED')) = (decision_action is not null)),
  CONSTRAINT "ck_decision_complete" CHECK (decision_action is null or (decided_by is not null and decided_at is not null and decision_revision_id is not null)),
  CONSTRAINT "ck_decision_return_status" CHECK ((decision_action = 'RETURN') = (status = 'RETURNED')),
  CONSTRAINT "ck_decision_maker_checker" CHECK (decided_by is null or decided_by <> submitted_by),
  CONSTRAINT "ck_decision_role" CHECK (decider_role = 'SENIOR_APPROVER'),
  CONSTRAINT "ck_decision_br13_approved" CHECK (decision_action is distinct from 'APPROVE' or final_payable = claimed_total),
  CONSTRAINT "ck_decision_br13_partial" CHECK (decision_action is distinct from 'PARTIALLY_APPROVE' or (final_payable > 0 and final_payable < claimed_total)),
  CONSTRAINT "ck_decision_br13_rejected" CHECK (decision_action is distinct from 'REJECT' or (final_payable = 0 and decision_reason is not null)),
  CONSTRAINT "ck_decision_reason_kind" CHECK (decision_reason is null or decision_reason::text like 'OUTCOME_%'),
  CONSTRAINT "ck_decision_return" CHECK (decision_action is distinct from 'RETURN' or (final_payable is null and decision_comment is not null)),
  CONSTRAINT "ck_decision_balance_checked" CHECK (decision_action is null or decision_action = 'RETURN' or balance_version_checked is not null)
);

CREATE TABLE "audit_event" (
  "id" BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
  "occurred_at" timestamptz NOT NULL DEFAULT (now()),
  "actor_type" actor_type NOT NULL,
  "actor_id" uuid,
  "claim_id" uuid,
  "event_type" varchar(60) NOT NULL,
  "entity_type" varchar(40) NOT NULL,
  "entity_id" varchar(64) NOT NULL,
  "before_value" jsonb,
  "after_value" jsonb,
  "ai_run_id" uuid,
  "request_id" varchar(64),
  "ip_address" inet,
  CONSTRAINT "ck_audit_user_actor" CHECK (actor_type <> 'USER' or actor_id is not null)
);

CREATE UNIQUE INDEX "ux_staff_account_email" ON "staff_account" ((lower(email)));

CREATE UNIQUE INDEX "ux_staff_account_id_role" ON "staff_account" ("id", "role");

CREATE INDEX "ix_refresh_token_family" ON "refresh_token" ("family_id");

CREATE INDEX "ix_refresh_token_account" ON "refresh_token" ("staff_account_id", "expires_at");

CREATE INDEX "ix_member_full_name" ON "member" ("full_name");

CREATE INDEX "ix_policy_member" ON "policy" ("member_id");

CREATE UNIQUE INDEX "ux_policy_id_member" ON "policy" ("id", "member_id");

CREATE UNIQUE INDEX "ux_rule_set_version_no" ON "rule_set_version" ("plan_id", "version_no");

CREATE INDEX "ix_rule_set_version_effective" ON "rule_set_version" ("plan_id", "status", "effective_from");

CREATE UNIQUE INDEX "ux_benefit_rule_code" ON "benefit_rule" ("rule_set_version_id", "rule_code");

CREATE INDEX "ix_benefit_rule_order" ON "benefit_rule" ("rule_set_version_id", "rule_type", "apply_order");

CREATE INDEX "ix_claim_queue" ON "claim" ("status", "assignee_id");

CREATE INDEX "ix_claim_registered_by" ON "claim" ("registered_by");

CREATE INDEX "ix_claim_policy" ON "claim" ("policy_id", "service_date");

CREATE INDEX "ix_document_claim" ON "document" ("claim_id");

CREATE UNIQUE INDEX "ux_document_id_claim" ON "document" ("id", "claim_id");

CREATE UNIQUE INDEX "ux_document_page_no" ON "document_page" ("document_id", "page_no");

CREATE UNIQUE INDEX "ux_document_page_claim_no" ON "document_page" ("claim_id", "claim_page_no");

CREATE INDEX "ix_ai_run_claim" ON "ai_run" ("claim_id", "task", "requested_at");

CREATE INDEX "ix_ai_run_model" ON "ai_run" ("model_version", "requested_at");

CREATE UNIQUE INDEX "ux_claim_field_slot" ON "claim_field" ("claim_id", "document_id", "field_key", "item_no");

CREATE INDEX "ix_claim_field_status" ON "claim_field" ("claim_id", "status");

CREATE INDEX "ix_claim_field_value" ON "claim_field" ("field_key", "value_text");

CREATE UNIQUE INDEX "ux_expense_line_no" ON "expense_line" ("document_id", "line_no");

CREATE INDEX "ix_expense_line_claim" ON "expense_line" ("claim_id");

CREATE UNIQUE INDEX "ux_alert_fingerprint" ON "alert" ("claim_id", "fingerprint");

CREATE INDEX "ix_alert_open" ON "alert" ("claim_id", "status", "severity");

CREATE INDEX "ix_risk_flag_claim" ON "risk_flag" ("claim_id", "status");

CREATE UNIQUE INDEX "ux_calculation_revision_no" ON "calculation_revision" ("claim_id", "revision_no");

CREATE UNIQUE INDEX "ux_calculation_line" ON "calculation_line" ("revision_id", "expense_line_id");

CREATE INDEX "ix_recommendation_claim" ON "recommendation" ("claim_id", "created_at");

CREATE INDEX "ix_recommendation_approval_queue" ON "recommendation" ("status", "submitted_at");

CREATE INDEX "ix_recommendation_decided_by" ON "recommendation" ("decided_by", "decided_at");

CREATE INDEX "ix_audit_claim_timeline" ON "audit_event" ("claim_id", "occurred_at");

CREATE INDEX "ix_audit_actor" ON "audit_event" ("actor_id", "occurred_at");

CREATE INDEX "ix_audit_type" ON "audit_event" ("event_type", "occurred_at");

COMMENT ON TABLE "staff_account" IS 'Seeded in the MVP; there is no account administration screen.';

COMMENT ON COLUMN "staff_account"."initials" IS 'Replaces the AI marker once this person accepts or edits a value';

COMMENT ON COLUMN "staff_account"."role" IS 'Exactly one role per account (US01)';

COMMENT ON TABLE "refresh_token" IS 'One row per refresh token, so sign-out and a disabled account can end a session (US01).
Expired rows can be purged; the sign-in itself is in audit_event.
';

COMMENT ON COLUMN "refresh_token"."family_id" IS 'Every token rotated from one sign-in; reuse after the grace period revokes the family';

COMMENT ON COLUMN "refresh_token"."token_hash" IS 'SHA-256; the token itself is never stored';

COMMENT ON COLUMN "refresh_token"."expires_at" IS '8 hours after sign-in; rotation copies it and never extends it';

COMMENT ON COLUMN "refresh_token"."rotated_at" IS 'Still accepted for 10 seconds, so two tabs can refresh together';

COMMENT ON TABLE "member" IS 'The insured person. Seeded; searched in step 1 of New claim (US02).';

COMMENT ON COLUMN "member"."member_code" IS 'e.g. MB-0007731';

COMMENT ON COLUMN "member"."full_name" IS 'Identity data (BR-11)';

COMMENT ON COLUMN "member"."national_id" IS 'Citizen ID (CCCD). Identity data (BR-11)';

COMMENT ON COLUMN "benefit_plan"."code" IS 'e.g. GOLD-OP';

COMMENT ON COLUMN "benefit_plan"."name" IS 'e.g. Gold Outpatient';

COMMENT ON TABLE "policy" IS 'One insured person and one policy year per policy, so the annual outpatient balance lives here.
Remaining = annual LIMIT rule - consumed_amount. Approval locks this row (SELECT ... FOR UPDATE),
compares balance_version with the calculation revision and blocks if it changed (BR-10).
';

COMMENT ON COLUMN "policy"."policy_number" IS 'e.g. PL-2026-00731';

COMMENT ON COLUMN "policy"."coverage_start" IS 'One policy covers one policy year';

COMMENT ON COLUMN "policy"."first_enrolled_on" IS 'Start of waiting periods; earlier than coverage_start after a renewal';

COMMENT ON COLUMN "policy"."opening_consumed" IS 'Annual usage before ClaimLens (seed), VND';

COMMENT ON COLUMN "policy"."consumed_amount" IS 'opening_consumed + final payable of every approved claim (BR-10)';

COMMENT ON COLUMN "policy"."balance_version" IS 'Raised by every approval; each calculation revision records the value it read';

COMMENT ON TABLE "rule_set_version" IS 'Adjudication uses the PUBLISHED version of the member plan with the greatest effective_from on or before
the service date (BR-04). A published version and its rules cannot be changed (trigger); a new version supersedes it.
A plan has at most one DRAFT (partial unique index in SQL).
';

COMMENT ON COLUMN "rule_set_version"."version_no" IS 'Shown as v3';

COMMENT ON COLUMN "rule_set_version"."effective_from" IS 'Required to publish; later than the current published version (US19)';

COMMENT ON COLUMN "rule_set_version"."based_on_version_id" IS 'Version the draft was copied from';

COMMENT ON TABLE "benefit_category" IS 'Stable taxonomy that AI-02 maps lines to and the dashboard groups by. Whether a category is covered is versioned in benefit_rule.';

COMMENT ON COLUMN "benefit_category"."code" IS 'e.g. CONSULTATION, LAB_TEST, PRESCRIPTION_DRUG, DIAGNOSTIC_IMAGING, PROCEDURE, COSMETIC';

COMMENT ON COLUMN "benefit_category"."name" IS 'e.g. Consultation';

COMMENT ON TABLE "benefit_rule" IS 'One table for the five rule kinds in the editor tabs plus eligibility. Publishing checks that every active category has one COVERAGE or one EXCLUSION rule.';

COMMENT ON COLUMN "benefit_rule"."rule_code" IS 'Cited on lines, formulas and summaries: COV-01, EX-04, COINS-80, LIM-ANNUAL';

COMMENT ON COLUMN "benefit_rule"."category_code" IS 'Required for COVERAGE and EXCLUSION; empty means every category';

COMMENT ON COLUMN "benefit_rule"."clause_text" IS 'Contract clause shown next to the line result';

COMMENT ON COLUMN "benefit_rule"."clause_ref" IS 'Clause number in the contract';

COMMENT ON COLUMN "benefit_rule"."apply_order" IS 'Order of application inside the version (BR-05)';

COMMENT ON COLUMN "benefit_rule"."rate_bp" IS 'COINSURANCE: share the insurer pays, 8000 = 80%';

COMMENT ON COLUMN "benefit_rule"."limit_amount" IS 'LIMIT: VND';

COMMENT ON COLUMN "benefit_rule"."limit_scope" IS 'LIMIT: per visit or annual';

COMMENT ON COLUMN "benefit_rule"."waiting_days" IS 'WAITING_PERIOD: days from policy.first_enrolled_on';

COMMENT ON TABLE "sample_claim" IS 'Synthetic claim for rule-set test runs (US20), kept apart from claim so the Administrator never reads claim content (BR-11).
A test run is computed on request and not stored; it writes nothing to claims or balances.
';

COMMENT ON COLUMN "sample_claim"."code" IS 'e.g. SMP-GOLDEN-01';

COMMENT ON COLUMN "sample_claim"."consumed_before" IS 'Annual usage before this claim, VND';

COMMENT ON COLUMN "sample_claim"."lines" IS '[{line_no, description, category_code, claimed_amount}]';

COMMENT ON TABLE "claim" IS 'Status moves only along the lifecycle in docs/03-use-cases.md (trigger). Returned = IN_REVIEW whose latest
recommendation is RETURNED. Queue priority (BR-09) is computed: high value (payable in the approval queue,
claimed_total in the review queue), then open or kept HIGH flags, then age.
';

COMMENT ON COLUMN "claim"."claim_number" IS 'CLM-YYYY-NNNNNN';

COMMENT ON COLUMN "claim"."provider_name" IS 'As entered at intake, e.g. Phòng khám Đa khoa Hồng Ngọc';

COMMENT ON COLUMN "claim"."service_date" IS 'Selects the rule-set version (BR-04)';

COMMENT ON COLUMN "claim"."claimed_total" IS 'VND, entered at intake';

COMMENT ON COLUMN "claim"."status_changed_at" IS 'Queue age';

COMMENT ON COLUMN "claim"."registered_by" IS 'Intake Officer; may see this claim (BR-11)';

COMMENT ON COLUMN "claim"."assignee_id" IS 'One reviewer per claim; an attribute, not a state (US23)';

COMMENT ON COLUMN "claim"."golden_scenario" IS 'Set only on golden-set claims (Definition of Done, US21)';

COMMENT ON COLUMN "claim"."row_version" IS 'Optimistic lock behind the Conflict UI state';

COMMENT ON TABLE "document" IS 'An uploaded file. Removing it before submission keeps this row and deletes its pages.';

COMMENT ON COLUMN "document"."original_key" IS 'Object-storage key of the original file';

COMMENT ON COLUMN "document"."rendition_key" IS 'PDF rendition of a DOCX or XLSX file (US03)';

COMMENT ON TABLE "document_page" IS 'Page classification macro-F1 compares ai_page_type with page_type.';

COMMENT ON COLUMN "document_page"."page_no" IS 'Page inside the file';

COMMENT ON COLUMN "document_page"."claim_page_no" IS 'p1..p30 across the claim';

COMMENT ON COLUMN "document_page"."image_key" IS 'Rendered page image for the viewer and AI';

COMMENT ON COLUMN "document_page"."page_type" IS 'Type in use';

COMMENT ON COLUMN "document_page"."ai_status" IS 'Document rail status';

COMMENT ON COLUMN "document_page"."ai_run_id" IS 'Latest page check or re-read';

COMMENT ON COLUMN "document_page"."ai_page_type" IS 'AI suggestion; Unclassified in use when below 0.80';

COMMENT ON TABLE "ai_run" IS 'A failed run is the fallback trigger: pages Unclassified, fields empty, Clinical check not run.';

COMMENT ON COLUMN "ai_run"."id" IS 'The run ID shown on the timeline (US05)';

COMMENT ON COLUMN "ai_run"."scope_document_id" IS 'PAGE_CHECK: the uploaded file';

COMMENT ON COLUMN "ai_run"."scope_page_id" IS 'PAGE_REREAD: the only page read again';

COMMENT ON COLUMN "ai_run"."model_version" IS 'Dashboard filter, e.g. v2.4';

COMMENT ON COLUMN "ai_run"."triggered_by" IS 'Empty when the system started it as part of a task';

COMMENT ON COLUMN "ai_run"."raw_output_key" IS 'Object-storage key of the full AI response, kept for every run';

COMMENT ON TABLE "claim_field" IS 'One slot per field per document (and per line for line fields). The value in use and the AI suggestion sit side by side;
ck_claim_field_source_required enforces BR-02. Every change writes audit_event with old and new value (US06).
Field rules (threshold group, required, sensitive) are in application code, keyed by field_key.
';

COMMENT ON COLUMN "claim_field"."document_id" IS 'Evidence tab groups fields by document';

COMMENT ON COLUMN "claim_field"."item_no" IS '0 = document header; n = expense line or prescription item n';

COMMENT ON COLUMN "claim_field"."expense_line_id" IS 'Line fields only';

COMMENT ON COLUMN "claim_field"."value_text" IS 'Value in use, as text';

COMMENT ON COLUMN "claim_field"."value_amount" IS 'Money fields, VND';

COMMENT ON COLUMN "claim_field"."value_date" IS 'Date fields';

COMMENT ON COLUMN "claim_field"."source_bbox" IS '{x, y, w, h}; drawn by the reviewer when AI gave none';

COMMENT ON COLUMN "claim_field"."ai_run_id" IS 'Latest extraction or re-read that filled the ai_* columns';

COMMENT ON COLUMN "claim_field"."ai_value" IS 'Raw AI value; kept even below 0.70 but never shown (US05)';

COMMENT ON COLUMN "claim_field"."ai_confidence" IS 'The Evidence tab sorts lowest first';

COMMENT ON TABLE "expense_line" IS 'Accepted line values (projection of the line fields, written in the same transaction), the AI-02 suggestion,
the category in use and the active override. Every later revision applies the override; the payable stays capped
by the remaining limit (BR-07). Removing an override clears the columns and is audited.
A removed line stays as a row, is left out of later revisions and no longer counts as a required field slot.
';

COMMENT ON COLUMN "expense_line"."document_id" IS 'The invoice the line was read from';

COMMENT ON COLUMN "expense_line"."description" IS 'As printed, e.g. Kem dưỡng phục hồi da Obagi';

COMMENT ON COLUMN "expense_line"."amount" IS 'Claimed line amount, VND; required before adjudication';

COMMENT ON COLUMN "expense_line"."mapping_run_id" IS 'AI-02 run that filled ai_category_code';

COMMENT ON COLUMN "expense_line"."ai_level" IS 'HIGH at 0.85 or more: auto-accepted';

COMMENT ON COLUMN "expense_line"."category_code" IS 'Benefit category in use';

COMMENT ON COLUMN "expense_line"."override_result" IS 'Active reviewer override (US16)';

COMMENT ON COLUMN "expense_line"."removed_by" IS 'Line removed by the reviewer; earlier revisions still point to it';

COMMENT ON TABLE "alert" IS 'Deterministic problems, including exact duplicates (BR-08). An open BLOCKING alert blocks adjudication.';

COMMENT ON COLUMN "alert"."fingerprint" IS 'Same problem across re-validations, e.g. DATE_CONSISTENCY:prescription';

COMMENT ON COLUMN "alert"."message" IS 'e.g. Prescription date 13/10/2026 differs from invoice date 12/10/2026';

COMMENT ON COLUMN "alert"."details" IS 'Conflicting values with their claim_field ids';

COMMENT ON COLUMN "alert"."duplicate_of_claim_id" IS 'US08: the other claim';

COMMENT ON COLUMN "alert"."resolved_by" IS 'Empty when the system closed it after a data fix';

COMMENT ON TABLE "risk_flag" IS 'AI-03 clinical signal (BR-08). Never changes amounts or line results.';

COMMENT ON COLUMN "risk_flag"."ai_run_id" IS 'The CLINICAL_CHECK run';

COMMENT ON COLUMN "risk_flag"."expense_line_id" IS 'The line the signal is about';

COMMENT ON COLUMN "risk_flag"."diagnosis_field_id" IS 'The diagnosis it was compared with';

COMMENT ON COLUMN "risk_flag"."relevance_score" IS 'AI relevance of the line to the diagnosis';

COMMENT ON COLUMN "risk_flag"."ai_reason" IS 'One sentence, shown as Signal - not a finding';

COMMENT ON COLUMN "risk_flag"."eval_label" IS 'Golden-set claims only; gives the false-positive rate (US09, US21)';

COMMENT ON TABLE "calculation_revision" IS 'Immutable snapshot. Any change to fields, mappings, documents or overrides creates a new revision; old ones stay readable (US13).';

COMMENT ON COLUMN "calculation_revision"."revision_no" IS 'Shown as Revision #2; the highest number is current';

COMMENT ON COLUMN "calculation_revision"."rule_set_version_id" IS 'Effective on the service date (BR-04)';

COMMENT ON COLUMN "calculation_revision"."input_hash" IS 'SHA-256 of the inputs, balance included; same inputs and version give the same result (US11). A different hash for the current inputs marks the revision stale (US13)';

COMMENT ON COLUMN "calculation_revision"."eligible_total" IS 'Sum of final covered amounts';

COMMENT ON COLUMN "calculation_revision"."after_rate_amount" IS 'Eligible total after the reimbursement rate';

COMMENT ON COLUMN "calculation_revision"."balance_version" IS 'policy.balance_version read; compared at approval (BR-10)';

COMMENT ON COLUMN "calculation_revision"."remaining_limit" IS 'min(per-visit limit, annual limit - consumed) (BR-05)';

COMMENT ON COLUMN "calculation_revision"."payable_amount" IS 'min(after_rate_amount, remaining_limit)';

COMMENT ON COLUMN "calculation_revision"."formula" IS 'Formula rows shown on screen: [{label, rule_code, delta, total}]';

COMMENT ON TABLE "calculation_line" IS 'The engine result stays next to the final result, so an override never hides what the rules said (US16).';

COMMENT ON COLUMN "calculation_line"."category_code" IS 'Mapping used';

COMMENT ON COLUMN "calculation_line"."engine_rule_id" IS 'Rule that decided the line, e.g. EX-04';

COMMENT ON COLUMN "calculation_line"."changed_since_previous" IS 'Highlighted after a re-run (US13)';

COMMENT ON TABLE "recommendation" IS 'One row per submission, with the approver decision on the same row (1 to 0..1).
A RETURNED row stays; the reviewer starts a new one. This is the only way a claim reaches
Approved, PartiallyApproved or Rejected (BR-01); an approval adds final_payable to policy.consumed_amount.
';

COMMENT ON COLUMN "recommendation"."calculation_revision_id" IS 'Revision the reviewer recommended on';

COMMENT ON COLUMN "recommendation"."proposed_outcome" IS 'From the payable (BR-13)';

COMMENT ON COLUMN "recommendation"."outcome_reason" IS 'OUTCOME_ reason; required when the reviewer differs from the proposal (BR-13)';

COMMENT ON COLUMN "recommendation"."recommended_payable" IS 'Set by the system: the revision payable, or 0 when recommending REJECTED';

COMMENT ON COLUMN "recommendation"."claimed_total" IS 'Snapshot at submission, used by the BR-13 checks';

COMMENT ON COLUMN "recommendation"."summary" IS 'Sentences with citation chips: [{text, origin, citations: [{label, kind, id}]}]';

COMMENT ON COLUMN "recommendation"."summary_ai_run_id" IS 'AI-04 draft run; empty when AI was unavailable';

COMMENT ON COLUMN "recommendation"."summary_ai_draft" IS 'Raw AI draft, kept to measure the edit rate';

COMMENT ON COLUMN "recommendation"."summary_edited" IS 'US15';

COMMENT ON COLUMN "recommendation"."attested_values" IS 'Auto-accepted AI values used in the calculation: [{kind, id, value, confidence}]';

COMMENT ON COLUMN "recommendation"."attested_at" IS 'Reviewer confirmed the attestation (US17)';

COMMENT ON COLUMN "recommendation"."open_high_flags" IS 'Open HIGH flags at submission; shown as a warning';

COMMENT ON COLUMN "recommendation"."decision_action" IS 'Senior Approver action (UC08)';

COMMENT ON COLUMN "recommendation"."decision_revision_id" IS 'Revision the decision rests on; newer after a BALANCE_RECHECK';

COMMENT ON COLUMN "recommendation"."final_payable" IS 'Set by the system from the decision revision: its payable, 0 for REJECT, empty for RETURN';

COMMENT ON COLUMN "recommendation"."decision_reason" IS 'OUTCOME_ reason; required for REJECT';

COMMENT ON COLUMN "recommendation"."decision_comment" IS 'Required for RETURN';

COMMENT ON COLUMN "recommendation"."balance_version_checked" IS 'policy.balance_version seen at approval (BR-10)';

COMMENT ON TABLE "audit_event" IS 'BR-12. Insert-only: UPDATE and DELETE are revoked from the application role. Feeds the claim Timeline (US22).
It also keeps what the tables overwrite: earlier AI suggestions, earlier overrides and rule-set test runs.
';

COMMENT ON COLUMN "audit_event"."claim_id" IS 'Empty for sign-in and rule-set events';

COMMENT ON COLUMN "audit_event"."event_type" IS 'e.g. CLAIM_STATUS_CHANGED, FIELD_CORRECTED, AI_RUN_FINISHED, LINE_OVERRIDDEN, DECISION_RECORDED, CLAIM_VIEWED, SIGNED_IN';

ALTER TABLE "refresh_token" ADD FOREIGN KEY ("staff_account_id") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "policy" ADD FOREIGN KEY ("member_id") REFERENCES "member" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "policy" ADD FOREIGN KEY ("plan_id") REFERENCES "benefit_plan" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "rule_set_version" ADD FOREIGN KEY ("plan_id") REFERENCES "benefit_plan" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "rule_set_version" ADD FOREIGN KEY ("based_on_version_id") REFERENCES "rule_set_version" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "rule_set_version" ADD FOREIGN KEY ("created_by") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "rule_set_version" ADD FOREIGN KEY ("published_by") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "benefit_rule" ADD FOREIGN KEY ("rule_set_version_id") REFERENCES "rule_set_version" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "benefit_rule" ADD FOREIGN KEY ("category_code") REFERENCES "benefit_category" ("code") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "sample_claim" ADD FOREIGN KEY ("plan_id") REFERENCES "benefit_plan" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "sample_claim" ADD FOREIGN KEY ("created_by") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "claim" ADD FOREIGN KEY ("member_id") REFERENCES "member" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "claim" ADD FOREIGN KEY ("registered_by") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "claim" ADD FOREIGN KEY ("assignee_id") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "document" ADD FOREIGN KEY ("claim_id") REFERENCES "claim" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "document" ADD FOREIGN KEY ("uploaded_by") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "document" ADD FOREIGN KEY ("removed_by") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "document_page" ADD FOREIGN KEY ("page_type_set_by") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "document_page" ADD FOREIGN KEY ("ai_run_id") REFERENCES "ai_run" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "ai_run" ADD FOREIGN KEY ("claim_id") REFERENCES "claim" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "ai_run" ADD FOREIGN KEY ("scope_document_id") REFERENCES "document" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "ai_run" ADD FOREIGN KEY ("scope_page_id") REFERENCES "document_page" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "ai_run" ADD FOREIGN KEY ("triggered_by") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "claim_field" ADD FOREIGN KEY ("claim_id") REFERENCES "claim" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "claim_field" ADD FOREIGN KEY ("document_id") REFERENCES "document" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "claim_field" ADD FOREIGN KEY ("expense_line_id") REFERENCES "expense_line" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "claim_field" ADD FOREIGN KEY ("source_page_id") REFERENCES "document_page" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "claim_field" ADD FOREIGN KEY ("ai_run_id") REFERENCES "ai_run" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "claim_field" ADD FOREIGN KEY ("ai_page_id") REFERENCES "document_page" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "claim_field" ADD FOREIGN KEY ("resolved_by") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "expense_line" ADD FOREIGN KEY ("claim_id") REFERENCES "claim" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "expense_line" ADD FOREIGN KEY ("mapping_run_id") REFERENCES "ai_run" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "expense_line" ADD FOREIGN KEY ("ai_category_code") REFERENCES "benefit_category" ("code") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "expense_line" ADD FOREIGN KEY ("category_code") REFERENCES "benefit_category" ("code") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "expense_line" ADD FOREIGN KEY ("mapping_resolved_by") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "expense_line" ADD FOREIGN KEY ("override_by") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "expense_line" ADD FOREIGN KEY ("removed_by") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "expense_line" ADD CONSTRAINT "expense_line_document_claim" FOREIGN KEY ("document_id", "claim_id") REFERENCES "document" ("id", "claim_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "alert" ADD FOREIGN KEY ("claim_id") REFERENCES "claim" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "alert" ADD FOREIGN KEY ("duplicate_of_claim_id") REFERENCES "claim" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "alert" ADD FOREIGN KEY ("resolved_by") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "risk_flag" ADD FOREIGN KEY ("claim_id") REFERENCES "claim" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "risk_flag" ADD FOREIGN KEY ("ai_run_id") REFERENCES "ai_run" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "risk_flag" ADD FOREIGN KEY ("expense_line_id") REFERENCES "expense_line" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "risk_flag" ADD FOREIGN KEY ("diagnosis_field_id") REFERENCES "claim_field" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "risk_flag" ADD FOREIGN KEY ("reviewed_by") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "calculation_revision" ADD FOREIGN KEY ("claim_id") REFERENCES "claim" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "calculation_revision" ADD FOREIGN KEY ("rule_set_version_id") REFERENCES "rule_set_version" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "calculation_revision" ADD FOREIGN KEY ("triggered_by") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "calculation_line" ADD FOREIGN KEY ("revision_id") REFERENCES "calculation_revision" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "calculation_line" ADD FOREIGN KEY ("expense_line_id") REFERENCES "expense_line" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "calculation_line" ADD FOREIGN KEY ("category_code") REFERENCES "benefit_category" ("code") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "calculation_line" ADD FOREIGN KEY ("engine_rule_id") REFERENCES "benefit_rule" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "recommendation" ADD FOREIGN KEY ("claim_id") REFERENCES "claim" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "recommendation" ADD FOREIGN KEY ("calculation_revision_id") REFERENCES "calculation_revision" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "recommendation" ADD FOREIGN KEY ("summary_ai_run_id") REFERENCES "ai_run" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "recommendation" ADD FOREIGN KEY ("submitted_by") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "recommendation" ADD FOREIGN KEY ("decision_revision_id") REFERENCES "calculation_revision" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "recommendation" ADD CONSTRAINT "recommendation_decider_role" FOREIGN KEY ("decided_by", "decider_role") REFERENCES "staff_account" ("id", "role") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "claim" ADD CONSTRAINT "claim_policy_member" FOREIGN KEY ("policy_id", "member_id") REFERENCES "policy" ("id", "member_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "document_page" ADD CONSTRAINT "page_document_claim" FOREIGN KEY ("document_id", "claim_id") REFERENCES "document" ("id", "claim_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "audit_event" ADD FOREIGN KEY ("actor_id") REFERENCES "staff_account" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "audit_event" ADD FOREIGN KEY ("claim_id") REFERENCES "claim" ("id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "audit_event" ADD FOREIGN KEY ("ai_run_id") REFERENCES "ai_run" ("id") DEFERRABLE INITIALLY IMMEDIATE;
